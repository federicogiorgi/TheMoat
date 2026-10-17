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

            listing.Label("Mountain side:");
            foreach (MoatSide side in Enum.GetValues(typeof(MoatSide)))
            {
                if (listing.RadioButton(SideLabel(side), mountainSide == side, 12f))
                {
                    mountainSide = side;
                }
            }
            listing.Gap(12f);

            listing.Label(string.Format("Mountain size: {0:P0} of the map", mountainPercent));
            mountainPercent = listing.Slider(mountainPercent, MinMountainPercent, MaxMountainPercent);
            listing.Gap(12f);

            listing.Label(
                string.Format("Edge roughness: {0:P0}", edgeRoughness),
                -1f,
                (TipSignal)"How irregular the foot of the mountain is. At 0% it is a straight cliff line; higher values add bays and spurs, up to about 15% of the map deep. The mountain stays solid either way."
            );
            edgeRoughness = listing.Slider(edgeRoughness, 0f, 1f);
            listing.Gap(12f);

            listing.CheckboxLabeled(
                "Allow ore veins in mountain",
                ref allowOresInMountain,
                "If enabled, mineable ore lumps (steel, components, gold, etc.) can spawn inside the mountain. If disabled, the mountain is 100% solid natural rock."
            );
            listing.Gap(8f);

            listing.CheckboxLabeled(
                "Caves inside the mountain",
                ref cavesInMountain,
                "If enabled, natural tunnels like vanilla caves are dug in the inner mountain. At least "
                    + MoatLayout.CaveBufferFromPlains + " cells of solid rock separate them from the plains and "
                    + MoatLayout.CaveBufferFromMapEdge + " from the map edges, so a cave never opens a second way into your base. No insect hives spawn in them."
            );
            listing.Gap(8f);

            listing.CheckboxLabeled(
                "Remove lakes and water on the plains",
                ref removeWaterOnPlains,
                "If enabled, any lakes, ponds, or rivers on the plains are converted into dry soil based on local fertility."
            );
            listing.Gap(8f);

            listing.CheckboxLabeled(
                "Flatten hills on the plains",
                ref flattenHillsOnPlains,
                "If enabled, elevation on the plains is kept flat so no rock walls or hill slopes generate on the open side."
            );
            listing.Gap(16f);

            if (listing.ButtonText("Reset to Defaults"))
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

        private static string SideLabel(MoatSide side)
        {
            switch (side)
            {
                case MoatSide.East: return "East (right)";
                case MoatSide.North: return "North (top)";
                case MoatSide.South: return "South (bottom)";
                default: return "West (left)";
            }
        }
    }
}
