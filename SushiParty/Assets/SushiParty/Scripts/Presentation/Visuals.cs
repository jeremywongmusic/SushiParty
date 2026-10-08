using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Presentation
{
    public static class Palette
    {
        private static readonly Dictionary<int, Material> SolidCache = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> GlowCache = new Dictionary<int, Material>();
        private static Shader litShader;
        public static readonly Color Sand = new Color(0.90f, 0.78f, 0.48f);
        public static readonly Color SandDark = new Color(0.72f, 0.59f, 0.33f);
        public static readonly Color Steel = new Color(0.45f, 0.48f, 0.54f);
        public static readonly Color Danger = new Color(1f, 0.85f, 0.20f);
        public static readonly Color TakoPurple = new Color(0.60f, 0.24f, 0.52f);
        public static readonly Color ChefWhite = new Color(0.97f, 0.97f, 0.94f);
        public static readonly Color RiceWhite = new Color(0.97f, 0.96f, 0.90f);
        public static readonly Color WasabiGreen = new Color(0.55f, 0.80f, 0.28f);
        public static readonly Color Shadow = new Color(0.06f, 0.07f, 0.10f);

        private static Shader LitShader
        {
            get
            {
                if (litShader == null)
                {
                    litShader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Sprites/Default");
                }

                return litShader;
            }
        }

        public static Material Solid(Color color)
        {
            int key = ColorKey(color);
            if (SolidCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(LitShader) { name = $"SP_Solid_{key:X8}" };
            ApplyColor(material, color);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.25f);
            }

            SolidCache[key] = material;
            return material;
        }

        public static Material Glow(Color color, float intensity = 2.2f)
        {
            int key = ColorKey(color) ^ Mathf.RoundToInt(intensity * 32f) << 24;
            if (GlowCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(LitShader) { name = $"SP_Glow_{key:X8}" };
            ApplyColor(material, color);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * intensity);
            }

            GlowCache[key] = material;
            return material;
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static int ColorKey(Color c)
        {
            return (Mathf.RoundToInt(c.r * 255f) << 24)
                   | (Mathf.RoundToInt(c.g * 255f) << 16)
                   | (Mathf.RoundToInt(c.b * 255f) << 8)
                   | Mathf.RoundToInt(c.a * 255f);
        }
    }

    public static class Shapes
    {
        public static Transform Create(
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            string name = null,
            bool glow = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            go.GetComponent<MeshRenderer>().sharedMaterial = glow ? Palette.Glow(color) : Palette.Solid(color);

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = localScale;
            return t;
        }

        public static Transform Cube(Transform parent, Vector3 pos, Vector3 scale, Color color, string name = null)
        {
            return Create(PrimitiveType.Cube, parent, pos, scale, color, name);
        }

        public static Transform Cylinder(Transform parent, Vector3 pos, Vector3 scale, Color color, string name = null)
        {
            return Create(PrimitiveType.Cylinder, parent, pos, scale, color, name);
        }

        public static Transform Sphere(Transform parent, Vector3 pos, float diameter, Color color, string name = null)
        {
            return Create(PrimitiveType.Sphere, parent, pos, Vector3.one * diameter, color, name);
        }

        public static void Tint(Transform shape, Color color, bool glow = false)
        {
            if (shape == null)
            {
                return;
            }

            MeshRenderer renderer = shape.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = glow ? Palette.Glow(color) : Palette.Solid(color);
            }
        }

        public static Transform Octopus(
            Transform parent,
            Color color,
            float radius,
            string name,
            bool isChef = false,
            bool animated = false)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            Sphere(t, new Vector3(0f, radius * 0.95f, 0f), radius * 1.75f, color, "Body");

            BuildTentacles(t, color, radius);
            BuildEyes(t, radius);

            if (isChef)
            {
                Cylinder(t, new Vector3(0f, radius * 1.80f, 0f),
                    new Vector3(radius * 1.20f, radius * 0.16f, radius * 1.20f), Palette.ChefWhite, "HatBrim");
                Sphere(t, new Vector3(0f, radius * 2.25f, 0f), radius * 1.45f, Palette.ChefWhite, "HatPuff");
            }

            if (animated)
            {
                CharacterAnimation.Attach(t);
            }

            return t;
        }

        private static void BuildTentacles(Transform parent, Color color, float radius)
        {
            Transform tentacles = new GameObject("Tentacles").transform;
            tentacles.SetParent(parent, false);

            const int count = 6;
            Color tentacleColor = Color.Lerp(color, Color.black, 0.20f);

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                Transform tentacle = Cube(
                    tentacles,
                    outward * (radius * 0.60f) + new Vector3(0f, radius * 0.20f, 0f),
                    new Vector3(radius * 0.36f, radius * 0.40f, radius * 0.36f),
                    tentacleColor,
                    $"Tentacle{i}");

                tentacle.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
        }

        private static void BuildEyes(Transform parent, float radius)
        {
            float height = radius * 1.15f;
            float spread = radius * 0.40f;
            float depth = radius * 0.70f;

            for (int side = -1; side <= 1; side += 2)
            {
                Sphere(parent, new Vector3(spread * side, height, depth), radius * 0.54f, Color.white, "Eye");
                Sphere(parent, new Vector3(spread * side, height, depth + radius * 0.17f), radius * 0.27f,
                    new Color(0.07f, 0.06f, 0.10f), "Pupil");
            }
        }

        public static Transform Wok(Transform parent, Vector3 localPosition, float diameter, string name = "Wok")
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            Cylinder(root.transform, Vector3.zero,
                new Vector3(diameter, diameter * 0.16f, diameter), new Color(0.20f, 0.18f, 0.22f), "Pan");
            Cylinder(root.transform, new Vector3(0f, diameter * 0.14f, 0f),
                new Vector3(diameter * 1.10f, diameter * 0.04f, diameter * 1.10f), new Color(0.58f, 0.55f, 0.62f), "Rim");

            return root.transform;
        }
    }
}
