using System;
using UnityEngine;
using Verse;

namespace TheMoat
{
    public class MoatSettings : ModSettings
    {
        public const float MinMountainPercent = 0.30f;
        public const float MaxMountainPercent = 0.70f;
        public const int MinMoatWidth = 2;
        public const int MaxMoatWidth = 15;
        public const int MaxLandBridgeWidth = 40;
        public const int MaxShallowFords = 6;
        public const float MinIslandPercent = 0.20f;
        public const float MaxIslandPercent = 0.70f;

        public MoatSide mountainSide = MoatSide.West;
        public float mountainPercent = 0.50f;
        public float edgeRoughness = 0f;
        public bool allowOresInMountain = true;
        public bool cavesInMountain = false;
        public bool removeWaterOnPlains = true;
        public bool flattenHillsOnPlains = true;
        public MoatType moatType = MoatType.Off;
        public int moatWidth = 5;
        public int landBridgeWidth = 4;
        public int shallowFords = 2;
        public MoatShape mapShape = MoatShape.MountainSide;
        public IslandTerrain islandTerrain = IslandTerrain.Mountain;
        public float islandPercent = 0.40f;
        public int islandWaterWidth = 6;
        public int islandPassageWidth = 3;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mountainSide, "mountainSide", MoatSide.West);
            Scribe_Values.Look(ref mountainPercent, "mountainPercent", 0.50f);
            Scribe_Values.Look(ref edgeRoughness, "edgeRoughness", 0f);
            Scribe_Values.Look(ref allowOresInMountain, "allowOresInMountain", true);
            Scribe_Values.Look(ref cavesInMountain, "cavesInMountain", false);
            Scribe_Values.Look(ref removeWaterOnPlains, "removeWaterOnPlains", true);
            Scribe_Values.Look(ref flattenHillsOnPlains, "flattenHillsOnPlains", true);
            Scribe_Values.Look(ref moatType, "moatType", MoatType.Off);
            Scribe_Values.Look(ref moatWidth, "moatWidth", 5);
            Scribe_Values.Look(ref landBridgeWidth, "landBridgeWidth", 4);
            Scribe_Values.Look(ref shallowFords, "shallowFords", 2);
            Scribe_Values.Look(ref mapShape, "mapShape", MoatShape.MountainSide);
            Scribe_Values.Look(ref islandTerrain, "islandTerrain", IslandTerrain.Mountain);
            Scribe_Values.Look(ref islandPercent, "islandPercent", 0.40f);
            Scribe_Values.Look(ref islandWaterWidth, "islandWaterWidth", 6);
            Scribe_Values.Look(ref islandPassageWidth, "islandPassageWidth", 3);

