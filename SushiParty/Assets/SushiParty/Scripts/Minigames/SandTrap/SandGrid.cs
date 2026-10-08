using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.SandTrap
{
    public sealed class SandGrid
    {
        public const int Size = 5;
        public const float CellSize = 2.2f;
        public const float BlockHeight = 1.6f;
        private static readonly Color RowTint = new Color(0.98f, 0.55f, 0.30f);
        private static readonly Color ColumnTint = new Color(0.40f, 0.72f, 0.98f);
        private static readonly Color CrossTint = new Color(1f, 0.98f, 0.75f);
        private readonly Transform root;
        private readonly Transform[,] blocks = new Transform[Size, Size];
        private readonly bool[,] present = new bool[Size, Size];
        private readonly List<FallingBlock> falling = new List<FallingBlock>();

        private sealed class FallingBlock
        {
            public Transform Transform;
            public float Age;
            public float Speed;
        }

        public SandGrid(Transform parent)
        {
            root = new GameObject("SandGrid").transform;
            root.SetParent(parent, false);

            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    Vector3 centre = CentreOf(row, column);
                    blocks[row, column] = Shapes.Cube(
                        root,
                        centre + new Vector3(0f, -BlockHeight * 0.5f, 0f),
                        new Vector3(CellSize * 0.92f, BlockHeight, CellSize * 0.92f),
                        (row + column) % 2 == 0 ? Palette.Sand : Palette.SandDark,
                        $"Sand_{row}_{column}");
                    present[row, column] = true;
                }
            }
        }

        public static float CoordinateFor(int index)
        {
            return (index - (Size - 1) * 0.5f) * CellSize;
        }

        public static int IndexNear(float coordinate, float tolerance)
        {
            int best = -1;
            float bestDistance = tolerance;

            for (int i = 0; i < Size; i++)
            {
                float distance = Mathf.Abs(CoordinateFor(i) - coordinate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        public static Vector3 CentreOf(int row, int column)
        {
            return new Vector3(CoordinateFor(column), 0f, CoordinateFor(row));
        }

        public bool Has(int row, int column)
        {
            return InBounds(row, column) && present[row, column];
        }

        public static bool InBounds(int row, int column)
        {
            return row >= 0 && row < Size && column >= 0 && column < Size;
        }

        public bool Remove(int row, int column)
        {
            if (!Has(row, column))
            {
                return false;
            }

            present[row, column] = false;

            Transform block = blocks[row, column];
            blocks[row, column] = null;

            if (block != null)
            {
                falling.Add(new FallingBlock { Transform = block, Speed = 2f });
            }

            return true;
        }

        public int NeighbourCount(int row, int column)
        {
            int count = 0;
            if (Has(row + 1, column)) count++;
            if (Has(row - 1, column)) count++;
            if (Has(row, column + 1)) count++;
            if (Has(row, column - 1)) count++;
            return count;
        }

        public int RemainingBlocks()
        {
            int count = 0;
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (present[row, column])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public void ApplyHighlights(int highlightedRow, int highlightedColumn)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    Transform block = blocks[row, column];
                    if (block == null)
                    {
                        continue;
                    }

                    bool rowLit = row == highlightedRow;
                    bool columnLit = column == highlightedColumn;

                    if (rowLit && columnLit)
                    {
                        Shapes.Tint(block, CrossTint, glow: true);
                    }
                    else if (rowLit)
                    {
                        Shapes.Tint(block, RowTint, glow: true);
                    }
                    else if (columnLit)
                    {
                        Shapes.Tint(block, ColumnTint, glow: true);
                    }
                    else
                    {
                        Shapes.Tint(block, (row + column) % 2 == 0 ? Palette.Sand : Palette.SandDark);
                    }
                }
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                FallingBlock block = falling[i];
                block.Age += deltaTime;
                block.Speed += 22f * deltaTime;

                if (block.Transform != null)
                {
                    block.Transform.position += Vector3.down * (block.Speed * deltaTime);
                    block.Transform.Rotate(new Vector3(18f, 26f, 12f) * deltaTime, Space.Self);
                }

                if (block.Age < 1.4f)
                {
                    continue;
                }

                if (block.Transform != null)
                {
                    Object.Destroy(block.Transform.gameObject);
                }

                falling.RemoveAt(i);
            }
        }
    }
}
