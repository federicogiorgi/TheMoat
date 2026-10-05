using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheMoat
{
    public static class MoatManager
    {
        private static ThingDef cachedFallbackRock;
        private static ThingDef cachedTileRock;

        public static bool IsStartingBase(Map map)
        {
            if (map == null) return false;

            // 1. Only during initial game creation (Find.GameInitData is null during all normal gameplay)
            if (Find.GameInitData == null) return false;

            // 2. Check persistent component flag (saved and loaded with the save file)
            MoatGameComponent comp = Current.Game != null ? Current.Game.GetComponent<MoatGameComponent>() : null;
            if (comp != null && comp.hasGeneratedStartingBase) return false;

            // 3. Must be player faction home
            if (map.ParentFaction != Faction.OfPlayer && !map.IsPlayerHome) return false;

            // 4. Must match the exact starting tile chosen on the world map during game setup
            if (map.Tile != Find.GameInitData.startingTile) return false;

            // 5. Must be the first player home map (no other colony map already exists)
            if (Find.Maps != null)
            {
                for (int i = 0; i < Find.Maps.Count; i++)
                {
                    Map m = Find.Maps[i];
                    if (m != map && m.IsPlayerHome)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static int GetSplitX(Map map)
        {
            float percent = MoatMod.Settings != null ? MoatMod.Settings.mountainPercent : 0.50f;
            percent = Mathf.Clamp(percent, 0.10f, 0.90f);
            return (int)(map.Size.x * percent);
        }

        public static ThingDef GetRockDefForMap(Map map, IntVec3 c)
        {
            // 1. Try vanilla RockDefAt while map gen noise grids are active
            try
            {
                ThingDef rock = GenStep_RocksFromGrid.RockDefAt(c);
                if (rock != null) return rock;
            }
            catch
            {
                // Working data (RockNoises) has already been cleared or is not initialized
            }

            // 2. Try the cached rock def for this tile
            if (cachedTileRock != null) return cachedTileRock;

            // 3. Try to query the world's natural rock types for this tile
            if (map != null && Find.World != null)
            {
                try
                {
                    foreach (ThingDef r in Find.World.NaturalRockTypesIn(map.Tile))
                    {
                        if (r != null)
                        {
                            cachedTileRock = r;
                            return r;
                        }
                    }
                }
                catch
                {
                }
            }

            // 4. Fallback to any natural rock def in database
            return GetFallbackRockDef();
        }

        public static void EnforceElevationAndCaves(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                cachedTileRock = null; // Reset cache for new map
                int splitX = GetSplitX(map);
                MapGenFloatGrid elev = MapGenerator.Elevation;
                MapGenFloatGrid caves = MapGenerator.Caves;

                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = 0; x < map.Size.x; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);

                        if (x < splitX)
                        {
                            // Left: Deep impassable mountain elevation, strictly zero caves
                            if (elev != null) elev[c] = 2.0f;
                            if (caves != null) caves[c] = 0f;
                        }
                        else
                        {
                            // Right: Flat elevation, zero caves
                            if (elev != null && MoatMod.Settings.flattenHillsOnPlains)
                            {
                                elev[c] = 0.30f;
                            }
                            if (caves != null) caves[c] = 0f;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error in EnforceElevationAndCaves: " + ex);
            }
        }

        public static void EnforceRocksAndRoofs(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                int splitX = GetSplitX(map);

                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = 0; x < splitX; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);

                        // 1. Thick rock roof across the entire mountain
                        map.roofGrid.SetRoof(c, RoofDefOf.RoofRockThick);

                        // 2. Solid rock wall
                        Building edifice = c.GetEdifice(map);
                        bool isNaturalRock = edifice != null && edifice.def != null && edifice.def.building != null && edifice.def.building.isNaturalRock;
                        bool isMineableOre = isNaturalRock && edifice.def.building.isResourceRock;

                        if (!isNaturalRock || (!MoatMod.Settings.allowOresInMountain && isMineableOre))
                        {
                            if (edifice != null)
                            {
                                edifice.Destroy(DestroyMode.Vanish);
                            }

                            ThingDef rockDef = GetRockDefForMap(map, c);
                            if (rockDef != null)
                            {
                                GenSpawn.Spawn(rockDef, c, map, WipeMode.Vanish);
                            }
                        }
                    }
                }

                // Right side: ensure no mountains or thick roofs
                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = splitX; x < map.Size.x; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);

                        // Clear any mountain roofs
                        RoofDef r = map.roofGrid.RoofAt(c);
                        if (r == RoofDefOf.RoofRockThick || r == RoofDefOf.RoofRockThin)
                        {
                            map.roofGrid.SetRoof(c, null);
                        }

                        // Remove any natural rock walls on the plains
                        Building edifice = c.GetEdifice(map);
                        if (edifice != null && edifice.def != null && edifice.def.building != null && edifice.def.building.isNaturalRock)
                        {
                            edifice.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error in EnforceRocksAndRoofs: " + ex);
            }
        }

        // Vanilla scales ore lumps by the tile's hilliness over the whole map (a flat tile gets about a quarter
        // of what a mountainous one gets), so the mountain half ends up nearly empty on flat tiles.
        // Top it up to roughly the ore density of a vanilla mountainous map.
        private const float OreLumpsPer10kMountainCells = 20f;
        private const int OreLumpEdgeMargin = 5;

        public static void TopUpMountainOres(Map map, GenStepParams parms)
        {
            if (!IsStartingBase(map)) return;
            if (!MoatMod.Settings.allowOresInMountain) return;

            try
            {
                int splitX = GetSplitX(map);
                int maxCenterX = splitX - OreLumpEdgeMargin;
                if (maxCenterX <= 0) return;

                int targetLumps = Mathf.RoundToInt(splitX * map.Size.z / 10000f * OreLumpsPer10kMountainCells);
                int existingLumps = CountOreLumps(map, splitX);
                int lumpsToAdd = targetLumps - existingLumps;
                if (lumpsToAdd <= 0) return;

                MoatOreScatterer scatterer = new MoatOreScatterer();
                int added = 0;
                for (int i = 0; i < lumpsToAdd; i++)
                {
                    for (int attempt = 0; attempt < 50; attempt++)
                    {
                        IntVec3 c = new IntVec3(Rand.Range(0, maxCenterX), 0, Rand.Range(0, map.Size.z));
                        Building edifice = c.GetEdifice(map);
                        if (edifice != null && edifice.def.IsNonResourceNaturalRock)
                        {
                            scatterer.ScatterLump(c, map, parms);
                            added++;
                            break;
                        }
                    }
                }

                Log.Message("[The Moat] Ore lumps in the mountain: " + existingLumps + " from vanilla, " + added + " added.");
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error in TopUpMountainOres: " + ex);
            }
        }

        // Counts connected groups of ore cells (4-way) on the mountain side
        private static int CountOreLumps(Map map, int splitX)
        {
            int sizeX = map.Size.x;
            int sizeZ = map.Size.z;
            bool[] visited = new bool[sizeX * sizeZ];
            Stack<IntVec3> stack = new Stack<IntVec3>();
            int lumps = 0;

            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < splitX; x++)
                {
                    if (visited[z * sizeX + x] || !IsOreAt(map, new IntVec3(x, 0, z))) continue;

                    lumps++;
                    visited[z * sizeX + x] = true;
                    stack.Push(new IntVec3(x, 0, z));
                    while (stack.Count > 0)
                    {
                        IntVec3 cur = stack.Pop();
                        for (int d = 0; d < 4; d++)
                        {
                            IntVec3 n = cur + GenAdj.CardinalDirections[d];
                            if (n.x < 0 || n.x >= splitX || n.z < 0 || n.z >= sizeZ) continue;
                            int idx = n.z * sizeX + n.x;
                            if (visited[idx] || !IsOreAt(map, n)) continue;
                            visited[idx] = true;
                            stack.Push(n);
                        }
                    }
                }
            }
            return lumps;
        }

        private static bool IsOreAt(Map map, IntVec3 c)
        {
            Building edifice = c.GetEdifice(map);
            return edifice != null && edifice.def.building != null && edifice.def.building.isResourceRock;
        }

        public static void CleanTerrain(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                int splitX = GetSplitX(map);

                // Left side: natural stone floors under rock
                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = 0; x < splitX; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        ThingDef rockDef = GetRockDefForMap(map, c);

                        if (rockDef != null && rockDef.building != null && rockDef.building.naturalTerrain != null)
                        {
                            map.terrainGrid.SetTerrain(c, rockDef.building.naturalTerrain);
                        }
                    }
                }

                // Right side: replace any water/marsh with dry land
                if (MoatMod.Settings.removeWaterOnPlains)
                {
                    MapGenFloatGrid fertGrid = MapGenerator.Fertility;

                    for (int z = 0; z < map.Size.z; z++)
                    {
                        for (int x = splitX; x < map.Size.x; x++)
                        {
                            IntVec3 c = new IntVec3(x, 0, z);
                            TerrainDef currentTerrain = map.terrainGrid.TerrainAt(c);

                            if (IsWaterOrWetTerrain(currentTerrain))
                            {
                                float fert = fertGrid != null ? fertGrid[c] : 1.0f;
                                TerrainDef dry = GetBiomeDryTerrain(map, fert);
                                map.terrainGrid.SetTerrain(c, dry);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error in CleanTerrain: " + ex);
            }
        }

        public static bool IsWaterOrWetTerrain(TerrainDef def)
        {
            if (def == null) return false;
            if (def.IsWater || def.IsRiver || def.takeSplashes) return true;

            string name = def.defName.ToLowerInvariant();
            if (name.Contains("water") || name.Contains("marsh") || name.Contains("lake") || name.Contains("mud"))
            {
                return true;
            }
            return false;
        }

        public static TerrainDef GetBiomeDryTerrain(Map map, float fertility)
        {
            if (map.Biome != null && map.Biome.terrainsByFertility != null && map.Biome.terrainsByFertility.Count > 0)
            {
                TerrainDef t = TerrainThreshold.TerrainAtValue(map.Biome.terrainsByFertility, fertility);
                if (t != null && !IsWaterOrWetTerrain(t))
                {
                    return t;
                }
            }

            TerrainDef soil = TerrainDefOf.Soil;
            if (soil != null && !IsWaterOrWetTerrain(soil)) return soil;

            TerrainDef gravel = TerrainDefOf.Gravel;
            if (gravel != null) return gravel;

            return TerrainDefOf.Sand;
        }

        public static ThingDef GetFallbackRockDef()
        {
            if (cachedFallbackRock != null) return cachedFallbackRock;

            if (ThingDefOf.Granite != null)
            {
                cachedFallbackRock = ThingDefOf.Granite;
                return cachedFallbackRock;
            }

            // Plain stone (not an ore), with a natural floor so it is a real rock type and not e.g. collapsed rocks
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.IsNonResourceNaturalRock && def.building.naturalTerrain != null)
                {
                    cachedFallbackRock = def;
                    return def;
                }
            }
            return null;
        }

        public static void EnsurePlayerStartSpot(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                int splitX = GetSplitX(map);
                IntVec3 startSpot = MapGenerator.PlayerStartSpot;

                if (startSpot.x < splitX + 5 || !startSpot.Standable(map) || map.roofGrid.Roofed(startSpot))
                {
                    IntVec3 bestSpot = CellFinderLoose.TryFindCentralCell(
                        map,
                        10,
                        5,
                        delegate(IntVec3 c)
                        {
                            return c.x >= splitX + 5 && c.Standable(map) && map.roofGrid.RoofAt(c) == null;
                        },
                        false
                    );

                    if (bestSpot.IsValid)
                    {
                        MapGenerator.PlayerStartSpot = bestSpot;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[The Moat] Error in EnsurePlayerStartSpot: " + ex);
            }
        }

        public static void FinalizeMoatMap(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                EnsurePlayerStartSpot(map);

                // Mark as generated in GameComponent
                MoatGameComponent comp = Current.Game != null ? Current.Game.GetComponent<MoatGameComponent>() : null;
                if (comp != null)
                {
                    comp.hasGeneratedStartingBase = true;
                }

                Log.Message("[The Moat] Starting base generation complete. The western 50% is protected by an impassable mountain cliff!");
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error during FinalizeMoatMap: " + ex);
            }
        }
    }
}