            // Older versions allowed 10-90%
            mountainPercent = Mathf.Clamp(mountainPercent, MinMountainPercent, MaxMountainPercent);
            edgeRoughness = Mathf.Clamp01(edgeRoughness);
            moatWidth = Mathf.Clamp(moatWidth, MinMoatWidth, MaxMoatWidth);
            landBridgeWidth = Mathf.Clamp(landBridgeWidth, 0, MaxLandBridgeWidth);
            shallowFords = Mathf.Clamp(shallowFords, 0, MaxShallowFords);
            islandPercent = Mathf.Clamp(islandPercent, MinIslandPercent, MaxIslandPercent);
            islandWaterWidth = Mathf.Clamp(islandWaterWidth, MinMoatWidth, MaxMoatWidth);
            islandPassageWidth = Mathf.Clamp(islandPassageWidth, 0, MaxLandBridgeWidth);
        }

        public void DoWindowContents(Rect inRect)
        {
            // Two columns: the map layout and the mountain or island on the left, the water on the right
            Listing_Standard listing = new Listing_Standard();
            listing.ColumnWidth = (inRect.width - 34f) / 2f;
            listing.Begin(inRect);

            listing.Label("TheMoat_MapShape".Translate(), -1f, "TheMoat_MapShapeDesc".Translate());
            foreach (MoatShape shape in Enum.GetValues(typeof(MoatShape)))
            {
                if (listing.RadioButton(("TheMoat_Shape" + shape).Translate(), mapShape == shape, 12f))
                {
                    mapShape = shape;
                }
            }
            listing.Gap(12f);

            bool island = mapShape == MoatShape.Island;
            listing.Label((island ? "TheMoat_PassageSide" : "TheMoat_MountainSide").Translate());
            foreach (MoatSide side in Enum.GetValues(typeof(MoatSide)))
            {
                if (listing.RadioButton(("TheMoat_Side" + side).Translate(), mountainSide == side, 12f))
                {
                    mountainSide = side;
                }
            }
            listing.Gap(12f);

            if (island)
            {
                listing.Label("TheMoat_IslandSize".Translate(islandPercent.ToString("P0")), -1f, "TheMoat_IslandSizeDesc".Translate());
                islandPercent = listing.Slider(islandPercent, MinIslandPercent, MaxIslandPercent);
            }
            else
            {
                listing.Label("TheMoat_MountainSize".Translate(mountainPercent.ToString("P0")));
                mountainPercent = listing.Slider(mountainPercent, MinMountainPercent, MaxMountainPercent);
            }
            listing.Gap(12f);

            listing.Label("TheMoat_EdgeRoughness".Translate(edgeRoughness.ToString("P0")), -1f,
                (island ? "TheMoat_ShoreRoughnessDesc" : "TheMoat_EdgeRoughnessDesc").Translate());
            edgeRoughness = listing.Slider(edgeRoughness, 0f, 1f);
            listing.Gap(12f);

            // An open island has no mountain for ores and caves
            if (!island || islandTerrain == IslandTerrain.Mountain)
            {
                listing.CheckboxLabeled("TheMoat_AllowOres".Translate(), ref allowOresInMountain, "TheMoat_AllowOresDesc".Translate());
                listing.Gap(8f);

                listing.CheckboxLabeled("TheMoat_Caves".Translate(), ref cavesInMountain,
                    "TheMoat_CavesDesc".Translate(MoatLayout.CaveBufferFromPlains, MoatLayout.CaveBufferFromMapEdge));
                listing.Gap(8f);
            }

            listing.CheckboxLabeled("TheMoat_RemoveWater".Translate(), ref removeWaterOnPlains, "TheMoat_RemoveWaterDesc".Translate());
            listing.Gap(8f);

            listing.CheckboxLabeled("TheMoat_FlattenHills".Translate(), ref flattenHillsOnPlains, "TheMoat_FlattenHillsDesc".Translate());

            listing.NewColumn();

            if (island)
            {
                listing.Label("TheMoat_IslandTerrain".Translate(), -1f, "TheMoat_IslandTerrainDesc".Translate());
                foreach (IslandTerrain terrain in Enum.GetValues(typeof(IslandTerrain)))
                {
                    if (listing.RadioButton(("TheMoat_Island" + terrain).Translate(), islandTerrain == terrain, 12f))
                    {
                        islandTerrain = terrain;
                    }
                }
                listing.Gap(12f);

                listing.Label("TheMoat_IslandWater".Translate(islandWaterWidth), -1f, "TheMoat_IslandWaterDesc".Translate());
                islandWaterWidth = Mathf.RoundToInt(listing.Slider(islandWaterWidth, MinMoatWidth, MaxMoatWidth));
                listing.Gap(12f);

                listing.Label("TheMoat_IslandPassage".Translate(islandPassageWidth), -1f, "TheMoat_IslandPassageDesc".Translate());
                islandPassageWidth = Mathf.RoundToInt(listing.Slider(islandPassageWidth, 0, MaxLandBridgeWidth));
                listing.Gap(12f);
            }
            else
            {
                listing.Label("TheMoat_MoatType".Translate(), -1f, "TheMoat_MoatTypeDesc".Translate());
                foreach (MoatType type in Enum.GetValues(typeof(MoatType)))
                {
                    if (listing.RadioButton(("TheMoat_Moat" + type).Translate(), moatType == type, 12f))
                    {
                        moatType = type;
                    }
                }
                listing.Gap(12f);
            }

            if (!island && moatType != MoatType.Off)
            {
                listing.Label("TheMoat_MoatWidth".Translate(moatWidth), -1f, "TheMoat_MoatWidthDesc".Translate());
                moatWidth = Mathf.RoundToInt(listing.Slider(moatWidth, MinMoatWidth, MaxMoatWidth));
                listing.Gap(12f);

                listing.Label("TheMoat_LandBridge".Translate(landBridgeWidth), -1f, "TheMoat_LandBridgeDesc".Translate());
                landBridgeWidth = Mathf.RoundToInt(listing.Slider(landBridgeWidth, 0, MaxLandBridgeWidth));
                listing.Gap(12f);

                if (moatType == MoatType.Deep)
                {
                    listing.Label("TheMoat_ShallowFords".Translate(shallowFords), -1f, "TheMoat_ShallowFordsDesc".Translate(MoatLayout.FordWidth));
                    shallowFords = Mathf.RoundToInt(listing.Slider(shallowFords, 0, MaxShallowFords));
                    listing.Gap(12f);
                }
            }
            listing.Gap(16f);

            if (listing.ButtonText("TheMoat_ResetDefaults".Translate()))
            {
                mountainSide = MoatSide.West;
                mountainPercent = 0.50f;
                edgeRoughness = 0f;
                allowOresInMountain = true;
                cavesInMountain = false;
                removeWaterOnPlains = true;
                flattenHillsOnPlains = true;
                moatType = MoatType.Off;
                moatWidth = 5;
                landBridgeWidth = 4;
                shallowFords = 2;
                mapShape = MoatShape.MountainSide;
                islandTerrain = IslandTerrain.Mountain;
                islandPercent = 0.40f;
                islandWaterWidth = 6;
                islandPassageWidth = 3;
            }

            listing.End();
        }
    }
}
