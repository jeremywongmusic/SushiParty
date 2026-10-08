using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Presentation
{
    public static class UiKit
    {
        private static Font builtinFont;

        public static Font Font
        {
            get
            {
                if (builtinFont != null)
                {
                    return builtinFont;
                }

                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtinFont == null)
                {
                    builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                    Debug.LogWarning("[SushiParty] Built-in LegacyRuntime.ttf missing; fell back to an OS font.");
                }

                return builtinFont;
            }
        }

        public static readonly Color Ink = new Color(0.99f, 0.99f, 1f);
        public static readonly Color InkDim = new Color(0.78f, 0.83f, 0.93f);
        public static readonly Color Backdrop = new Color(0.07f, 0.09f, 0.20f, 0.92f);
        public static readonly Color CardIdle = new Color(0.19f, 0.24f, 0.42f, 0.96f);
        public static readonly Color PartyBlue = new Color(0.11f, 0.15f, 0.33f);
        public static readonly Color PanelBlue = new Color(0.16f, 0.21f, 0.40f, 0.96f);
        public static readonly Color Cream = new Color(0.98f, 0.96f, 0.91f);
        public static readonly Color DarkInk = new Color(0.13f, 0.17f, 0.34f);
        public static readonly Color DarkInkDim = new Color(0.36f, 0.40f, 0.54f);
        public static readonly Color WindowEdge = new Color(1f, 1f, 1f);
        public static readonly Color Success = new Color(0.11f, 0.62f, 0.26f);
        public static readonly Color Failure = new Color(0.85f, 0.20f, 0.24f);
        private static Sprite discSprite;
        private static Sprite ringSprite;
        private static Sprite roundedSprite;

        public static Sprite RoundedSprite => roundedSprite != null
            ? roundedSprite
            : roundedSprite = BuildRoundedRect();

        private static Sprite BuildRoundedRect()
        {
            const int size = 64;
            const int radius = 22;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                    float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        public static Sprite DiscSprite => discSprite != null
            ? discSprite
            : discSprite = BuildCircle(innerRadiusFraction: 0f);

        public static Sprite RingSprite => ringSprite != null
            ? ringSprite
            : ringSprite = BuildCircle(innerRadiusFraction: 0.68f);

        private static Sprite BuildCircle(float innerRadiusFraction)
        {
            const int size = 64;
            float outer = size * 0.5f - 0.5f;
            float inner = outer * innerRadiusFraction;
            Vector2 centre = new Vector2(outer, outer);

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), centre);
                    float alpha = Mathf.Clamp01(outer - distance + 0.5f);

                    if (inner > 0f)
                    {
                        alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - inner + 0.5f));
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null, float match = 0.5f)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = match;

            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static Text Chunky(this Text text, float thickness = 2.6f, Color? outlineColor = null)
        {
            UnityEngine.UI.Outline outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = outlineColor ?? DarkInk;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = false;

            UnityEngine.UI.Shadow shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
            shadow.effectDistance = new Vector2(0f, -thickness * 1.3f);
            shadow.useGraphicAlpha = false;

            return text;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Disc(Transform parent, string name, Color color, bool hollow = false)
        {
            Image image = Panel(parent, name, color);
            image.sprite = hollow ? RingSprite : DiscSprite;
            image.type = Image.Type.Simple;
            return image;
        }

        public static Image RoundedPanel(Transform parent, string name, Color color)
        {
            Image image = Panel(parent, name, color);
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        public static void Window(RectTransform host, Color body, float edge = 6f, float shadow = 10f)
        {
            Image drop = RoundedPanel(host, "Shadow", new Color(0f, 0f, 0f, 0.32f));
            drop.Rt().Stretch(-edge);
            drop.Rt().offsetMin += new Vector2(shadow * 0.35f, -shadow);
            drop.Rt().offsetMax += new Vector2(shadow * 0.35f, -shadow);

            RoundedPanel(host, "Edge", WindowEdge).Rt().Stretch(-edge);
            RoundedPanel(host, "Body", body).Rt().Stretch();
        }

        public static Text Banner(RectTransform host, string caption, Color fill, int fontSize, float width)
        {
            RectTransform banner = Rect(host, "Banner");
            banner.Pin(new Vector2(0.5f, 1f), new Vector2(width, fontSize + 30f), new Vector2(0f, (fontSize + 30f) * 0.45f));

            RoundedPanel(banner, "Edge", WindowEdge).Rt().Stretch(-5f);
            RoundedPanel(banner, "Fill", fill).Rt().Stretch();

            Text text = Label(banner, caption, fontSize, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            text.Rt().Stretch();
            return text;
        }

        public static Text Pill(Transform parent, string caption, Color fill, Color ink, int fontSize, Vector2 size)
        {
            RectTransform pill = Rect(parent, "Pill");
            pill.sizeDelta = size;

            RoundedPanel(pill, "Fill", fill).Rt().Stretch();

            Text text = Label(pill, caption, fontSize, TextAnchor.MiddleCenter, ink, FontStyle.Bold);
            text.Rt().Stretch();
            return text;
        }

        public static Text Label(
            Transform parent,
            string content,
            int fontSize,
            TextAnchor anchor = TextAnchor.MiddleCenter,
            Color? color = null,
            FontStyle style = FontStyle.Normal)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color ?? Ink;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Region(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector4 padding = default)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(padding.x, padding.y);
            rt.offsetMax = new Vector2(-padding.z, -padding.w);
            return rt;
        }

        public static RectTransform Pin(this RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static RectTransform Rt(this Component component)
        {
            return (RectTransform)component.transform;
        }

        public static bool ContainsScreenPoint(this RectTransform rt, Vector2 screenPoint)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);
        }
    }
}
