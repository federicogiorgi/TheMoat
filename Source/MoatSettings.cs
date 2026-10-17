using System;
using UnityEngine;
using Verse;

namespace TheMoat
{
    public class MoatSettings : ModSettings
    {
        public const float MinMountainPercent = 0.30f;
        public const float MaxMountainPercent = 0.70f;

        public MoatSide mountainSide = MoatSide.West;
        public float mountainPercent = 0.50f;
        public float edgeRoughness = 0f;
        public bool allowOresInMountain = true;
        public bool cavesInMountain = false;
        public bool removeWaterOnPlains = true;
        public bool flattenHillsOnPlains = true;

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

            // Older versions allowed 10-90%
            mountainPercent = Mathf.Clamp(mountainPercent, MinMountainPercent, MaxMountainPercent);
            edgeRoughness = Mathf.Clamp01(edgeRoughness);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("TheMoat_MountainSide".Translate());
            foreach (MoatSide side in Enum.GetValues(typeof(MoatSide)))
            {
                if (listing.RadioButton(("TheMoat_Side" + side).Translate(), mountainSide == side, 12f))
                {
                    mountainSide = side;
                }
            }
            listing.Gap(12f);

            listing.Label("TheMoat_MountainSize".Translate(mountainPercent.ToString("P0")));
            mountainPercent = listing.Slider(mountainPercent, MinMountainPercent, MaxMountainPercent);
            listing.Gap(12f);

            listing.Label("TheMoat_EdgeRoughness".Translate(edgeRoughness.ToString("P0")), -1f, "TheMoat_EdgeRoughnessDesc".Translate());
            edgeRoughness = listing.Slider(edgeRoughness, 0f, 1f);
            listing.Gap(12f);

            listing.CheckboxLabeled("TheMoat_AllowOres".Translate(), ref allowOresInMountain, "TheMoat_AllowOresDesc".Translate());
            listing.Gap(8f);

            listing.CheckboxLabeled("TheMoat_Caves".Translate(), ref cavesInMountain,
                "TheMoat_CavesDesc".Translate(MoatLayout.CaveBufferFromPlains, MoatLayout.CaveBufferFromMapEdge));
            listing.Gap(8f);

            listing.CheckboxLabeled("TheMoat_RemoveWater".Translate(), ref removeWaterOnPlains, "TheMoat_RemoveWaterDesc".Translate());
            listing.Gap(8f);

            listing.CheckboxLabeled("TheMoat_FlattenHills".Translate(), ref flattenHillsOnPlains, "TheMoat_FlattenHillsDesc".Translate());
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
            }

            listing.End();
        }
    }
}
