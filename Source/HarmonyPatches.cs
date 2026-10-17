using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TheMoat
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        // Mountain and plains elevation, no caves
        [HarmonyPatch(typeof(GenStep_ElevationFertility), "Generate")]
        public static class Patch_ElevationFertility_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnforceElevationAndCaves(map);
            }
        }

        // Tile mutators (mountain, cliffs, coast, valley...) rewrite the elevation grid in a later step, which
        // would put a second mountain on the plains and dig into ours: enforce the layout again after them
        [HarmonyPatch(typeof(GenStep_MutatorPostElevationFertility), "Generate")]
        public static class Patch_MutatorPostElevationFertility_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnforceElevationAndCaves(map);
                MoatManager.GenerateMountainCaves(map);
            }
        }

        // No vanilla caves on the starting map; the optional mountain caves come from MoatManager.GenerateMountainCaves
        [HarmonyPatch(typeof(MapGenCavesUtility), "GenerateCaves")]
        public static class Patch_MapGenCavesUtility_GenerateCaves
        {
            public static bool Prefix(Map map)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map) && !MoatManager.diggingMoatCaves)
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

        [HarmonyPatch(typeof(GenStep_RocksFromGrid), "Generate")]
        public static class Patch_RocksFromGrid_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                // Ores first, so lumps poking past the split line get cleared by the enforcement pass
                MoatManager.TopUpMountainOres(map, parms);
                MoatManager.EnforceRocksAndRoofs(map);
            }
        }

        [HarmonyPatch(typeof(GenStep_Terrain), "Generate")]
        public static class Patch_Terrain_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.CleanTerrain(map);
            }
        }

        // Tile mutators lay their water (coast, lakes...) after the terrain step: dry the plains again after them
        [HarmonyPatch(typeof(GenStep_MutatorPostTerrain), "Generate")]
        public static class Patch_MutatorPostTerrain_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.CleanTerrain(map);
            }
        }

        // Keep scattered features (monolith, geysers, ruins...) on the plains
        [HarmonyPatch(typeof(GenStep_Scatterer), "CanScatterAt")]
        public static class Patch_Scatterer_CanScatterAt
        {
            public static bool Prefix(GenStep_Scatterer __instance, IntVec3 loc, Map map, ref bool __result)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        MoatLayout layout = MoatLayout.For(map);

                        if (__instance is GenStep_ScatterLumpsMineable)
                        {
                            if (!MoatMod.Settings.allowOresInMountain && layout.IsMountain(loc))
                            {
                                __result = false;
                                return false;
                            }
                            return true;
                        }

                        // See the shrine patch below
                        if (__instance is GenStep_ScatterShrines)
                        {
                            return true;
                        }

                        if (layout.DistanceFromMountain(loc) < 6)
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

        // Ancient dangers normally need natural rock next to them, which the plains don't have
        [HarmonyPatch(typeof(GenStep_ScatterShrines), "CanScatterAt")]
        public static class Patch_ScatterShrines_CanScatterAt
        {
            public static bool Prefix(GenStep_ScatterShrines __instance, IntVec3 c, Map map, ref bool __result)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        MoatLayout layout = MoatLayout.For(map);

                        if (layout.DistanceFromMountain(c) < 8 || c.x < 12 || c.x > map.Size.x - 12 || c.z < 12 || c.z > map.Size.z - 12)
                        {
                            __result = false;
                            return false;
                        }

                        if (!c.Standable(map) || map.roofGrid.Roofed(c))
                        {
                            __result = false;
                            return false;
                        }

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

        // Ancient mech remains (Ideology): find a spot on the plains, see MoatManager.TryFindAncientMechsCell
        [HarmonyPatch(typeof(GenStep_Scatterer), "TryFindScatterCell")]
        public static class Patch_Scatterer_TryFindScatterCell
        {
            public static bool Prefix(GenStep_Scatterer __instance, Map map, ref IntVec3 result, ref bool __result)
            {
                try
                {
                    if (__instance is GenStep_ScatterAncientMechs && MoatManager.IsStartingBase(map))
                    {
                        IntVec3 cell;
                        if (MoatManager.TryFindAncientMechsCell(__instance, map, out cell))
                        {
                            result = cell;
                            __result = true;
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[The Moat] Error in Patch_Scatterer_TryFindScatterCell: " + ex);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GenStep_FindPlayerStartSpot), "Generate")]
        public static class Patch_FindPlayerStartSpot_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnsurePlayerStartSpot(map);
            }
        }

        // Roads must leave the map through the plains
        [HarmonyPatch(typeof(GenStep_Roads), "FindRoadExitCell")]
        public static class Patch_Roads_FindRoadExitCell
        {
            public static void Postfix(Map map, ref IntVec3 __result)
            {
                try
                {
                    if (MoatManager.IsStartingBase(map))
                    {
                        MoatLayout layout = MoatLayout.For(map);
                        if (layout.IsMountain(__result))
                        {
                            IntVec3 newExit;
                            bool found = CellFinder.TryFindRandomEdgeCellWith(
                                delegate(IntVec3 c)
                                {
                                    return !layout.IsMountain(c) && c.Standable(map);
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

        // Roads clear rock along their path, so restore the mountain afterwards
        [HarmonyPatch(typeof(GenStep_Roads), "Generate")]
        public static class Patch_Roads_Generate
        {
            public static void Postfix(Map map, GenStepParams parms)
            {
                MoatManager.EnforceRocksAndRoofs(map);
            }
        }

        [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
        public static class Patch_MapGenerator_GenerateMap
        {
            public static void Postfix(Map __result)
            {
                MoatManager.FinalizeMoatMap(__result);
            }
        }
    }
}
