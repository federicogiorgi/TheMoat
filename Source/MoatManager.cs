using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace TheMoat
{
    public static class MoatManager
    {
        private static ThingDef cachedFallbackRock;
        private static ThingDef cachedTileRock;

        public static bool IsStartingBase(Map map)
        {
            if (map == null) return false;

            // GameInitData only exists while a new game is being set up
            if (Find.GameInitData == null) return false;

            MoatGameComponent comp = Current.Game != null ? Current.Game.GetComponent<MoatGameComponent>() : null;
            if (comp != null && comp.hasGeneratedStartingBase) return false;

            if (map.ParentFaction != Faction.OfPlayer && !map.IsPlayerHome) return false;

            if (map.Tile != Find.GameInitData.startingTile) return false;

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

        public static ThingDef GetRockDefForMap(Map map, IntVec3 c)
        {
            try
            {
                ThingDef rock = GenStep_RocksFromGrid.RockDefAt(c);
                if (rock != null) return rock;
            }
            catch
            {
                // Rock noise grids are gone outside map generation
            }

            if (cachedTileRock != null) return cachedTileRock;

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

            return GetFallbackRockDef();
        }

        public static void EnforceElevationAndCaves(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                cachedTileRock = null;
                MoatLayout layout = MoatLayout.For(map);
                MapGenFloatGrid elev = MapGenerator.Elevation;
                MapGenFloatGrid caves = MapGenerator.Caves;

                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = 0; x < map.Size.x; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);

                        if (layout.IsMountain(c))
                        {
                            if (elev != null) elev[c] = 2.0f;
                            if (caves != null) caves[c] = 0f;
                        }
                        else
                        {
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

        // Set while we run vanilla's cave digger, so the patch that blocks vanilla caves lets ours through
        [ThreadStatic] public static bool diggingMoatCaves;

        // Vanilla-style tunnels, dug only in the cave zone of the mountain (see MoatLayout.IsCaveZone)
        public static void GenerateMountainCaves(Map map)
        {
            if (!IsStartingBase(map) || !MoatMod.Settings.cavesInMountain) return;

            try
            {
                MoatLayout layout = MoatLayout.For(map);
                MapGenFloatGrid elevation = MapGenerator.Elevation;
                if (elevation == null || MapGenerator.Caves == null) return;

                // Same tunnel shape as the vanilla Caves landmark
                ModuleBase directionNoise = new Perlin(0.00205, 2.0, 0.5, 4, Rand.Int, QualityMode.Medium);
                MapGenCavesUtility.CaveGenParms parms = MapGenCavesUtility.CaveGenParms.Default;
                parms.branchChance = 0.02f;
                parms.allowBranchingAfterThisManyCells = 10;

                diggingMoatCaves = true;
                try
                {
                    MapGenCavesUtility.GenerateCaves(map, new BoolGrid(map), new List<IntVec3>(), directionNoise, parms,
                        c => c.InBounds(map) && elevation[c] > 0.7f && layout.IsCaveZone(c));
                }
                finally
                {
                    diggingMoatCaves = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error in GenerateMountainCaves: " + ex);
            }
        }

        private static bool IsCaveAt(IntVec3 c)
        {
            MapGenFloatGrid caves = MapGenerator.Caves;
            return caves != null && caves[c] > 0f;
        }

        public static void EnforceRocksAndRoofs(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                MoatLayout layout = MoatLayout.For(map);

                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = 0; x < map.Size.x; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (!layout.IsMountain(c)) continue;

                        map.roofGrid.SetRoof(c, RoofDefOf.RoofRockThick);

                        Building edifice = c.GetEdifice(map);
                        bool isNaturalRock = edifice != null && edifice.def != null && edifice.def.building != null && edifice.def.building.isNaturalRock;
                        bool isMineableOre = isNaturalRock && edifice.def.building.isResourceRock;

                        // Leave the tunnels open (only ever dug when caves are enabled)
                        if (!isNaturalRock && IsCaveAt(c)) continue;

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

                // Plains: no rock and no mountain roof
                for (int z = 0; z < map.Size.z; z++)
                {
                    for (int x = 0; x < map.Size.x; x++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (layout.IsMountain(c)) continue;

                        RoofDef r = map.roofGrid.RoofAt(c);
                        if (r == RoofDefOf.RoofRockThick || r == RoofDefOf.RoofRockThin)
                        {
                            map.roofGrid.SetRoof(c, null);
                        }

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
                MoatLayout layout = MoatLayout.For(map);

                int targetLumps = Mathf.RoundToInt(layout.MountainCellCount / 10000f * OreLumpsPer10kMountainCells);
                int existingLumps = CountOreLumps(map, layout);
                int lumpsToAdd = targetLumps - existingLumps;
                if (lumpsToAdd <= 0) return;

                MoatOreScatterer scatterer = new MoatOreScatterer();
                int added = 0;
                for (int i = 0; i < lumpsToAdd; i++)
                {
                    for (int attempt = 0; attempt < 50; attempt++)
                    {
                        int along = Rand.Range(0, layout.EdgeLength);
                        int maxCenterDepth = layout.MountainDepthAt(along) - OreLumpEdgeMargin;
                        if (maxCenterDepth <= 0) continue;
                        IntVec3 c = layout.CellAt(Rand.Range(0, maxCenterDepth), along);
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

        // Counts connected groups of ore cells (4-way) in the mountain
        private static int CountOreLumps(Map map, MoatLayout layout)
        {
            int sizeX = map.Size.x;
            int sizeZ = map.Size.z;
            bool[] visited = new bool[sizeX * sizeZ];
            Stack<IntVec3> stack = new Stack<IntVec3>();
            int lumps = 0;

            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (visited[z * sizeX + x] || !layout.IsMountain(c) || !IsOreAt(map, c)) continue;

                    lumps++;
                    visited[z * sizeX + x] = true;
                    stack.Push(c);
                    while (stack.Count > 0)
                    {
                        IntVec3 cur = stack.Pop();
                        for (int d = 0; d < 4; d++)
                        {
                            IntVec3 n = cur + GenAdj.CardinalDirections[d];
                            if (!n.InBounds(map) || !layout.IsMountain(n)) continue;
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

        // Vanilla only puts ancient mech remains on a patch with no plants at all (they come from gravel and rocky
        // ground near hills). The flattened, grassy plains have none, so search them ourselves and clear the plants.
        private const int MechSpotRadius = 3;      // 7x7, covers the biggest remains (5x5, 4x6) in any rotation
        private const int MechClearRadius = 8;     // room for the warwalker's legs and claws around the torso
        private const int MechEdgeMargin = 10;
        private const int MechMaxClearedSpots = 20;  // don't strip the plains bare if vanilla keeps rejecting spots
        private static readonly MethodInfo CanScatterAtMethod = AccessTools.Method(typeof(GenStep_Scatterer), "CanScatterAt");

        public static bool TryFindAncientMechsCell(GenStep_Scatterer scatterer, Map map, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            MoatLayout layout = MoatLayout.For(map);

            List<IntVec3> candidates = new List<IntVec3>();
            for (int z = MechEdgeMargin; z < map.Size.z - MechEdgeMargin; z++)
            {
                for (int x = MechEdgeMargin; x < map.Size.x - MechEdgeMargin; x++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (layout.DistanceFromMountain(c) >= MechEdgeMargin)
                    {
                        candidates.Add(c);
                    }
                }
            }
            candidates.Shuffle();

            int clearedSpots = 0;
            Dictionary<string, int> rejections = new Dictionary<string, int>();
            foreach (IntVec3 c in candidates)
            {
                if (clearedSpots >= MechMaxClearedSpots) break;
                string reason = WhyNotClearExceptPlants(map, CellRect.CenteredOn(c, MechSpotRadius));
                if (reason != null)
                {
                    int n;
                    rejections.TryGetValue(reason, out n);
                    rejections[reason] = n + 1;
                    continue;
                }

                int cleared = ClearPlants(map, CellRect.CenteredOn(c, MechSpotRadius));
                clearedSpots++;
                if (!(bool)CanScatterAtMethod.Invoke(scatterer, new object[] { c, map })) continue;

                cleared += ClearPlants(map, CellRect.CenteredOn(c, MechClearRadius));
                result = c;
                Log.Message("[The Moat] Ancient mech remains placed on the plains at " + c + " (" + cleared + " plants cleared).");
                return true;
            }

            Log.Warning("[The Moat] Found no room for ancient mech remains on the plains (" + candidates.Count + " cells, "
                + clearedSpots + " spots rejected by vanilla, other rejections: "
                + string.Join(", ", rejections.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value).ToArray()) + ").");
            return false;
        }

        // Null if the area only has plants in the way, otherwise what blocks it
        private static string WhyNotClearExceptPlants(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect)
            {
                if (!c.InBounds(map)) return "out of bounds";
                if (c.Roofed(map)) return "roofed";
                Building edifice = c.GetEdifice(map);
                if (edifice != null) return "edifice " + edifice.def.defName;
                if (!c.Walkable(map)) return "not walkable (" + c.GetTerrain(map).defName + ")";

                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!things[i].def.IsPlant) return "thing " + things[i].def.defName;
                }
            }
            return null;
        }

        private static int ClearPlants(Map map, CellRect rect)
        {
            int cleared = 0;
            foreach (IntVec3 c in rect.ClipInsideMap(map))
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    if (things[i].def.IsPlant)
                    {
                        things[i].Destroy(DestroyMode.Vanish);
                        cleared++;
                    }
                }
            }
            return cleared;
        }

        public static void CleanTerrain(Map map)
        {
            if (!IsStartingBase(map)) return;

            try
            {
                MoatLayout layout = MoatLayout.For(map);

                // Mountain: vanilla already lays the matching rock floor under each rock type

                // Plains: replace any water/marsh with dry land
                if (MoatMod.Settings.removeWaterOnPlains)
                {
                    MapGenFloatGrid fertGrid = MapGenerator.Fertility;

                    for (int z = 0; z < map.Size.z; z++)
                    {
                        for (int x = 0; x < map.Size.x; x++)
                        {
                            IntVec3 c = new IntVec3(x, 0, z);
                            if (layout.IsMountain(c)) continue;
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
                MoatLayout layout = MoatLayout.For(map);
                IntVec3 startSpot = MapGenerator.PlayerStartSpot;

                if (layout.DistanceFromMountain(startSpot) < 5 || !startSpot.Standable(map) || map.roofGrid.Roofed(startSpot))
                {
                    IntVec3 bestSpot = CellFinderLoose.TryFindCentralCell(
                        map,
                        10,
                        5,
                        delegate(IntVec3 c)
                        {
                            return layout.DistanceFromMountain(c) >= 5 && c.Standable(map) && map.roofGrid.RoofAt(c) == null;
                        },
                        true
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
                MoatGameComponent comp = Current.Game != null ? Current.Game.GetComponent<MoatGameComponent>() : null;
                if (comp != null)
                {
                    comp.hasGeneratedStartingBase = true;
                }

                Log.Message("[The Moat] Starting map generated: mountain on the " + MoatMod.Settings.mountainSide.ToString().ToLowerInvariant()
                    + " side, " + MoatMod.Settings.mountainPercent.ToString("P0") + " of the map.");
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error during FinalizeMoatMap: " + ex);
            }
        }
    }
}
