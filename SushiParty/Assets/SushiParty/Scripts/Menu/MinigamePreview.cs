using SushiParty.Core;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Menu
{
    public static class MinigamePreview
    {
        public const string ResourceFolder = "Previews/";
        private static readonly Color Deep = new Color(0.10f, 0.14f, 0.28f);
        private static readonly Color Mid = new Color(0.24f, 0.30f, 0.50f);
        private const float BehindDim = 0.5f;
        private static readonly Color SeatOne = MatchSetup.SlotOneColor;
        private static readonly Color SeatTwo = MatchSetup.SlotTwoColor;

        public static void Build(RectTransform host, MinigameDefinition definition)
        {
            UiKit.RoundedPanel(host, "Backdrop", Deep).Rt().Stretch();

            Texture2D shot = Resources.Load<Texture2D>(ResourceFolder + definition.SceneName);
            if (shot != null)
            {
                GameObject go = new GameObject("Screenshot", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(host, false);
                RawImage raw = go.GetComponent<RawImage>();
                raw.texture = shot;
                raw.raycastTarget = false;
                raw.Rt().Stretch();
                return;
            }

            Draw(host, definition);
        }

        private static void Draw(RectTransform host, MinigameDefinition definition)
        {
            switch (definition.Id)
            {
                case MinigameId.BumperSparks: BumperSparks(host, definition.Accent); break;
                case MinigameId.SandTrap: SandTrap(host); break;
                case MinigameId.CrossfireCaverns: CrossfireCaverns(host); break;
                case MinigameId.PairOfAces: PairOfAces(host); break;
                case MinigameId.ZoomRoom: ZoomRoom(host); break;
                case MinigameId.PedalToThePaddle: PedalToThePaddle(host); break;
                default: Box(host, 0.3f, 0.4f, 0.7f, 0.6f, Mid); break;
            }
        }

        private static void BumperSparks(RectTransform host, Color accent)
        {
            Circle(host, 0.5f, 0.5f, 0.78f, accent, hollow: true);
            Circle(host, 0.5f, 0.5f, 0.62f, Mid);
            Circle(host, 0.36f, 0.42f, 0.14f, SeatOne);
            Circle(host, 0.64f, 0.40f, 0.14f, SeatTwo);
            Circle(host, 0.52f, 0.64f, 0.16f, Palette.TakoPurple);
        }

        private static void SandTrap(RectTransform host)
        {
            const int size = 5;
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    if (row == 1 && column == 3)
                    {
                        continue;
                    }

                    float x = 0.18f + column * 0.13f;
                    float y = 0.18f + row * 0.13f;
                    Box(host, x, y, x + 0.11f, y + 0.11f,
                        (row + column) % 2 == 0 ? Palette.Sand : Palette.SandDark);
                }
            }

            Circle(host, 0.44f, 0.57f, 0.15f, Palette.TakoPurple);
            Box(host, 0.05f, 0.18f, 0.13f, 0.83f, SeatOne);
            Box(host, 0.18f, 0.05f, 0.83f, 0.13f, SeatTwo);
        }

        private static void CrossfireCaverns(RectTransform host)
        {
            Box(host, 0.06f, 0.76f, 0.94f, 0.82f, Mid);
            Box(host, 0.06f, 0.18f, 0.94f, 0.24f, Mid);

            Circle(host, 0.30f, 0.79f, 0.16f, SeatOne);
            Circle(host, 0.68f, 0.21f, 0.16f, SeatTwo);
            Circle(host, 0.52f, 0.50f, 0.20f, Palette.TakoPurple);

            Circle(host, 0.34f, 0.62f, 0.07f, Palette.RiceWhite);
            Circle(host, 0.64f, 0.36f, 0.07f, Palette.RiceWhite);
            Box(host, 0.14f, 0.44f, 0.20f, 0.58f, new Color(0.42f, 0.38f, 0.44f));
            Box(host, 0.80f, 0.40f, 0.86f, 0.54f, new Color(0.42f, 0.38f, 0.44f));
        }

        private static void PairOfAces(RectTransform host)
        {
            Box(host, 0f, 0.22f, 1f, 1f, new Color(0.12f, 0.16f, 0.24f));
            Box(host, 0f, 0f, 1f, 0.20f, Mid);

            Circle(host, 0.58f, 0.68f, 0.22f, Palette.TakoPurple);
            Box(host, 0.46f, 0.56f, 0.70f, 0.62f, new Color(0.20f, 0.18f, 0.22f));

            Circle(host, 0.24f, 0.24f, 0.16f, SeatOne);
            Circle(host, 0.74f, 0.24f, 0.16f, SeatTwo);
            Circle(host, 0.38f, 0.46f, 0.08f, Palette.RiceWhite);
            Circle(host, 0.68f, 0.40f, 0.10f, Palette.WasabiGreen);
        }

        private static void ZoomRoom(RectTransform host)
        {
            float[,] walls =
            {
                { 0.20f, 0.30f, 0.28f, 0.80f },
                { 0.38f, 0.16f, 0.46f, 0.58f },
                { 0.56f, 0.42f, 0.64f, 0.86f },
                { 0.72f, 0.20f, 0.80f, 0.66f },
                { 0.28f, 0.72f, 0.58f, 0.80f },
            };

            for (int i = 0; i < walls.GetLength(0); i++)
            {
                Box(host, walls[i, 0], walls[i, 1], walls[i, 2], walls[i, 3],
                    new Color(0.34f, 0.32f, 0.42f));
            }

            Circle(host, 0.11f, 0.24f, 0.14f, SeatOne);
            Circle(host, 0.89f, 0.80f, 0.14f, SeatTwo);
            Circle(host, 0.50f, 0.30f, 0.15f, Palette.TakoPurple);
        }

        private static void PedalToThePaddle(RectTransform host)
        {
            Box(host, 0f, 0f, 1f, 0.30f, new Color(0.15f, 0.32f, 0.45f));

            Circle(host, 0.42f, 0.52f, 0.62f, new Color(0.80f, 0.62f, 0.38f), hollow: true);
            Circle(host, 0.42f, 0.52f, 0.14f, new Color(0.48f, 0.45f, 0.52f));

            Circle(host, 0.60f, 0.76f, 0.14f, SeatOne);
            Circle(host, 0.26f, 0.70f, 0.14f, SeatTwo);
            Circle(host, 0.86f, 0.36f, 0.15f, Palette.TakoPurple);
        }

        private static Image Box(RectTransform host, float x0, float y0, float x1, float y1, Color color)
        {
            Image image = UiKit.Panel(host, "Box", color);
            RectTransform rect = image.Rt();
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static Image Circle(RectTransform host, float cx, float cy, float diameter, Color color, bool hollow = false)
        {
            Image image = UiKit.Disc(host, "Disc", color, hollow);
            RectTransform rect = image.Rt();
            rect.anchorMin = new Vector2(cx, cy);
            rect.anchorMax = new Vector2(cx, cy);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * (diameter * PreviewHeight);
            return image;
        }

        public const float PreviewHeight = 150f;
    }
}
