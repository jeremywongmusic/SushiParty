using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Board
{
    public sealed class BoardView
    {
        private const float TileDiameter = 2.2f;
        private const float TileHalfHeight = 0.14f;
        private const float RimSpill = 0.26f;
        private const float RimDrop = 0.10f;
        private const float JunctionScale = 1.30f;
        private const float JunctionSpill = 0.70f;
        private const float ConnectorWidth = 0.42f;
        private const float ConnectorHeight = 0.12f;
        private const float ConnectorDrop = 0.20f;
        private const float MinimumRun = 0.30f;
        private const float ArrowLength = 0.90f;
        private const float ArrowWidth = 0.34f;
        private const float ArrowSweep = 36f;
        private const float ArrowSetback = 0.12f;
        private const float TokenRadius = 0.62f;
        private const float ChefRadius = 0.82f;
        private const float TokenLift = 0.06f;
        private const float HighlightLift = 0.90f;
        private const float HighlightBrighten = 0.35f;
        private const float ShareNudge = 0.62f;
        private const int SeatCount = 3;
        private const float SeaThickness = 0.5f;
        private const float SeaDrop = 1.30f;
        private const float SeaMargin = 33f;
        private const float PlatterThickness = 0.35f;
        private const float PlatterDrop = 0.62f;
        private const float PlatterMargin = 3.2f;
        private static readonly Color CoinOrange = new Color(0.98f, 0.55f, 0.22f);
        private static readonly Color OceanCyan = new Color(0.25f, 0.72f, 0.82f);
        private static readonly Color ShrineGold = new Color(0.98f, 0.80f, 0.28f);
        private static readonly Color TileRim = new Color(0.10f, 0.09f, 0.14f);
        private static readonly Color SeaBlue = new Color(0.09f, 0.34f, 0.48f);
        private static readonly Color PlatterLacquer = new Color(0.16f, 0.14f, 0.20f);
        private static readonly Color PathMat = Palette.SandDark;
        private static readonly Color PathArrow = Palette.Sand;

        private struct Link
        {
            public int From;
            public int To;
            public Transform Mat;
            public Transform ArrowLeft;
            public Transform ArrowRight;
            public bool Lit;
        }

        private readonly Transform root;
        private readonly Transform[] tiles;
        private readonly Transform[] tokens = new Transform[SeatCount];
        private readonly Transform[] bodies = new Transform[SeatCount];
        private readonly CharacterAnimation[] animations = new CharacterAnimation[SeatCount];
        private readonly Vector3[] points;
        private readonly Vector3[] facings;
        private readonly SpaceKind[] kinds;
        private readonly bool[] junctions;
        private readonly int[] tileHighlights;
        private readonly Link[] links;
        private readonly Color[] baseTints = new Color[SeatCount];
        private readonly int[] tokenSpaces = new int[SeatCount];
        private readonly bool[] highlighted = new bool[SeatCount];
        private readonly int spaceCount;

        private BoardView(Transform parent, BoardLayout layout)
        {
            spaceCount = layout.Count;
            tiles = new Transform[spaceCount];
            points = new Vector3[spaceCount];
            facings = new Vector3[spaceCount];
            kinds = new SpaceKind[spaceCount];
            junctions = new bool[spaceCount];
            tileHighlights = new int[spaceCount];
            links = new Link[CountExits(layout)];

            root = new GameObject("Board").transform;
            root.SetParent(parent, false);

            ReadLayout(layout);

            BuildSea(Measure());
            BuildPath(layout);
            BuildSpaces();
            BuildTokens();
        }

        public static BoardView Create(Transform parent, BoardLayout layout)
        {
            if (layout == null)
            {
                throw new System.ArgumentNullException(nameof(layout));
            }

            return new BoardView(parent, layout);
        }

        private static int CountExits(BoardLayout layout)
        {
            int total = 0;
            for (int i = 0; i < layout.Count; i++)
            {
                total += layout.ExitsOf(i).Count;
            }

            return total;
        }

        private void ReadLayout(BoardLayout layout)
        {
            for (int i = 0; i < spaceCount; i++)
            {
                Vector2 authored = layout.PositionOf(i);

                points[i] = new Vector3(authored.x, 0f, authored.y);
                kinds[i] = layout.KindAt(i);
                junctions[i] = layout.IsJunction(i);
            }

            for (int i = 0; i < spaceCount; i++)
            {
                Vector3 onward = points[layout.ExitsOf(i)[0]] - points[i];
                onward.y = 0f;
                facings[i] = onward.sqrMagnitude > 0.0001f ? onward.normalized : Vector3.forward;
            }
        }

        private Bounds Measure()
        {
            Vector3 min = points[0];
            Vector3 max = points[0];

            for (int i = 1; i < spaceCount; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private void BuildSea(Bounds board)
        {
            Transform sea = new GameObject("Sea").transform;
            sea.SetParent(root, false);

            Vector3 middle = board.center;

            float widest = Mathf.Max(board.size.x, board.size.z) + TileDiameter + SeaMargin * 2f;

            Shapes.Cylinder(
                sea,
                new Vector3(middle.x, -SeaDrop, middle.z),
                new Vector3(widest, SeaThickness, widest),
                SeaBlue,
                "Water");

            Shapes.Cylinder(
                sea,
                new Vector3(middle.x, -PlatterDrop, middle.z),
                new Vector3(
                    board.size.x + TileDiameter + PlatterMargin * 2f,
                    PlatterThickness,
                    board.size.z + TileDiameter + PlatterMargin * 2f),
                PlatterLacquer,
                "Platter");
        }

        private void BuildPath(BoardLayout layout)
        {
            Transform path = new GameObject("Path").transform;
            path.SetParent(root, false);

            int next = 0;
            for (int i = 0; i < spaceCount; i++)
            {
                IReadOnlyList<int> exits = layout.ExitsOf(i);
                for (int e = 0; e < exits.Count; e++)
                {
                    links[next] = BuildLink(path, i, exits[e]);
                    next++;
                }
            }
        }

        private Link BuildLink(Transform parent, int from, int to)
        {
            Vector3 start = points[from];
            Vector3 travel = points[to] - start;
            float distance = travel.magnitude;
            Vector3 direction = distance > 0.0001f ? travel / distance : Vector3.forward;

            float leaving = TileReach(from);
            float arriving = TileReach(to);

            float run = Mathf.Max(distance - leaving - arriving, MinimumRun);
            Vector3 middle = start + direction * ((leaving + distance - arriving) * 0.5f);

            Link link = new Link
            {
                From = from,
                To = to,
            };

            link.Mat = Shapes.Cube(
                parent,
                middle + new Vector3(0f, -ConnectorDrop, 0f),
                new Vector3(ConnectorWidth, ConnectorHeight, run),
                PathMat,
                $"Link_{from}_{to}");
            link.Mat.localRotation = Quaternion.LookRotation(direction, Vector3.up);

            Vector3 tip = start + direction * (distance - arriving - ArrowSetback);

            link.ArrowLeft = BuildArrowArm(
                parent, tip, Quaternion.Euler(0f, ArrowSweep, 0f) * -direction, $"Arrow_{from}_{to}_L");
            link.ArrowRight = BuildArrowArm(
                parent, tip, Quaternion.Euler(0f, -ArrowSweep, 0f) * -direction, $"Arrow_{from}_{to}_R");

            return link;
        }

        private static Transform BuildArrowArm(Transform parent, Vector3 tip, Vector3 armDirection, string name)
        {
            Transform arm = Shapes.Cube(
                parent,
                tip + armDirection * (ArrowLength * 0.5f) + new Vector3(0f, -ConnectorDrop, 0f),
                new Vector3(ArrowWidth, ConnectorHeight, ArrowLength),
                PathArrow,
                name);

            arm.localRotation = Quaternion.LookRotation(armDirection, Vector3.up);
            return arm;
        }

        private void BuildSpaces()
        {
            Transform track = new GameObject("Spaces").transform;
            track.SetParent(root, false);

            for (int i = 0; i < spaceCount; i++)
            {
                Vector3 centre = points[i];
                bool junction = junctions[i];
                float diameter = TileDiameter * (junction ? JunctionScale : 1f);

                float spill = junction ? JunctionSpill : RimSpill;

                Shapes.Cylinder(
                    track,
                    centre + new Vector3(0f, -TileHalfHeight - RimDrop, 0f),
                    new Vector3(diameter + spill, TileHalfHeight, diameter + spill),
                    junction ? PathArrow : TileRim,
                    junction ? $"Junction_{i}" : $"Rim_{i}");

                tiles[i] = Shapes.Create(
                    PrimitiveType.Cylinder,
                    track,
                    centre + new Vector3(0f, -TileHalfHeight, 0f),
                    new Vector3(diameter, TileHalfHeight, diameter),
                    ColorFor(kinds[i]),
                    $"Space_{i}",
                    Glows(kinds[i]));
            }
        }

        private void BuildTokens()
        {
            CreateToken(BoardSeat.One, MatchSetup.SlotOneColor, TokenRadius, "Token_One", isChef: false);
            CreateToken(BoardSeat.Two, MatchSetup.SlotTwoColor, TokenRadius, "Token_Two", isChef: false);

            CreateToken(BoardSeat.Chef, Palette.TakoPurple, ChefRadius, "Token_ChefTako", isChef: true);
        }

        private void CreateToken(BoardSeat seat, Color color, float radius, string name, bool isChef)
        {
            int index = (int)seat;

            tokens[index] = Shapes.Octopus(root, color, radius, name, isChef);
            bodies[index] = tokens[index].Find("Body");
            baseTints[index] = color;

            animations[index] = CharacterAnimation.Attach(tokens[index]);

            PlaceToken(seat, 0);
        }

        public Vector3 WorldPositionOf(int spaceIndex)
        {
            return root.TransformPoint(points[Clamp(spaceIndex)] + new Vector3(0f, TokenLift, 0f));
        }

        public Transform TokenFor(BoardSeat seat)
        {
            return tokens[(int)seat];
        }

        public CharacterAnimation AnimationFor(BoardSeat seat)
        {
            return animations[(int)seat];
        }

        public void PlaceToken(BoardSeat seat, int spaceIndex)
        {
            tokenSpaces[(int)seat] = Clamp(spaceIndex);
            ApplyToken(seat);
        }

        public void SetTokenHighlight(BoardSeat seat, bool active)
        {
            int index = (int)seat;
            highlighted[index] = active;
            ApplyToken(seat);

            if (bodies[index] != null)
            {
                Shapes.Tint(bodies[index], active ? Brighten(baseTints[index]) : baseTints[index], active);
            }
        }

        public void HighlightExit(int fromSpace, int toSpace, bool active)
        {
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].From != fromSpace || links[i].To != toSpace)
                {
                    continue;
                }

                SetLink(i, active);
                return;
            }
        }

        public void ClearExitHighlights()
        {
            for (int i = 0; i < links.Length; i++)
            {
                SetLink(i, false);
            }
        }

        public void RefreshSpaces(BoardLayout layout)
        {
            if (layout == null)
            {
                return;
            }

            int shared = Mathf.Min(spaceCount, layout.Count);
            for (int i = 0; i < shared; i++)
            {
                kinds[i] = layout.KindAt(i);
                ApplyTileTint(i);
            }
        }

        private void ApplyToken(BoardSeat seat)
        {
            int index = (int)seat;
            Transform token = tokens[index];
            if (token == null)
            {
                return;
            }

            int space = tokenSpaces[index];
            Vector3 stand = points[space] + SeatNudge(seat);
            stand.y = TokenLift + (highlighted[index] ? HighlightLift : 0f);

            token.localPosition = stand;

            token.localRotation = Quaternion.LookRotation(facings[space], Vector3.up);
        }

        private void SetLink(int index, bool active)
        {
            if (links[index].Lit == active)
            {
                return;
            }

            links[index].Lit = active;

            Color arrow = active ? Brighten(PathArrow) : PathArrow;
            Shapes.Tint(links[index].Mat, active ? Brighten(PathArrow) : PathMat, active);
            Shapes.Tint(links[index].ArrowLeft, arrow, active);
            Shapes.Tint(links[index].ArrowRight, arrow, active);

            int destination = links[index].To;
            tileHighlights[destination] = Mathf.Max(0, tileHighlights[destination] + (active ? 1 : -1));
            ApplyTileTint(destination);
        }

        private void ApplyTileTint(int index)
        {
            SpaceKind kind = kinds[index];
            Color colour = ColorFor(kind);
            bool lit = tileHighlights[index] > 0;

            Shapes.Tint(tiles[index], lit ? Brighten(colour) : colour, lit || Glows(kind));
        }

        private float TileReach(int index)
        {
            return TileDiameter * 0.5f * (junctions[index] ? JunctionScale : 1f);
        }

        private static Vector3 SeatNudge(BoardSeat seat)
        {
            float turn = (int)seat / (float)SeatCount * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(turn) * ShareNudge, 0f, Mathf.Cos(turn) * ShareNudge);
        }

        private static Color ColorFor(SpaceKind kind)
        {
            return kind switch
            {
                SpaceKind.Start => Palette.ChefWhite,
                SpaceKind.CoinGain => CoinOrange,
                SpaceKind.CoinLoss => Palette.TakoPurple,
                SpaceKind.Shrine => ShrineGold,
                SpaceKind.Wasabi => Palette.WasabiGreen,
                SpaceKind.Swap => OceanCyan,
                _ => Palette.Steel,
            };
        }

        private static bool Glows(SpaceKind kind)
        {
            return kind == SpaceKind.Shrine;
        }

        private static Color Brighten(Color color)
        {
            return Color.Lerp(color, Color.white, HighlightBrighten);
        }

        private int Clamp(int spaceIndex)
        {
            return spaceIndex < 0 ? 0 : (spaceIndex >= spaceCount ? spaceCount - 1 : spaceIndex);
        }
    }
}
