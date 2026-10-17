using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TheMoat
{
    public class MoatMod : Mod
    {
        public static MoatSettings Settings;

        public MoatMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MoatSettings>();

            try
            {
                Harmony harmony = new Harmony("Keldarton.TheMoat");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Message("[The Moat] Loaded. Mountain on the " + Settings.mountainSide.ToString().ToLowerInvariant() + " side, "
                    + Settings.mountainPercent.ToString("P0") + " of the map.");
            }
            catch (Exception ex)
            {
                Log.Error("[The Moat] Error during Harmony initialization: " + ex);
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "The Moat";
        }
    }
}
