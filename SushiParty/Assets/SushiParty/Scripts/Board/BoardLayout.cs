using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Board
{
    public enum SpaceKind
    {
        Start = 0,

        CoinGain = 1,

        CoinLoss = 2,

        Shrine = 3,

        Wasabi = 4,

        Swap = 5,
    }

    public readonly struct BoardSpace
    {
        public readonly int Index;
        public readonly SpaceKind Kind;
        public readonly Vector2 Position;
        public readonly int[] Exits;

        public BoardSpace(int index, SpaceKind kind, Vector2 position, int[] exits)
        {
            Index = index;
            Kind = kind;
            Position = position;
            Exits = exits;
        }

        public bool IsJunction => Exits != null && Exits.Length > 1;
    }

    public sealed class BoardLayout
    {
        private readonly SpaceKind[] kinds;
        private readonly Vector2[] positions;
        private readonly int[][] exits;

        public BoardLayout(IReadOnlyList<SpaceKind> kinds, IReadOnlyList<Vector2> positions, IReadOnlyList<int[]> exits)
        {
            if (kinds == null || kinds.Count < 2)
            {
                throw new System.ArgumentException("a board needs at least two spaces", nameof(kinds));
            }

            if (positions == null || positions.Count != kinds.Count)
            {
                throw new System.ArgumentException("every space needs a position", nameof(positions));
            }

            if (exits == null || exits.Count != kinds.Count)
            {
                throw new System.ArgumentException("every space needs an exit list", nameof(exits));
            }

            this.kinds = new SpaceKind[kinds.Count];
            this.positions = new Vector2[kinds.Count];
            this.exits = new int[kinds.Count][];

            for (int i = 0; i < kinds.Count; i++)
            {
                if (exits[i] == null || exits[i].Length == 0)
                {
                    throw new System.ArgumentException($"space {i} is a dead end", nameof(exits));
                }

                for (int e = 0; e < exits[i].Length; e++)
                {
                    int target = exits[i][e];
                    if (target < 0 || target >= kinds.Count)
                    {
                        throw new System.ArgumentException($"space {i} exits to {target}, which does not exist", nameof(exits));
                    }
                }

                this.kinds[i] = kinds[i];
                this.positions[i] = positions[i];
                this.exits[i] = exits[i];
            }
        }

        public int Count => kinds.Length;
        public SpaceKind KindAt(int index) => kinds[index];
        public Vector2 PositionOf(int index) => positions[index];
        public IReadOnlyList<int> ExitsOf(int index) => exits[index];
        public bool IsJunction(int index) => exits[index].Length > 1;

        public BoardSpace SpaceAt(int index)
        {
            return new BoardSpace(index, kinds[index], positions[index], exits[index]);
        }

        public int Advance(int index, int steps)
        {
            int at = index;
            for (int i = 0; i < steps; i++)
            {
                at = exits[at][0];
            }

            return at;
        }

        public int StepsBetween(int from, int to)
        {
            if (from == to)
            {
                return 0;
            }

            int[] distance = new int[kinds.Length];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = -1;
            }

            Queue<int> frontier = new Queue<int>();
            distance[from] = 0;
            frontier.Enqueue(from);

            while (frontier.Count > 0)
            {
                int at = frontier.Dequeue();
                int[] onward = exits[at];

                for (int e = 0; e < onward.Length; e++)
                {
                    int next = onward[e];
                    if (distance[next] != -1)
                    {
                        continue;
                    }

                    distance[next] = distance[at] + 1;
                    if (next == to)
                    {
                        return distance[next];
                    }

                    frontier.Enqueue(next);
                }
            }

            return -1;
        }

        public List<int> IndicesOf(SpaceKind kind)
        {
            List<int> found = new List<int>();
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i] == kind)
                {
                    found.Add(i);
                }
            }

            return found;
        }

        public void SetKind(int index, SpaceKind kind)
        {
            kinds[index] = kind;
        }

        public static BoardLayout CreateDefault()
        {
            (SpaceKind kind, float x, float y, int[] exits)[] board =
            {
                 (SpaceKind.Start,   -14f, -11f, new[] { 1 }),
                 (SpaceKind.CoinGain, -10f, -12.5f, new[] { 2 }),
                 (SpaceKind.CoinGain,  -6f, -13f, new[] { 3 }),
                 (SpaceKind.Swap,     -2f, -12f, new[] { 4 }),
                 (SpaceKind.CoinGain,   2f, -10.5f, new[] { 5 }),

                 (SpaceKind.CoinLoss,   6f, -9f, new[] { 6, 21 }),

                 (SpaceKind.CoinGain,  10f, -7f, new[] { 7 }),
                 (SpaceKind.CoinGain,  13f, -3.5f, new[] { 8 }),
                 (SpaceKind.Wasabi,   14.5f, 0.5f, new[] { 9 }),
                 (SpaceKind.CoinGain,  14f, 4.5f, new[] { 10 }),
                 (SpaceKind.Shrine,   12f, 8f, new[] { 11 }),
                 (SpaceKind.CoinGain,   9f, 11f, new[] { 12 }),
                 (SpaceKind.CoinLoss,   5f, 12.5f, new[] { 13 }),
                 (SpaceKind.Swap,      1f, 12.5f, new[] { 14 }),
                 (SpaceKind.CoinGain,  -3f, 11.5f, new[] { 15 }),

                 (SpaceKind.CoinGain,  -6.5f, 9.5f, new[] { 16 }),
                 (SpaceKind.CoinLoss, -10f, 7.5f, new[] { 17 }),

                 (SpaceKind.CoinGain, -13f, 4.5f, new[] { 18, 25 }),
                 (SpaceKind.CoinGain, -15f, 0.5f, new[] { 19 }),
                 (SpaceKind.CoinLoss, -15.5f, -3.5f, new[] { 20 }),
                 (SpaceKind.CoinGain, -14.5f, -7.5f, new[] { 0 }),

                 (SpaceKind.Wasabi,    5f, -4.5f, new[] { 22 }),
                 (SpaceKind.CoinGain,   2.5f, -1f, new[] { 23 }),
                 (SpaceKind.CoinLoss,  -0.5f, 3f, new[] { 24 }),
                 (SpaceKind.CoinGain,  -3.5f, 6.5f, new[] { 15 }),

                 (SpaceKind.CoinLoss, -11.5f, 0.5f, new[] { 26 }),
                 (SpaceKind.CoinGain, -11.5f, -4.5f, new[] { 0 }),
            };

            SpaceKind[] kinds = new SpaceKind[board.Length];
            Vector2[] positions = new Vector2[board.Length];
            int[][] exits = new int[board.Length][];

            for (int i = 0; i < board.Length; i++)
            {
                kinds[i] = board[i].kind;
                positions[i] = new Vector2(board[i].x, board[i].y);
                exits[i] = board[i].exits;
            }

            return new BoardLayout(kinds, positions, exits);
        }
    }
}
