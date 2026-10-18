using System;
using System.Collections.Generic;
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

    public enum MoatType
    {
        Off,
        Shallow,
        Deep
    }

    public enum MoatWater : byte
    {
        None,
        Shallow,
        Deep,
        // Dry land where the land bridge crosses the moat
        Bridge
    }

    // Where the mountain sits on the map. "Depth" is the distance from the map edge the mountain is attached to,
    // "along" the position along that edge. The mountain covers every cell shallower than edgeDepth[along].
    public struct MoatLayout
    {
        // At full roughness the foot of the mountain wanders up to this share of the map in and out
        public const float MaxRoughnessAmplitude = 0.15f;
        // Keep at least this much mountain and plains whatever the noise does
        private const int MinMountainDepth = 8;
        private const float MinPlainsShare = 0.15f;
        // DistanceFromMountain looks this far sideways, so a spur next door counts as close
        private const int NearEdgeRadius = 10;
        // Solid rock kept between caves and the plains / the map edges
        public const int CaveBufferFromPlains = 12;
        public const int CaveBufferFromMapEdge = 8;
        // Shallow crossings in a deep moat: this many cells wide, kept apart from each other, the land bridge
        // and the ends of the moat
        public const int FordWidth = 3;
        private const int FordSpacing = 12;

        public MoatSide side;
        public int thickness;
        public int sizeX;
        public int sizeZ;
        private int[] edgeDepth;
        private int[] nearEdgeDepth;
        private int[] caveDepth;
        // MoatWater per cell (index z * sizeX + x), null without a moat
        private byte[] moatGrid;

        // Per thread: Map Preview generates its previews off the main thread
        [ThreadStatic] private static Map cachedMap;
        [ThreadStatic] private static MoatSide cachedSide;
        [ThreadStatic] private static float cachedPercent;
        [ThreadStatic] private static float cachedRoughness;
        [ThreadStatic] private static MoatType cachedMoatType;
        [ThreadStatic] private static int cachedMoatWidth;
        [ThreadStatic] private static int cachedBridgeWidth;
        [ThreadStatic] private static int cachedFords;
        [ThreadStatic] private static MoatLayout cachedLayout;

        public static MoatLayout For(Map map)
        {
            MoatSettings settings = MoatMod.Settings;
            float percent = settings != null ? settings.mountainPercent : 0.50f;
            percent = Mathf.Clamp(percent, MoatSettings.MinMountainPercent, MoatSettings.MaxMountainPercent);
            float roughness = settings != null ? Mathf.Clamp01(settings.edgeRoughness) : 0f;
            MoatSide side = settings != null ? settings.mountainSide : MoatSide.West;
            MoatType moatType = settings != null ? settings.moatType : MoatType.Off;
            int moatWidth = settings != null ? Mathf.Clamp(settings.moatWidth, MoatSettings.MinMoatWidth, MoatSettings.MaxMoatWidth) : 0;
            int bridgeWidth = settings != null ? Mathf.Clamp(settings.landBridgeWidth, 0, MoatSettings.MaxLandBridgeWidth) : 0;
            int fords = settings != null ? Mathf.Clamp(settings.shallowFords, 0, MoatSettings.MaxShallowFords) : 0;

            // Scatterers ask for the layout once per candidate cell, so build it once per map
            if (cachedMap == map && cachedSide == side && cachedPercent == percent && cachedRoughness == roughness
                && cachedMoatType == moatType && cachedMoatWidth == moatWidth && cachedBridgeWidth == bridgeWidth && cachedFords == fords)
            {
                return cachedLayout;
            }

            MoatLayout layout = new MoatLayout();
            layout.side = side;
            layout.sizeX = map.Size.x;
            layout.sizeZ = map.Size.z;
            layout.thickness = (int)(layout.DepthAxisLength * percent);
            int seed = EdgeSeed(map);
            layout.BuildEdge(roughness, seed);
            int[] outerDepth = layout.BuildMoat(moatType, moatWidth, bridgeWidth, fords, seed);
            layout.BuildNearEdge(outerDepth);

            cachedMap = map;
            cachedSide = side;
            cachedPercent = percent;
            cachedRoughness = roughness;
            cachedMoatType = moatType;
            cachedMoatWidth = moatWidth;
            cachedBridgeWidth = bridgeWidth;
            cachedFords = fords;
            cachedLayout = layout;
            return layout;
        }

        // Same tile in the same world gives the same edge, so map previews match the real map
        private static int EdgeSeed(Map map)
        {
            int worldSeed = Find.World != null && Find.World.info != null ? Find.World.info.Seed : 0;
            return Gen.HashCombineInt(worldSeed, map.Tile.GetHashCode());
        }

        private void BuildEdge(float roughness, int seed)
        {
            int length = EdgeLength;
            edgeDepth = new int[length];
            nearEdgeDepth = new int[length];
            caveDepth = new int[length];

            float amplitude = roughness * MaxRoughnessAmplitude * DepthAxisLength;
            float[] profile = amplitude > 0f ? MoatEdgeNoise.Profile(length, seed) : null;
            int maxDepth = DepthAxisLength - Mathf.CeilToInt(DepthAxisLength * MinPlainsShare);

            for (int a = 0; a < length; a++)
            {
                int depth = profile != null ? Mathf.RoundToInt(thickness + amplitude * profile[a]) : thickness;
                edgeDepth[a] = Mathf.Clamp(depth, MinMountainDepth, maxDepth);
            }
        }

        // Water on the plains along the foot of the mountain: every plains cell within moatWidth cells (straight-line
        // distance) of the rock. Any path from the plains to the mountain has to cross it, also around spurs and
        // into bays. Returns how deep the mountain plus moat reaches at each position along the edge.
        private int[] BuildMoat(MoatType type, int moatWidth, int bridgeWidth, int fords, int seed)
        {
            int length = EdgeLength;
            int[] outerDepth = (int[])edgeDepth.Clone();
            if (type == MoatType.Off || moatWidth <= 0) return outerDepth;

            // Columns straight across the moat: the dry land bridge in the middle, and in a deep moat shallow fords
            bool[] bridge = new bool[length];
            int bridgeStart = length / 2 - bridgeWidth / 2;
            for (int a = Math.Max(0, bridgeStart); a < Math.Min(length, bridgeStart + bridgeWidth); a++)
            {
                bridge[a] = true;
            }
            bool[] ford = type == MoatType.Deep ? PlaceFords(length, fords, bridgeStart, bridgeWidth, seed) : new bool[length];

            moatGrid = new byte[sizeX * sizeZ];
            // Between cell centres: the w-th row off a straight cliff is w away, the diagonal next to a corner 1.41
            float reachSq = (moatWidth + 0.5f) * (moatWidth + 0.5f);
            // A deep moat keeps a one-cell shallow bank on the plains side, like vanilla lakes
            float deepSq = (moatWidth - 0.5f) * (moatWidth - 0.5f);

            for (int a = 0; a < length; a++)
            {
                int lo = Math.Max(0, a - moatWidth);
                int hi = Math.Min(length - 1, a + moatWidth);
                int maxDepth = 0;
                for (int b = lo; b <= hi; b++) maxDepth = Math.Max(maxDepth, edgeDepth[b] + moatWidth);
                maxDepth = Math.Min(maxDepth, DepthAxisLength - 1);

                for (int d = edgeDepth[a]; d <= maxDepth; d++)
                {
                    // Nearest rock in column b: the cell at this depth if the mountain reaches it, else its foot
                    int bestSq = int.MaxValue;
                    for (int b = lo; b <= hi; b++)
                    {
                        int dd = Math.Max(0, d - (edgeDepth[b] - 1));
                        bestSq = Math.Min(bestSq, dd * dd + (a - b) * (a - b));
                    }
                    if (bestSq > reachSq) continue;

                    outerDepth[a] = Math.Max(outerDepth[a], d + 1);

                    MoatWater water = bridge[a] ? MoatWater.Bridge
                        : type == MoatType.Deep && !ford[a] && bestSq <= deepSq ? MoatWater.Deep : MoatWater.Shallow;
                    IntVec3 c = CellAt(d, a);
                    moatGrid[c.z * sizeX + c.x] = (byte)water;
                }
            }
            return outerDepth;
        }

        // Random spots along the moat, the same for the same tile
        private static bool[] PlaceFords(int length, int count, int bridgeStart, int bridgeWidth, int seed)
        {
            bool[] ford = new bool[length];
            List<int> taken = new List<int>();
            System.Random rand = new System.Random(seed + 104729);
            if (length - FordSpacing - FordWidth + 1 <= FordSpacing) return ford;

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    int start = rand.Next(FordSpacing, length - FordSpacing - FordWidth + 1);
                    bool clear = bridgeWidth <= 0
                        || start + FordWidth + FordSpacing <= bridgeStart || start >= bridgeStart + bridgeWidth + FordSpacing;
                    foreach (int other in taken)
                    {
                        if (Math.Abs(start - other) < FordWidth + FordSpacing) clear = false;
                    }
                    if (!clear) continue;

                    taken.Add(start);
                    for (int a = start; a < start + FordWidth; a++) ford[a] = true;
                    break;
                }
            }
            return ford;
        }

        // outerDepth is the mountain plus its moat, so everything that keeps clear of the mountain keeps clear of the water too
        private void BuildNearEdge(int[] outerDepth)
        {
            int length = EdgeLength;
            for (int a = 0; a < length; a++)
            {
                int deepest = 0;
                for (int b = Math.Max(0, a - NearEdgeRadius); b <= Math.Min(length - 1, a + NearEdgeRadius); b++)
                {
                    deepest = Math.Max(deepest, outerDepth[b]);
                }
                nearEdgeDepth[a] = deepest;

                // Caves stop short of the shallowest edge nearby, so a bay to the side keeps its buffer too
                int shallowest = int.MaxValue;
                for (int b = Math.Max(0, a - CaveBufferFromPlains); b <= Math.Min(length - 1, a + CaveBufferFromPlains); b++)
                {
                    shallowest = Math.Min(shallowest, edgeDepth[b]);
                }
                caveDepth[a] = shallowest - CaveBufferFromPlains;
            }
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
            get
            {
                int count = 0;
                for (int a = 0; a < edgeDepth.Length; a++) count += edgeDepth[a];
                return count;
            }
        }

        // How deep the mountain reaches at this position along its edge
        public int MountainDepthAt(int along)
        {
            return edgeDepth[Mathf.Clamp(along, 0, edgeDepth.Length - 1)];
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

        public int Along(IntVec3 c)
        {
            return IsNorthOrSouth ? c.x : c.z;
        }

        public bool IsMountain(IntVec3 c)
        {
            return Depth(c) < MountainDepthAt(Along(c));
        }

        // Where caves may be dug: deep enough in the mountain and away from every map edge
        public bool IsCaveZone(IntVec3 c)
        {
            if (c.x < CaveBufferFromMapEdge || c.z < CaveBufferFromMapEdge
                || c.x >= sizeX - CaveBufferFromMapEdge || c.z >= sizeZ - CaveBufferFromMapEdge)
            {
                return false;
            }
            return Depth(c) < caveDepth[Mathf.Clamp(Along(c), 0, caveDepth.Length - 1)];
        }

        public bool HasMoat
        {
            get { return moatGrid != null; }
        }

        public MoatWater MoatAt(IntVec3 c)
        {
            if (moatGrid == null || c.x < 0 || c.z < 0 || c.x >= sizeX || c.z >= sizeZ) return MoatWater.None;
            return (MoatWater)moatGrid[c.z * sizeX + c.x];
        }

        // How far out on the plains c is from the foot of the mountain (or from the outer bank of the moat), counting
        // spurs a few cells to the side; zero or negative inside the mountain, the moat and in narrow bays
        public int DistanceFromMountain(IntVec3 c)
        {
            return Depth(c) - nearEdgeDepth[Mathf.Clamp(Along(c), 0, nearEdgeDepth.Length - 1)];
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

    // 1D fractal noise for the foot of the mountain, stretched to fill [-1, 1]. Plain C# with its own random numbers,
    // so it does not touch the map generator's Rand sequence.
    public static class MoatEdgeNoise
    {
        private const float BaseWavelength = 80f;
        private const int Octaves = 6;          // down to bumps a few cells wide
        private const float Persistence = 0.6f;

        public static float[] Profile(int length, int seed)
        {
            float[] result = new float[length];
            float wavelength = BaseWavelength;
            float amplitude = 1f;
            float totalAmplitude = 0f;

            for (int o = 0; o < Octaves; o++)
            {
                System.Random rand = new System.Random(seed + o * 7919);
                int points = (int)(length / wavelength) + 2;
                float[] lattice = new float[points];
                for (int i = 0; i < points; i++)
                {
                    lattice[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
                }
                float shift = (float)rand.NextDouble() * wavelength;

                for (int a = 0; a < length; a++)
                {
                    float pos = (a + shift) / wavelength;
                    int i0 = Math.Min((int)pos, points - 2);
                    float t = pos - i0;
                    t = t * t * (3f - 2f * t);
                    result[a] += amplitude * (lattice[i0] + (lattice[i0 + 1] - lattice[i0]) * t);
                }

                totalAmplitude += amplitude;
                amplitude *= Persistence;
                wavelength /= 2f;
            }

            // Stretch so the deepest bay or longest spur reaches the full amplitude
            float peak = 0f;
            for (int a = 0; a < length; a++)
            {
                peak = Math.Max(peak, Math.Abs(result[a]));
            }
            if (peak <= 0f) peak = totalAmplitude;
            for (int a = 0; a < length; a++)
            {
                result[a] /= peak;
            }
            return result;
        }
    }
}
