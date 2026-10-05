using RimWorld;
using Verse;

namespace TheMoat
{
    // Exposes vanilla lump placement (ore choice by commonality, lump size and shape) for single lumps
    public class MoatOreScatterer : GenStep_ScatterLumpsMineable
    {
        public void ScatterLump(IntVec3 c, Map map, GenStepParams parms)
        {
            ScatterAt(c, map, parms, 1);
        }
    }
}
