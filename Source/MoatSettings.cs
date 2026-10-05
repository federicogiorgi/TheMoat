using System;
using UnityEngine;
using Verse;

namespace TheMoat
{
    public class MoatSettings : ModSettings
    {
        public const float MinMountainPercent = 0.30f;
        public const float MaxMountainPercent = 0.70f;

        public float mountainPercent = 0.50f;
        public bool allowOresInMountain = true;
        public bool removeWaterOnPlains = true;
        public bool flattenHillsOnPlains = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mountainPercent, "mountainPercent", 0.50f);
            Scribe_Values.Look(ref allowOresInMountain, "allowOresInMountain", true);
            Scribe_Values.Look(ref removeWaterOnPlains, "removeWaterOnPlains", true);
            Scribe_Values.Look(ref flattenHillsOnPlains, "flattenHillsOnPlains", true);

            // Older versions allowed 10-90%
            mountainPercent = Mathf.Clamp(mountainPercent, MinMountainPercent, MaxMountainPercent);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label(string.Format("Mountain Width: {0:P0} of the map (West side)", mountainPercent));
            mountainPercent = listing.Slider(mountainPercent, MinMountainPercent, MaxMountainPercent);
            listing.Gap(12f);

            listing.CheckboxLabeled(
                "Allow ore veins in mountain",
                ref allowOresInMountain,
                "If enabled, mineable ore lumps (steel, components, gold, etc.) can spawn inside the mountain. If disabled, the mountain is 100% solid natural rock."
            );
            listing.Gap(8f);

            listing.CheckboxLabeled(
                "Remove lakes and water on the plains",
                ref removeWaterOnPlains,
                "If enabled, any lakes, ponds, or rivers on the eastern plains are converted into dry soil based on local fertility."
            );
            listing.Gap(8f);

            listing.CheckboxLabeled(
                "Flatten hills on the plains",
                ref flattenHillsOnPlains,
                "If enabled, elevation on the eastern plains is kept flat so no rock walls or hill slopes generate on the open side."
            );
            listing.Gap(16f);

            if (listing.ButtonText("Reset to Defaults"))
            {
                mountainPercent = 0.50f;
                allowOresInMountain = true;
                removeWaterOnPlains = true;
                flattenHillsOnPlains = true;
            }

            listing.End();
        }
    }
}
