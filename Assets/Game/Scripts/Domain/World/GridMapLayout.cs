using System;
using System.Collections.Generic;

namespace Game.Domain.World
{
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public int X { get; }
        public int Y { get; }

        public GridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridCoordinate other) =>
            X == other.X && Y == other.Y;

        public override bool Equals(object obj) =>
            obj is GridCoordinate other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";
    }

    public enum MineKind
    {
        Normal,
        Gold
    }

    public enum GridTerrainKind
    {
        Ocean,
        Plains,
        Forest,
        Desert,
        Hills,
        Tundra
    }

    public readonly struct MinePlacement
    {
        public GridCoordinate Coordinate { get; }
        public MineKind Kind { get; }

        public MinePlacement(GridCoordinate coordinate, MineKind kind)
        {
            Coordinate = coordinate;
            Kind = kind;
        }
    }

    public sealed class GridMapLayout
    {
        public int Width { get; }
        public int Height { get; }
        public bool WrapHorizontally { get; }

        [Obsolete("정사각형 전용 속성입니다. Width와 Height를 사용하세요.")]
        public int Size => Width;
        public int Seed { get; }
        public GridCoordinate PlayerStart { get; }
        public IReadOnlyList<GridCoordinate> OpponentStarts { get; }
        public IReadOnlyList<GridCoordinate> NeutralCastles { get; }
        public IReadOnlyList<MinePlacement> Mines { get; }
        public IReadOnlyList<GridTerrainKind> Terrain { get; }

        public GridMapLayout(
            int size,
            int seed,
            GridCoordinate playerStart,
            IReadOnlyList<GridCoordinate> opponentStarts,
            IReadOnlyList<MinePlacement> mines)
            : this(
                size,
                size,
                seed,
                playerStart,
                opponentStarts,
                mines,
                true,
                null)
        {
        }

        public GridMapLayout(
            int width,
            int height,
            int seed,
            GridCoordinate playerStart,
            IReadOnlyList<GridCoordinate> opponentStarts,
            IReadOnlyList<MinePlacement> mines,
            bool wrapHorizontally,
            IReadOnlyList<GridTerrainKind> terrain = null,
            IReadOnlyList<GridCoordinate> neutralCastles = null)
        {
            if (width < 2)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 2)
                throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            WrapHorizontally = wrapHorizontally;
            Seed = seed;
            PlayerStart = playerStart;
            OpponentStarts = opponentStarts ??
                Array.Empty<GridCoordinate>();
            NeutralCastles = neutralCastles ??
                Array.Empty<GridCoordinate>();
            Mines = mines ?? Array.Empty<MinePlacement>();
            Terrain = terrain != null && terrain.Count == width * height
                ? terrain
                : CreateDefaultTerrain(width * height);
        }

        public bool Contains(GridCoordinate coordinate) =>
            coordinate.X >= 0 && coordinate.X < Width &&
            coordinate.Y >= 0 && coordinate.Y < Height;

        public GridCoordinate Normalize(GridCoordinate coordinate)
        {
            int x = WrapHorizontally
                ? PositiveModulo(coordinate.X, Width)
                : coordinate.X;
            return new GridCoordinate(x, coordinate.Y);
        }

        public bool TryNormalize(
            GridCoordinate coordinate,
            out GridCoordinate normalized)
        {
            normalized = Normalize(coordinate);
            return normalized.Y >= 0 && normalized.Y < Height &&
                   normalized.X >= 0 && normalized.X < Width;
        }

        public int HorizontalDistance(
            GridCoordinate left,
            GridCoordinate right)
        {
            int direct = Math.Abs(
                Normalize(left).X - Normalize(right).X);
            return WrapHorizontally
                ? Math.Min(direct, Width - direct)
                : direct;
        }

        public int ManhattanDistance(
            GridCoordinate left,
            GridCoordinate right) =>
            HorizontalDistance(left, right) +
            Math.Abs(left.Y - right.Y);

        public GridCoordinate Move(
            GridCoordinate origin,
            int deltaX,
            int deltaY)
        {
            var destination = new GridCoordinate(
                origin.X + deltaX,
                origin.Y + deltaY);
            if (!TryNormalize(destination, out GridCoordinate normalized))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destination),
                    "세로 방향으로는 맵 경계를 넘을 수 없습니다.");
            }

            return normalized;
        }

        public GridTerrainKind GetTerrain(GridCoordinate coordinate)
        {
            if (!TryNormalize(coordinate, out GridCoordinate normalized))
                throw new ArgumentOutOfRangeException(nameof(coordinate));

            return Terrain[normalized.Y * Width + normalized.X];
        }

        public bool IsLand(GridCoordinate coordinate) =>
            GetTerrain(coordinate) != GridTerrainKind.Ocean;

        public bool IsNeutralCastle(GridCoordinate coordinate)
        {
            if (!TryNormalize(coordinate, out GridCoordinate normalized))
                return false;

            for (int i = 0; i < NeutralCastles.Count; i++)
            {
                if (NeutralCastles[i].Equals(normalized))
                    return true;
            }

            return false;
        }

        private static GridTerrainKind[] CreateDefaultTerrain(int count)
        {
            var terrain = new GridTerrainKind[count];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            return terrain;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }

    public sealed class GridMapLayoutGenerator
    {
        public GridMapLayout Generate(
            int size,
            int mineCount,
            int seed,
            GridCoordinate playerStart)
        {
            return Generate(
                size,
                size,
                mineCount,
                seed,
                playerStart,
                Array.Empty<GridCoordinate>(),
                true);
        }

        public GridMapLayout Generate(
            int size,
            int mineCount,
            int seed,
            GridCoordinate playerStart,
            IReadOnlyList<GridCoordinate> opponentStarts)
        {
            return Generate(
                size,
                size,
                mineCount,
                seed,
                playerStart,
                opponentStarts,
                true);
        }

        public GridMapLayout Generate(
            int width,
            int height,
            int mineCount,
            int seed,
            GridCoordinate playerStart,
            IReadOnlyList<GridCoordinate> opponentStarts,
            bool wrapHorizontally = true,
            int neutralCastleCount = 0)
        {
            if (width < 2)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 2)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (playerStart.X < 0 || playerStart.X >= width ||
                playerStart.Y < 0 || playerStart.Y >= height)
            {
                throw new ArgumentOutOfRangeException(nameof(playerStart));
            }

            var blockedStarts = new HashSet<GridCoordinate>
            {
                playerStart
            };
            var validOpponentStarts = new List<GridCoordinate>();
            if (opponentStarts != null)
            {
                for (int i = 0; i < opponentStarts.Count; i++)
                {
                    GridCoordinate opponentStart = opponentStarts[i];
                    if (opponentStart.X < 0 || opponentStart.X >= width ||
                        opponentStart.Y < 0 || opponentStart.Y >= height)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(opponentStarts));
                    }

                    if (blockedStarts.Add(opponentStart))
                        validOpponentStarts.Add(opponentStart);
                }
            }

            GridTerrainKind[] terrain = GenerateTerrain(
                width,
                height,
                seed);
            foreach (GridCoordinate start in blockedStarts)
            {
                terrain[start.Y * width + start.X] =
                    GridTerrainKind.Plains;
            }

            var candidates = new List<GridCoordinate>(
                width * height - blockedStarts.Count);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var coordinate = new GridCoordinate(x, y);
                    if (!blockedStarts.Contains(coordinate) &&
                        terrain[y * width + x] != GridTerrainKind.Ocean)
                    {
                        candidates.Add(coordinate);
                    }
                }
            }

            var random = new Random(seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                GridCoordinate temporary = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }

            int clampedNeutralCastleCount = Math.Min(
                Math.Max(0, neutralCastleCount),
                candidates.Count);
            var neutralCastles = new List<GridCoordinate>(
                clampedNeutralCastleCount);
            var neutralCastleSet = new HashSet<GridCoordinate>();
            const int minimumDistanceFromFactionStart = 7;
            const int minimumNeutralCastleSpacing = 6;

            // 항구 역할을 실제로 선택할 수 있도록 가능한 경우 첫 빈 성은
            // 반드시 해안 육지에 배치한다.
            if (clampedNeutralCastleCount > 0)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    GridCoordinate candidate = candidates[i];
                    if (!IsCoastalLand(
                            candidate,
                            terrain,
                            width,
                            height,
                            wrapHorizontally) ||
                        !IsFarEnoughFrom(
                            candidate,
                            blockedStarts,
                            minimumDistanceFromFactionStart,
                            width,
                            wrapHorizontally))
                    {
                        continue;
                    }

                    neutralCastles.Add(candidate);
                    neutralCastleSet.Add(candidate);
                    break;
                }
            }

            for (int i = 0;
                 i < candidates.Count &&
                 neutralCastles.Count < clampedNeutralCastleCount;
                 i++)
            {
                GridCoordinate candidate = candidates[i];
                if (!IsFarEnoughFrom(
                        candidate,
                        blockedStarts,
                        minimumDistanceFromFactionStart,
                        width,
                        wrapHorizontally) ||
                    !IsFarEnoughFrom(
                        candidate,
                        neutralCastles,
                        minimumNeutralCastleSpacing,
                        width,
                        wrapHorizontally))
                {
                    continue;
                }

                neutralCastles.Add(candidate);
                neutralCastleSet.Add(candidate);
            }

            // 아주 작은 맵에서도 요청 개수는 가능한 범위까지 채우되,
            // 시작 성이나 다른 빈 성과 좌표가 겹치지는 않게 한다.
            for (int i = 0;
                 i < candidates.Count &&
                 neutralCastles.Count < clampedNeutralCastleCount;
                 i++)
            {
                if (neutralCastleSet.Add(candidates[i]))
                    neutralCastles.Add(candidates[i]);
            }

            int clampedMineCount = Math.Min(
                Math.Max(0, mineCount),
                candidates.Count - neutralCastles.Count);
            var mines = new MinePlacement[clampedMineCount];
            int mineIndex = 0;
            for (int i = 0;
                 i < candidates.Count && mineIndex < clampedMineCount;
                 i++)
            {
                if (neutralCastleSet.Contains(candidates[i]))
                    continue;

                mines[mineIndex] = new MinePlacement(
                    candidates[i],
                    random.Next(0, 5) == 0
                        ? MineKind.Gold
                        : MineKind.Normal);
                mineIndex++;
            }

            return new GridMapLayout(
                width,
                height,
                seed,
                playerStart,
                validOpponentStarts,
                mines,
                wrapHorizontally,
                terrain,
                neutralCastles);
        }

        private static bool IsFarEnoughFrom(
            GridCoordinate candidate,
            IEnumerable<GridCoordinate> others,
            int minimumDistance,
            int width,
            bool wrapHorizontally)
        {
            foreach (GridCoordinate other in others)
            {
                int directX = Math.Abs(candidate.X - other.X);
                int xDistance = wrapHorizontally
                    ? Math.Min(directX, width - directX)
                    : directX;
                int distance = xDistance + Math.Abs(candidate.Y - other.Y);
                if (distance < minimumDistance)
                    return false;
            }

            return true;
        }

        private static bool IsCoastalLand(
            GridCoordinate coordinate,
            IReadOnlyList<GridTerrainKind> terrain,
            int width,
            int height,
            bool wrapHorizontally)
        {
            GridCoordinate[] offsets =
            {
                new GridCoordinate(1, 0),
                new GridCoordinate(-1, 0),
                new GridCoordinate(0, 1),
                new GridCoordinate(0, -1)
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                int x = coordinate.X + offsets[i].X;
                int y = coordinate.Y + offsets[i].Y;
                if (y < 0 || y >= height)
                    continue;
                if (wrapHorizontally)
                    x = ((x % width) + width) % width;
                else if (x < 0 || x >= width)
                    continue;

                if (terrain[y * width + x] == GridTerrainKind.Ocean)
                    return true;
            }

            return false;
        }

        private static GridTerrainKind[] GenerateTerrain(
            int width,
            int height,
            int seed)
        {
            var terrain = new GridTerrainKind[width * height];
            double phase = PositiveHash01(seed, 17, 31) * Math.PI * 2d;

            for (int y = 0; y < height; y++)
            {
                double latitude = height <= 1
                    ? 0d
                    : Math.Abs((y / (double)(height - 1)) * 2d - 1d);
                for (int x = 0; x < width; x++)
                {
                    double longitude = Math.PI * 2d * x / width;
                    double continent =
                        0.50d +
                        0.20d * Math.Sin(longitude * 2d + phase) +
                        0.13d * Math.Cos(
                            longitude * 3d - y * 0.19d + phase * 0.7d) +
                        0.09d * Math.Sin(
                            longitude * 5d + y * 0.31d - phase * 1.3d);
                    double moisture =
                        0.5d +
                        0.28d * Math.Sin(
                            longitude * 3d + y * 0.23d + phase * 2d) +
                        0.12d * (PositiveHash01(seed, x, y) - 0.5d);

                    GridTerrainKind kind;
                    if (continent < 0.34d)
                        kind = GridTerrainKind.Ocean;
                    else if (latitude > 0.84d)
                        kind = GridTerrainKind.Tundra;
                    else if (continent > 0.70d)
                        kind = GridTerrainKind.Hills;
                    else if (moisture > 0.61d)
                        kind = GridTerrainKind.Forest;
                    else if (moisture < 0.34d && latitude < 0.68d)
                        kind = GridTerrainKind.Desert;
                    else
                        kind = GridTerrainKind.Plains;

                    terrain[y * width + x] = kind;
                }
            }

            return terrain;
        }

        private static double PositiveHash01(int seed, int x, int y)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)x * 0x9E3779B9u;
                value ^= (uint)y * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value / (double)uint.MaxValue;
            }
        }
    }
}
