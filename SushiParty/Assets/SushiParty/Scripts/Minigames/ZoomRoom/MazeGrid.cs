using SushiParty.Audio;
using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Minigames.ZoomRoom
{
    public sealed class MazeGrid
    {
        public const float TileSize = 2.4f;

        private static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private readonly bool[,] open;
        private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();

        public int Width { get; }
        public int Height { get; }

        public MazeGrid(int cellsX, int cellsY, int extraOpenings)
        {
            Width = cellsX * 2 + 1;
            Height = cellsY * 2 + 1;
            open = new bool[Width, Height];

            Carve(cellsX, cellsY);
            AddLoops(extraOpenings);
        }

        public bool IsOpen(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && open[x, y];
        }

        public Vector2 TileToWorld(Vector2Int tile)
        {
            return new Vector2(
                (tile.x - (Width - 1) * 0.5f) * TileSize,
                (tile.y - (Height - 1) * 0.5f) * TileSize);
        }

        public Vector2Int WorldToTile(Vector2 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt(world.x / TileSize + (Width - 1) * 0.5f),
                Mathf.RoundToInt(world.y / TileSize + (Height - 1) * 0.5f));
        }

        public bool Blocked(Vector2 world, float radius)
        {
            for (int cornerX = -1; cornerX <= 1; cornerX += 2)
            {
                for (int cornerY = -1; cornerY <= 1; cornerY += 2)
                {
                    Vector2 corner = world + new Vector2(cornerX * radius, cornerY * radius);
                    Vector2Int tile = WorldToTile(corner);
                    if (!IsOpen(tile.x, tile.y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public Vector2 Move(Vector2 position, Vector2 delta, float radius)
        {
            Vector2 result = position;

            Vector2 tryX = new Vector2(result.x + delta.x, result.y);
            if (!Blocked(tryX, radius))
            {
                result = tryX;
            }

            Vector2 tryY = new Vector2(result.x, result.y + delta.y);
            if (!Blocked(tryY, radius))
            {
                result = tryY;
            }

            return result;
        }

        public int[,] CreateDistanceBuffer()
        {
            return new int[Width, Height];
        }

        public void FillDistances(Vector2Int start, int[,] into)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    into[x, y] = int.MaxValue;
                }
            }

            if (!IsOpen(start.x, start.y))
            {
                start = NearestOpen(start);
            }

            frontier.Clear();
            into[start.x, start.y] = 0;
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                int next = into[current.x, current.y] + 1;

                foreach (Vector2Int step in Steps)
                {
                    Vector2Int neighbour = current + step;
                    if (!IsOpen(neighbour.x, neighbour.y) || into[neighbour.x, neighbour.y] <= next)
                    {
                        continue;
                    }

                    into[neighbour.x, neighbour.y] = next;
                    frontier.Enqueue(neighbour);
                }
            }
        }

        public Vector2Int NearestOpen(Vector2Int tile)
        {
            if (IsOpen(tile.x, tile.y))
            {
                return tile;
            }

            for (int radius = 1; radius < Mathf.Max(Width, Height); radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        Vector2Int candidate = new Vector2Int(tile.x + dx, tile.y + dy);
                        if (IsOpen(candidate.x, candidate.y))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return new Vector2Int(1, 1);
        }

        public IEnumerable<Vector2Int> OpenNeighbours(Vector2Int tile)
        {
            foreach (Vector2Int step in Steps)
            {
                Vector2Int neighbour = tile + step;
                if (IsOpen(neighbour.x, neighbour.y))
                {
                    yield return neighbour;
                }
            }
        }

        private void Carve(int cellsX, int cellsY)
        {
            bool[,] visited = new bool[cellsX, cellsY];
            Stack<Vector2Int> stack = new Stack<Vector2Int>();

            Vector2Int start = new Vector2Int(Random.Range(0, cellsX), Random.Range(0, cellsY));
            visited[start.x, start.y] = true;
            open[start.x * 2 + 1, start.y * 2 + 1] = true;
            stack.Push(start);

            List<Vector2Int> candidates = new List<Vector2Int>(4);

            while (stack.Count > 0)
            {
                Vector2Int cell = stack.Peek();
                candidates.Clear();

                foreach (Vector2Int step in Steps)
                {
                    Vector2Int next = cell + step;
                    if (next.x < 0 || next.x >= cellsX || next.y < 0 || next.y >= cellsY)
                    {
                        continue;
                    }

                    if (!visited[next.x, next.y])
                    {
                        candidates.Add(next);
                    }
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
                visited[chosen.x, chosen.y] = true;

                open[chosen.x * 2 + 1, chosen.y * 2 + 1] = true;
                open[cell.x * 2 + 1 + (chosen.x - cell.x), cell.y * 2 + 1 + (chosen.y - cell.y)] = true;

                stack.Push(chosen);
            }
        }

        private void AddLoops(int count)
        {
            int attempts = 0;
            while (count > 0 && attempts < count * 40)
            {
                attempts++;

                int x = Random.Range(1, Width - 1);
                int y = Random.Range(1, Height - 1);

                if (open[x, y])
                {
                    continue;
                }

                bool horizontal = IsOpen(x - 1, y) && IsOpen(x + 1, y);
                bool vertical = IsOpen(x, y - 1) && IsOpen(x, y + 1);
                if (!horizontal && !vertical)
                {
                    continue;
                }

                open[x, y] = true;
                count--;
            }
        }
    }
}
