using System;
using Verse;

namespace TheMoat
{
    public class MoatGameComponent : GameComponent
    {
        public bool hasGeneratedStartingBase = false;

        public MoatGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hasGeneratedStartingBase, "hasGeneratedStartingBase", false);
        }
    }
}
