using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TheMoat
{
    public static class MoatManager
    {
        private static ThingDef cachedFallbackRock;

        public static bool IsStartingBase(Map map)
        {
            if (map == null) return false;

            // 1. Only during new game creation (Find.GameInitData is null during all normal gameplay)
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

        public static void EnforceElevationAndCaves(Map map)
        {
            if (!IsStartingBase(map)) return;

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

        public static void EnforceRocksAndRoofs(Map map)
        {
            if (!IsStartingBase(map)) return;

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
                    bool isMineableOre = isNaturalRock && edifice.def.building.mineableThing != null;

                    if (!isNaturalRock || (!MoatMod.Settings.allowOresInMountain && isMineableOre))
                    {
                        if (edifice != null)
                        {
                            edifice.Destroy(DestroyMode.Vanish);
                        }

                        ThingDef rockDef = GenStep_RocksFromGrid.RockDefAt(c);
                        if (rockDef == null)
                        {
                            rockDef = GetFallbackRockDef();
                        }
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

        public static void CleanTerrain(Map map)
        {
            if (!IsStartingBase(map)) return;

            int splitX = GetSplitX(map);

            // Left side: natural stone floors under rock
            for (int z = 0; z < map.Size.z; z++)
            {
                for (int x = 0; x < splitX; x++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    ThingDef rockDef = GenStep_RocksFromGrid.RockDefAt(c);
                    if (rockDef == null) rockDef = GetFallbackRockDef();

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

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.building != null && def.building.isNaturalRock && def.building.mineableThing == null)
                {
                    cachedFallbackRock = def;
                    return def;
                }
            }
            return ThingDefOf.Granite;
        }

        public static void EnsurePlayerStartSpot(Map map)
        {
            if (!IsStartingBase(map)) return;

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

        public static void FinalizeMoatMap(Map map)
        {
            if (!IsStartingBase(map)) return;

            // Re-apply rocks, roofs, and terrain sanity
            EnforceRocksAndRoofs(map);
            CleanTerrain(map);
            EnsurePlayerStartSpot(map);

            // Mark as generated in GameComponent
            MoatGameComponent comp = Current.Game != null ? Current.Game.GetComponent<MoatGameComponent>() : null;
            if (comp != null)
            {
                comp.hasGeneratedStartingBase = true;
            }

            Log.Message("[The Moat] Starting base generation complete. The western 50% is protected by an impassable mountain cliff!");
        }
    }
}
