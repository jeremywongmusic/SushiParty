// PROTOTYPE — throwaway. See README.md.
using UnityEngine;

namespace SushiParty.Prototypes.ShaderLook
{
    public enum PrototypeLook
    {
        Off = 0,
        Neon = 1,
        Ink = 2,
        Lacquer = 3,
    }

    public sealed class PrototypeLookProfile
    {
        public PrototypeLook Look;
        public string Name;
        public string Pitch;
        public string ShaderName;
        public bool Bloom;
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomScatter;
        public float Vignette;
        public float ChromaticAberration;
        public float Contrast;
        public float Saturation;
        public float PostExposure;
        public float Temperature;
        public float FilmGrain;
        public bool AcesTonemap;
        public float FlourishScale;
        public float FlourishEmission;
        public PrototypeFlourishShape LitShape;
        public PrototypeFlourishShape DeathShape;
        public float Wobble;
        public float Punch;

        public static readonly PrototypeLookProfile[] All =
        {
            new PrototypeLookProfile
            {
                Look = PrototypeLook.Off,
                Name = "Off — the game as it stands",
                Pitch = "URP Lit, no post, no bloom. Palette.Glow is emission nothing renders.",
                ShaderName = null,
                FlourishScale = 0f,
                FlourishEmission = 0f,
                LitShape = PrototypeFlourishShape.Ring,
                DeathShape = PrototypeFlourishShape.Mote,
                Wobble = 0f,
                Punch = 0f,
            },

            new PrototypeLookProfile
            {
                Look = PrototypeLook.Neon,
                Name = "A — Neon Izakaya",
                Pitch = "Lit from inside. Hard rims, blown bloom, and Palette.Glow finally paying off.",
                ShaderName = "SushiParty/Prototype/Neon",

                Bloom = true,
                BloomThreshold = 0.80f,
                BloomIntensity = 1.55f,
                BloomScatter = 0.75f,
                Vignette = 0.36f,
                ChromaticAberration = 0.14f,
                Contrast = 14f,
                Saturation = 26f,
                PostExposure = 0.10f,
                Temperature = -8f,
                FilmGrain = 0f,
                AcesTonemap = true,

                FlourishScale = 1.15f,
                FlourishEmission = 2.6f,
                LitShape = PrototypeFlourishShape.Ring,
                DeathShape = PrototypeFlourishShape.Star,
                Wobble = 0.25f,
                Punch = 0.16f,
            },

            new PrototypeLookProfile
            {
                Look = PrototypeLook.Ink,
                Name = "B — Inkbrush",
                Pitch = "No glow at all. Three flat bands, a hard outline round everything, paper grain.",
                ShaderName = "SushiParty/Prototype/Ink",

                Bloom = false,
                BloomThreshold = 1.4f,
                BloomIntensity = 0f,
                BloomScatter = 0.5f,
                Vignette = 0.20f,
                ChromaticAberration = 0f,
                Contrast = 30f,
                Saturation = -16f,
                PostExposure = 0.18f,
                Temperature = 6f,
                FilmGrain = 0.42f,
                AcesTonemap = false,

                FlourishScale = 1.35f,
                FlourishEmission = 0.85f,
                LitShape = PrototypeFlourishShape.Star,
                DeathShape = PrototypeFlourishShape.Ring,
                Wobble = 0f,
                Punch = 0.22f,
            },

            new PrototypeLookProfile
            {
                Look = PrototypeLook.Lacquer,
                Name = "C — Lacquer",
                Pitch = "Food, not arcade. Wet highlights, light through the thin parts, and jelly.",
                ShaderName = "SushiParty/Prototype/Lacquer",

                Bloom = true,
                BloomThreshold = 1.15f,
                BloomIntensity = 0.75f,
                BloomScatter = 0.62f,
                Vignette = 0.28f,
                ChromaticAberration = 0.04f,
                Contrast = 8f,
                Saturation = 10f,
                PostExposure = 0.05f,
                Temperature = 14f,
                FilmGrain = 0.10f,
                AcesTonemap = true,

                FlourishScale = 0.95f,
                FlourishEmission = 1.5f,
                LitShape = PrototypeFlourishShape.Beam,
                DeathShape = PrototypeFlourishShape.Mote,
                Wobble = 1f,
                Punch = 0.10f,
            },
        };

        public static PrototypeLookProfile For(PrototypeLook look)
        {
            int index = (int)look;
            return index >= 0 && index < All.Length ? All[index] : All[0];
        }
    }
}
