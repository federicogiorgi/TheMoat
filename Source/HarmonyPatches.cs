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
                try
                {
                    MoatManager.EnforceElevationAndCaves(map);
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_ElevationFertility_Generate: " + ex);
                }
            }
        }

        // 2. Prevent Cave Generation
        [HarmonyPatch(typeof(MapGenCavesUtility), "GenerateCaves")]
        public static class Patch_MapGenCavesUtility_GenerateCaves
        {
            public static bool Prefix(Map map)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_MapGenCavesUtility_GenerateCaves: " + ex);
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
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_CaveHives_Generate: " + ex);
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
                try
                {
                    MoatManager.EnforceRocksAndRoofs(map);
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_RocksFromGrid_Generate: " + ex);
                }
            }
        }

        // 5. Natural Terrain & Water Removal
        [HarmonyPatch(typeof(GenStep_Terrain), "Generate")]
        public static class Patch_Terrain_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                try
                {
                    MoatManager.CleanTerrain(map);
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_Terrain_Generate: " + ex);
                }
            }
        }

        // 6. Restrict General Scatterers (Monolith, Geysers, Ruins) to plains
        [HarmonyPatch(typeof(GenStep_Scatterer), "CanScatterAt")]
        public static class Patch_Scatterer_CanScatterAt
        {
            public static bool Prefix(GenStep_Scatterer __instance, IntVec3 loc, Map map, ref bool __result)
            {
                try
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

                        // Handled by dedicated shrine patch
                        if (__instance is GenStep_ScatterShrines)
                        {
                            return true;
                        }

                        // Special features: strictly prohibited on the western mountain half
                        if (loc.x < splitX + 6)
                        {
                            __result = false;
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_Scatterer_CanScatterAt: " + ex);
                }
                return true;
            }
        }

        // 7. Ancient Dangers: allow placement on flat open plains without requiring natural rock
        [HarmonyPatch(typeof(GenStep_ScatterShrines), "CanScatterAt")]
        public static class Patch_ScatterShrines_CanScatterAt
        {
            public static bool Prefix(GenStep_ScatterShrines __instance, IntVec3 loc, Map map, ref bool __result)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        int splitX = MoatManager.GetSplitX(map);

                        // Shrines must spawn on the right plains
                        if (loc.x < splitX + 8 || loc.x > map.Size.x - 12 || loc.z < 12 || loc.z > map.Size.z - 12)
                        {
                            __result = false;
                            return false;
                        }

                        // Must be standable and unroofed
                        if (!loc.Standable(map) || map.roofGrid.Roofed(loc))
                        {
                            __result = false;
                            return false;
                        }

                        // Bypass the vanilla requirement of needing adjacent natural rock
                        __result = true;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_ScatterShrines_CanScatterAt: " + ex);
                }
                return true;
            }
        }

        // 8. Player Start Spot
        [HarmonyPatch(typeof(GenStep_FindPlayerStartSpot), "Generate")]
        public static class Patch_FindPlayerStartSpot_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                try
                {
                    MoatManager.EnsurePlayerStartSpot(map);
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_FindPlayerStartSpot_Generate: " + ex);
                }
            }
        }

        // 9. Road Exit Point Redirection
        [HarmonyPatch(typeof(GenStep_Roads), "FindRoadExitCell")]
        public static class Patch_Roads_FindRoadExitCell
        {
            public static void Postfix(Map map, ref IntVec3 __result)
            {
                try
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
                catch (Exception ex)
                {
                    Log.Warning("[The Moat] Error in Patch_Roads_FindRoadExitCell: " + ex);
                }
            }
        }

        // 10. Road Bulldoze Protection for Mountain
        [HarmonyPatch(typeof(GenStep_Roads), "Generate")]
        public static class Patch_Roads_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        MoatManager.EnforceRocksAndRoofs(map);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_Roads_Generate: " + ex);
                }
            }
        }

        // 11. Map Generation Finalizer
        [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
        public static class Patch_MapGenerator_GenerateMap
        {
            public static void Postfix(Map __result)
            {
                try
                {
                    if (__result != null && MoatManager.IsStartingBase(__result))
                    {
                        MoatManager.FinalizeMoatMap(__result);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_MapGenerator_GenerateMap: " + ex);
                }
            }
        }
    }
}
