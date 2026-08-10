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
        public int Size { get; }
        public int Seed { get; }
        public GridCoordinate PlayerStart { get; }
        public IReadOnlyList<GridCoordinate> OpponentStarts { get; }
        public IReadOnlyList<MinePlacement> Mines { get; }

        public GridMapLayout(
            int size,
            int seed,
            GridCoordinate playerStart,
            IReadOnlyList<GridCoordinate> opponentStarts,
            IReadOnlyList<MinePlacement> mines)
        {
            Size = size;
            Seed = seed;
            PlayerStart = playerStart;
            OpponentStarts = opponentStarts ??
                Array.Empty<GridCoordinate>();
            Mines = mines ?? Array.Empty<MinePlacement>();
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
                mineCount,
                seed,
                playerStart,
                Array.Empty<GridCoordinate>());
        }

        public GridMapLayout Generate(
            int size,
            int mineCount,
            int seed,
            GridCoordinate playerStart,
            IReadOnlyList<GridCoordinate> opponentStarts)
        {
            if (size < 2)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (playerStart.X < 0 || playerStart.X >= size ||
                playerStart.Y < 0 || playerStart.Y >= size)
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
                    if (opponentStart.X < 0 || opponentStart.X >= size ||
                        opponentStart.Y < 0 || opponentStart.Y >= size)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(opponentStarts));
                    }

                    if (blockedStarts.Add(opponentStart))
                        validOpponentStarts.Add(opponentStart);
                }
            }

            var candidates = new List<GridCoordinate>(
                size * size - blockedStarts.Count);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var coordinate = new GridCoordinate(x, y);
                    if (!blockedStarts.Contains(coordinate))
                        candidates.Add(coordinate);
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

            int clampedMineCount = Math.Min(
                Math.Max(0, mineCount),
                candidates.Count);
            var mines = new MinePlacement[clampedMineCount];
            for (int i = 0; i < clampedMineCount; i++)
            {
                mines[i] = new MinePlacement(
                    candidates[i],
                    random.Next(0, 5) == 0
                        ? MineKind.Gold
                        : MineKind.Normal);
            }

            return new GridMapLayout(
                size,
                seed,
                playerStart,
                validOpponentStarts,
                mines);
        }
    }
}
