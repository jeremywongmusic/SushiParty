// PROTOTYPE — throwaway. See README.md.
using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Prototypes.ShaderLook
{
    public enum PrototypeFlourishShape
    {
        Ring = 0,
        Mote = 1,
        Beam = 2,
        Star = 3,
    }

    public sealed class PrototypeFlourishes
    {
        private const int PoolSize = 128;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");

        private sealed class Flourish
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age;
            public float Life;
            public float StartSize;
            public float EndSize;
            public float FixedHeight;
            public Vector3 Drift;
            public bool Live;
        }

        private readonly List<Flourish> pool = new List<Flourish>(PoolSize);
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Transform root;
        private readonly Material material;
        private readonly Mesh quad;
        private int nextToRecycle;

        public int LiveCount { get; private set; }

        public bool Available => material != null;

        public PrototypeFlourishes(Transform parent)
        {
            root = new GameObject("Flourishes").transform;
            root.SetParent(parent, false);

            Shader shader = Shader.Find("SushiParty/Prototype/Flourish");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[Prototype/ShaderLook] SushiParty/Prototype/Flourish is missing — " +
                    "the effects layer is off. The skins still work.");
                return;
            }

            material = new Material(shader) { name = "SP_Proto_Flourish" };
            quad = BuildQuad();
        }

        private static Mesh BuildQuad()
        {
            Mesh mesh = new Mesh { name = "SP_Proto_Quad" };

            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            });

            mesh.SetNormals(new List<Vector3>
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            });

            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        public void Ring(Vector3 position, Color colour, float size, float life, float emission)
        {
            Spawn(PrototypeFlourishShape.Ring, position, colour, size, size, life, emission, Vector3.zero);
        }

        public void Star(Vector3 position, Color colour, float size, float life, float emission)
        {
            Spawn(PrototypeFlourishShape.Star, position, colour, size, size * 1.35f, life, emission, Vector3.zero);
        }

        public void Beam(Vector3 position, Color colour, float width, float height, float life, float emission)
        {
            Flourish flourish = Take();
            if (flourish == null)
            {
                return;
            }

            Configure(
                flourish,
                PrototypeFlourishShape.Beam,
                position + Vector3.up * (height * 0.5f),
                colour,
                width,
                width,
                life,
                emission,
                Vector3.zero);

            flourish.FixedHeight = height;
            flourish.Transform.localScale = new Vector3(width, height, 1f);
        }

        public void Burst(Vector3 position, Color colour, int count, float size, float life, float emission)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 drift = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.2f, 1.4f),
                    Random.Range(-1f, 1f)).normalized * Random.Range(1.2f, 3.4f);

                Spawn(
                    PrototypeFlourishShape.Mote,
                    position + drift * 0.06f,
                    colour,
                    size * Random.Range(0.5f, 1.1f),
                    size * Random.Range(0.15f, 0.4f),
                    life * Random.Range(0.7f, 1.25f),
                    emission,
                    drift);
            }
        }

        private void Spawn(
            PrototypeFlourishShape shape,
            Vector3 position,
            Color colour,
            float startSize,
            float endSize,
            float life,
            float emission,
            Vector3 drift)
        {
            Flourish flourish = Take();
            if (flourish == null)
            {
                return;
            }

            Configure(flourish, shape, position, colour, startSize, endSize, life, emission, drift);
        }

        private void Configure(
            Flourish flourish,
            PrototypeFlourishShape shape,
            Vector3 position,
            Color colour,
            float startSize,
            float endSize,
            float life,
            float emission,
            Vector3 drift)
        {
            flourish.Transform.position = position;
            flourish.Transform.localScale = Vector3.one * startSize;
            flourish.StartSize = startSize;
            flourish.EndSize = endSize;
            flourish.FixedHeight = 0f;
            flourish.Drift = drift;
            flourish.Age = 0f;
            flourish.Life = Mathf.Max(0.05f, life);
            flourish.Live = true;
            flourish.Transform.gameObject.SetActive(true);

            block.Clear();
            block.SetColor(BaseColorId, colour);
            block.SetFloat(EmissionId, emission);
            block.SetFloat(ModeId, (float)shape);
            block.SetFloat(ProgressId, 0f);
            flourish.Renderer.SetPropertyBlock(block);
        }

        private Flourish Take()
        {
            if (material == null)
            {
                return null;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Live)
                {
                    return pool[i];
                }
            }

            if (pool.Count < PoolSize)
            {
                return Create();
            }

            nextToRecycle = (nextToRecycle + 1) % pool.Count;
            return pool[nextToRecycle];
        }

        private Flourish Create()
        {
            GameObject go = new GameObject("Flourish" + pool.Count.ToString("00"));
            go.transform.SetParent(root, false);
            go.SetActive(false);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = quad;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            Flourish flourish = new Flourish
            {
                Transform = go.transform,
                Renderer = renderer,
            };

            pool.Add(flourish);
            return flourish;
        }

        public void Tick(float deltaTime)
        {
            int live = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                Flourish flourish = pool[i];
                if (!flourish.Live)
                {
                    continue;
                }

                flourish.Age += deltaTime;
                float progress = Mathf.Clamp01(flourish.Age / flourish.Life);

                if (progress >= 1f)
                {
                    flourish.Live = false;
                    flourish.Transform.gameObject.SetActive(false);
                    continue;
                }

                live++;

                float eased = 1f - (1f - progress) * (1f - progress);
                float size = Mathf.LerpUnclamped(flourish.StartSize, flourish.EndSize, eased);

                flourish.Transform.localScale = flourish.FixedHeight > 0f
                    ? new Vector3(flourish.StartSize, flourish.FixedHeight, 1f)
                    : Vector3.one * size;

                if (flourish.Drift != Vector3.zero)
                {
                    flourish.Transform.position += flourish.Drift * deltaTime;
                    flourish.Drift += Vector3.down * (9f * deltaTime);
                }

                flourish.Renderer.GetPropertyBlock(block);
                block.SetFloat(ProgressId, progress);
                flourish.Renderer.SetPropertyBlock(block);
            }

            LiveCount = live;
        }

        public void Clear()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].Live = false;
                if (pool[i].Transform != null)
                {
                    pool[i].Transform.gameObject.SetActive(false);
                }
            }

            LiveCount = 0;
        }
    }
}
