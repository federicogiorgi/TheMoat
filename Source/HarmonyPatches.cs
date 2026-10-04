using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TheMoat
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        // 1. Elevation & Caves Grid Postfix
        [HarmonyPatch(typeof(GenStep_ElevationFertility), "Generate")]
        public static class Patch_ElevationFertility_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnforceElevationAndCaves(map);
            }
        }

        // 2. Prevent Cave Generation
        [HarmonyPatch(typeof(MapGenCavesUtility), "GenerateCaves")]
        public static class Patch_MapGenCavesUtility_GenerateCaves
        {
            public static bool Prefix(Map map)
            {
                if (MoatManager.IsStartingBase(map))
                {
                    return false;
                }
                return true;
            }
        }

        // 3. Prevent Cave Hives
        [HarmonyPatch(typeof(GenStep_CaveHives), "Generate")]
        public static class Patch_CaveHives_Generate
        {
            public static bool Prefix(Map map, GenStepParams parms)
            {
                if (MoatManager.IsStartingBase(map))
                {
                    return false;
                }
                return true;
            }
        }

        // 4. Rock Walls & Overhead Mountain Roofs
        [HarmonyPatch(typeof(GenStep_RocksFromGrid), "Generate")]
        public static class Patch_RocksFromGrid_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnforceRocksAndRoofs(map);
            }
        }

        // 5. Natural Terrain & Water Removal
        [HarmonyPatch(typeof(GenStep_Terrain), "Generate")]
        public static class Patch_Terrain_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.CleanTerrain(map);
            }
        }

        // 6. Restrict Special Features (Shrines, Monolith, Geysers, Ruins) from mountain
        [HarmonyPatch(typeof(GenStep_Scatterer), "CanScatterAt")]
        public static class Patch_Scatterer_CanScatterAt
        {
            public static bool Prefix(GenStep_Scatterer __instance, IntVec3 loc, Map map, ref bool __result)
            {
                if (MoatManager.IsStartingBase(map))
                {
                    int splitX = MoatManager.GetSplitX(map);

                    // If it's mineral ore veins
                    if (__instance is GenStep_ScatterLumpsMineable)
                    {
                        if (!MoatMod.Settings.allowOresInMountain && loc.x < splitX)
                        {
                            __result = false;
                            return false;
                        }
                        return true;
                    }

                    // Special features: strictly prohibited on the western mountain half
                    int margin = 6;
                    if (__instance is GenStep_ScatterShrines)
                    {
                        margin = 14; // Ancient Dangers are large buildings
                    }

                    if (loc.x < splitX + margin)
                    {
                        __result = false;
                        return false;
                    }
                }
                return true;
            }
        }

        // 7. Player Start Spot
        [HarmonyPatch(typeof(GenStep_FindPlayerStartSpot), "Generate")]
        public static class Patch_FindPlayerStartSpot_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnsurePlayerStartSpot(map);
            }
        }

        // 8. Road Exit Point Redirection
        [HarmonyPatch(typeof(GenStep_Roads), "FindRoadExitCell")]
        public static class Patch_Roads_FindRoadExitCell
        {
            public static void Postfix(Map map, ref IntVec3 __result)
            {
                if (MoatManager.IsStartingBase(map))
                {
                    int splitX = MoatManager.GetSplitX(map);
                    if (__result.x < splitX)
                    {
                        IntVec3 newExit;
                        bool found = CellFinder.TryFindRandomEdgeCellWith(
                            delegate(IntVec3 c)
                            {
                                return c.x >= splitX && c.Standable(map);
                            },
                            map,
                            CellFinder.EdgeRoadChance_Always,
                            out newExit
                        );
                        if (found && newExit.IsValid)
                        {
                            __result = newExit;
                        }
                    }
                }
            }
        }

        // 9. Road Bulldoze Protection for Mountain
        [HarmonyPatch(typeof(GenStep_Roads), "Generate")]
        public static class Patch_Roads_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                if (MoatManager.IsStartingBase(map))
                {
                    MoatManager.EnforceRocksAndRoofs(map);
                }
            }
        }

        // 10. Map Generation Finalizer
        [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
        public static class Patch_MapGenerator_GenerateMap
        {
            public static void Postfix(Map __result)
            {
                if (__result != null && MoatManager.IsStartingBase(__result))
                {
                    MoatManager.FinalizeMoatMap(__result);
                }
            }
        }
    }
}
