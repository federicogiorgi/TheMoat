using UnityEngine;
using Verse;

namespace TheMoat
{
    public enum MoatSide
    {
        West,
        East,
        North,
        South
    }

    // Where the mountain sits on the map. "Depth" is the distance from the map edge the mountain is attached to.
    public struct MoatLayout
    {
        public MoatSide side;
        public int thickness;
        public int sizeX;
        public int sizeZ;

        public static MoatLayout For(Map map)
        {
            MoatSettings settings = MoatMod.Settings;
            float percent = settings != null ? settings.mountainPercent : 0.50f;
            percent = Mathf.Clamp(percent, MoatSettings.MinMountainPercent, MoatSettings.MaxMountainPercent);

            MoatLayout layout = new MoatLayout();
            layout.side = settings != null ? settings.mountainSide : MoatSide.West;
            layout.sizeX = map.Size.x;
            layout.sizeZ = map.Size.z;
            layout.thickness = (int)(layout.DepthAxisLength * percent);
            return layout;
        }

        public bool IsNorthOrSouth
        {
            get { return side == MoatSide.North || side == MoatSide.South; }
        }

        public int DepthAxisLength
        {
            get { return IsNorthOrSouth ? sizeZ : sizeX; }
        }

        public int EdgeLength
        {
            get { return IsNorthOrSouth ? sizeX : sizeZ; }
        }

        public int MountainCellCount
        {
            get { return thickness * EdgeLength; }
        }

        public int Depth(IntVec3 c)
        {
            switch (side)
            {
                case MoatSide.East: return sizeX - 1 - c.x;
                case MoatSide.North: return sizeZ - 1 - c.z;
                case MoatSide.South: return c.z;
                default: return c.x;
            }
        }

        public bool IsMountain(IntVec3 c)
        {
            return Depth(c) < thickness;
        }

        // How far out on the plains c is from the foot of the mountain; negative inside the mountain
        public int DistanceFromMountain(IntVec3 c)
        {
            return Depth(c) - thickness;
        }

        // The cell at the given depth and position along the mountain's map edge
        public IntVec3 CellAt(int depth, int along)
        {
            switch (side)
            {
                case MoatSide.East: return new IntVec3(sizeX - 1 - depth, 0, along);
                case MoatSide.North: return new IntVec3(along, 0, sizeZ - 1 - depth);
                case MoatSide.South: return new IntVec3(along, 0, depth);
                default: return new IntVec3(depth, 0, along);
            }
        }
    }
}
