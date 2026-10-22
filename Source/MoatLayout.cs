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

    public enum MoatShape
    {
        MountainSide,
        Island
    }

    public enum IslandTerrain
    {
        Mountain,
        Open
    }

    public enum MoatWater : byte
    {
        None,
        Shallow,
        Deep,
        // Dry land where the land bridge crosses the moat, or the way in crosses an island's water
        Bridge
    }

    // Where the mountain sits on the map. "Depth" is the distance from the map edge the mountain is attached to,
    // "along" the position along that edge. The mountain covers every cell shallower than edgeDepth[along].
    // On an island map the island sits in the middle instead (islandGrid) and side is where its way in faces.
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
        // At full roughness the island's shore wanders up to this share of its radius in and out
        private const float IslandRoughnessAmplitude = 0.35f;
        private const int MinIslandRadius = 8;
        // Open land kept between the island's water and the map edges, for raiders to walk round and roads to leave by
        private const int IslandMapEdgeMargin = 12;
        // A mountain island's colony lands this far out from the water, in front of the way in
        private const int IslandStartDistance = 20;

        public MoatSide side;
        public int thickness;
        public int sizeX;
        public int sizeZ;
        private int[] edgeDepth;
        private int[] nearEdgeDepth;
        private int[] caveDepth;
        // MoatWater per cell (index z * sizeX + x), null without a moat
        private byte[] moatGrid;
        // Island maps only, per cell: on the island; steps (8-way) to the nearest cell off the island;
        // steps from the nearest water or mountain cell, minus one
        private bool[] islandGrid;
        private int[] insideDepth;
        private int[] openDistance;
        public bool islandIsMountain;
        // Island maps only: where the colony should land
        public IntVec3 startTarget;
        private int passageStart;
        private int passageWidth;

        // Per thread: Map Preview generates its previews off the main thread
        [ThreadStatic] private static Map cachedMap;
        [ThreadStatic] private static MoatSide cachedSide;
        [ThreadStatic] private static float cachedPercent;
        [ThreadStatic] private static float cachedRoughness;
        [ThreadStatic] private static MoatType cachedMoatType;
        [ThreadStatic] private static int cachedMoatWidth;
        [ThreadStatic] private static int cachedBridgeWidth;
        [ThreadStatic] private static int cachedFords;
        [ThreadStatic] private static MoatShape cachedShape;
        [ThreadStatic] private static IslandTerrain cachedIslandTerrain;
        [ThreadStatic] private static float cachedIslandPercent;
        [ThreadStatic] private static int cachedIslandWater;
        [ThreadStatic] private static int cachedIslandPassage;
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
            MoatShape shape = settings != null ? settings.mapShape : MoatShape.MountainSide;
            IslandTerrain islandTerrain = settings != null ? settings.islandTerrain : IslandTerrain.Mountain;
            float islandPercent = settings != null
                ? Mathf.Clamp(settings.islandPercent, MoatSettings.MinIslandPercent, MoatSettings.MaxIslandPercent) : 0.40f;
            int islandWater = settings != null
                ? Mathf.Clamp(settings.islandWaterWidth, MoatSettings.MinMoatWidth, MoatSettings.MaxMoatWidth) : MoatSettings.MinMoatWidth;
            int islandPassage = settings != null ? Mathf.Clamp(settings.islandPassageWidth, 0, MoatSettings.MaxLandBridgeWidth) : 0;

            // Scatterers ask for the layout once per candidate cell, so build it once per map
            if (cachedMap == map && cachedSide == side && cachedPercent == percent && cachedRoughness == roughness
                && cachedMoatType == moatType && cachedMoatWidth == moatWidth && cachedBridgeWidth == bridgeWidth && cachedFords == fords
                && cachedShape == shape && cachedIslandTerrain == islandTerrain && cachedIslandPercent == islandPercent
                && cachedIslandWater == islandWater && cachedIslandPassage == islandPassage)
            {
                return cachedLayout;
            }

            MoatLayout layout = new MoatLayout();
            layout.side = side;
            layout.sizeX = map.Size.x;
            layout.sizeZ = map.Size.z;
            int seed = EdgeSeed(map);
            if (shape == MoatShape.Island)
            {
                layout.BuildIsland(islandTerrain, islandPercent, roughness, islandWater, islandPassage, seed);
            }
            else
            {
                layout.thickness = (int)(layout.DepthAxisLength * percent);
                layout.BuildEdge(roughness, seed);
                int[] outerDepth = layout.BuildMoat(moatType, moatWidth, bridgeWidth, fords, seed);
                layout.BuildNearEdge(outerDepth);
            }

            cachedMap = map;
            cachedSide = side;
            cachedPercent = percent;
            cachedRoughness = roughness;
            cachedMoatType = moatType;
            cachedMoatWidth = moatWidth;
            cachedBridgeWidth = bridgeWidth;
            cachedFords = fords;
            cachedShape = shape;
            cachedIslandTerrain = islandTerrain;
            cachedIslandPercent = islandPercent;
            cachedIslandWater = islandWater;
            cachedIslandPassage = islandPassage;
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

        // An island in the middle of the map, round at 0% roughness, ringed by deep water. The only dry way across is a
        // straight strip on the chosen side, at the middle of that side like the land bridge.
        private void BuildIsland(IslandTerrain terrain, float percent, float roughness, int waterWidth, int passageWidth, int seed)
        {
            islandIsMountain = terrain == IslandTerrain.Mountain;
            int cells = sizeX * sizeZ;
            float cx = sizeX / 2f;
            float cz = sizeZ / 2f;
            int minSide = Math.Min(sizeX, sizeZ);
            float maxRadius = Math.Max(1f, minSide / 2f - waterWidth - IslandMapEdgeMargin);
            float minRadius = Math.Min(MinIslandRadius, maxRadius);
            float radius = Mathf.Clamp(minSide * percent / 2f, minRadius, maxRadius);
            float amplitude = roughness * IslandRoughnessAmplitude * radius;
            int perimeter = Math.Max(64, Mathf.RoundToInt(2f * Mathf.PI * radius));
            float[] profile = amplitude > 0f ? MoatEdgeNoise.WrappedProfile(perimeter, seed) : null;

            islandGrid = new bool[cells];
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dz = z + 0.5f - cz;
                    float r = radius;
                    if (profile != null)
                    {
                        float pos = (Mathf.Atan2(dz, dx) + Mathf.PI) / (2f * Mathf.PI) * perimeter;
                        int i0 = (int)pos % perimeter;
                        float t = pos - Mathf.Floor(pos);
                        float noise = profile[i0] + (profile[(i0 + 1) % perimeter] - profile[i0]) * t;
                        r = Mathf.Clamp(radius + amplitude * noise, minRadius, maxRadius);
                    }
                    islandGrid[z * sizeX + x] = dx * dx + dz * dz < r * r;
                }
            }

            // Water: every cell off the island within waterWidth cells (straight-line distance) of it, as in BuildMoat.
            // Every cell next to the island, diagonals too, is deep, so nobody reaches the shore except over the way in.
            int[] nearestSq = new int[cells];
            for (int i = 0; i < cells; i++) nearestSq[i] = int.MaxValue;
            float reachSq = (waterWidth + 0.5f) * (waterWidth + 0.5f);
            float deepSq = (waterWidth - 0.5f) * (waterWidth - 0.5f);
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (!islandGrid[z * sizeX + x] || !IsShore(x, z)) continue;
                    for (int oz = -waterWidth; oz <= waterWidth; oz++)
                    {
                        for (int ox = -waterWidth; ox <= waterWidth; ox++)
                        {
                            int sq = ox * ox + oz * oz;
                            int nx = x + ox;
                            int nz = z + oz;
                            if (sq > reachSq || nx < 0 || nz < 0 || nx >= sizeX || nz >= sizeZ) continue;
                            int idx = nz * sizeX + nx;
                            if (!islandGrid[idx] && sq < nearestSq[idx]) nearestSq[idx] = sq;
                        }
                    }
                }
            }

            this.passageWidth = passageWidth;
            passageStart = EdgeLength / 2 - passageWidth / 2;
            int middleDepth = DepthAxisLength / 2;
            moatGrid = new byte[cells];
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    int idx = z * sizeX + x;
                    if (nearestSq[idx] > reachSq) continue;
                    IntVec3 c = new IntVec3(x, 0, z);
                    MoatWater water = IsOnWayIn(c) ? MoatWater.Bridge : nearestSq[idx] <= deepSq ? MoatWater.Deep : MoatWater.Shallow;
                    moatGrid[idx] = (byte)water;
                }
            }

            bool mountain = islandIsMountain;
            bool[] island = islandGrid;
            byte[] moat = moatGrid;
            insideDepth = StepsFrom(i => !island[i]);
            openDistance = StepsFrom(i => moat[i] != (byte)MoatWater.None || (mountain && island[i]));
            for (int i = 0; i < cells; i++) openDistance[i]--;

            if (islandIsMountain)
            {
                // On the plains in front of the way in, short of the water
                int mid = EdgeLength / 2;
                int firstWater = 0;
                while (firstWater < middleDepth && moatGrid[Index(CellAt(firstWater, mid))] == (byte)MoatWater.None) firstWater++;
                startTarget = CellAt(Math.Max(IslandMapEdgeMargin / 2, firstWater - IslandStartDistance), mid);
            }
            else
            {
                startTarget = new IntVec3(sizeX / 2, 0, sizeZ / 2);
            }
        }

        private bool IsShore(int x, int z)
        {
            for (int d = 0; d < 8; d++)
            {
                IntVec3 n = new IntVec3(x, 0, z) + GenAdj.AdjacentCells[d];
                if (n.x < 0 || n.z < 0 || n.x >= sizeX || n.z >= sizeZ || !islandGrid[n.z * sizeX + n.x]) return true;
            }
            return false;
        }

        // Steps (8-way) from every cell to the nearest source cell; sources are 0
        private int[] StepsFrom(Func<int, bool> isSource)
        {
            int cells = sizeX * sizeZ;
            int[] steps = new int[cells];
            Queue<int> queue = new Queue<int>();
            for (int i = 0; i < cells; i++)
            {
                if (isSource(i))
                {
                    queue.Enqueue(i);
                }
                else
                {
                    steps[i] = int.MaxValue / 2;
                }
            }
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                IntVec3 c = new IntVec3(cur % sizeX, 0, cur / sizeX);
                for (int d = 0; d < 8; d++)
                {
                    IntVec3 n = c + GenAdj.AdjacentCells[d];
                    if (n.x < 0 || n.z < 0 || n.x >= sizeX || n.z >= sizeZ) continue;
                    int idx = n.z * sizeX + n.x;
                    if (steps[idx] <= steps[cur] + 1) continue;
                    steps[idx] = steps[cur] + 1;
                    queue.Enqueue(idx);
                }
            }
            return steps;
        }

        private int Index(IntVec3 c)
        {
            return Mathf.Clamp(c.z, 0, sizeZ - 1) * sizeX + Mathf.Clamp(c.x, 0, sizeX - 1);
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
                if (islandGrid != null)
                {
                    if (!islandIsMountain) return 0;
                    for (int i = 0; i < islandGrid.Length; i++)
                    {
                        if (islandGrid[i]) count++;
                    }
                    return count;
                }
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
            if (islandGrid != null) return islandIsMountain && IsOnIsland(c);
            return Depth(c) < MountainDepthAt(Along(c));
        }

        public bool HasIsland
        {
            get { return islandGrid != null; }
        }

        public bool IsOnIsland(IntVec3 c)
        {
            if (islandGrid == null || c.x < 0 || c.z < 0 || c.x >= sizeX || c.z >= sizeZ) return false;
            return islandGrid[c.z * sizeX + c.x];
        }

        // Island maps: the straight line of the way in, from the map edge to the middle of the island. Kept free of
        // hill rock, so the way in can always be walked.
        public bool IsOnWayIn(IntVec3 c)
        {
            if (islandGrid == null || passageWidth <= 0) return false;
            int a = Along(c);
            return a >= passageStart && a < passageStart + passageWidth && Depth(c) < DepthAxisLength / 2;
        }

        // Mountain island cells with more than margin cells of rock between them and the water
        public List<IntVec3> InnerMountainCells(int margin)
        {
            List<IntVec3> cells = new List<IntVec3>();
            if (islandGrid == null || !islandIsMountain) return cells;
            for (int i = 0; i < islandGrid.Length; i++)
            {
                if (insideDepth[i] > margin) cells.Add(new IntVec3(i % sizeX, 0, i / sizeX));
            }
            return cells;
        }

        // Where caves may be dug: deep enough in the mountain and away from every map edge
        public bool IsCaveZone(IntVec3 c)
        {
            if (c.x < CaveBufferFromMapEdge || c.z < CaveBufferFromMapEdge
                || c.x >= sizeX - CaveBufferFromMapEdge || c.z >= sizeZ - CaveBufferFromMapEdge)
            {
                return false;
            }
            if (islandGrid != null) return islandIsMountain && insideDepth[c.z * sizeX + c.x] > CaveBufferFromPlains;
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
        // spurs a few cells to the side; zero or negative inside the mountain, the moat and in narrow bays.
        // On an island map: from the nearest water (and mountain), on the island too
        public int DistanceFromMountain(IntVec3 c)
        {
            if (islandGrid != null) return openDistance[Index(c)];
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

        // A profile whose end runs smoothly into its start, for the shore of an island
        public static float[] WrappedProfile(int length, int seed)
        {
            int overlap = Math.Max(1, length / 4);
            float[] open = Profile(length + overlap, seed);
            float[] result = new float[length];
            Array.Copy(open, result, length);
            // The first cells fade from the profile's continuation past the end into its own start
            for (int a = 0; a < overlap; a++)
            {
                float t = (a + 1f) / (overlap + 1f);
                result[a] = open[length + a] * (1f - t) + open[a] * t;
            }
            return result;
        }
    }
}
