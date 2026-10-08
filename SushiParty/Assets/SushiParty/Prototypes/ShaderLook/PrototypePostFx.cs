// PROTOTYPE — throwaway. See README.md.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SushiParty.Prototypes.ShaderLook
{
    public sealed class PrototypePostFx
    {
        private readonly Volume volume;
        private readonly VolumeProfile volumeProfile;
        private readonly Bloom bloom;
        private readonly Vignette vignette;
        private readonly ChromaticAberration chromatic;
        private readonly ColorAdjustments colour;
        private readonly WhiteBalance whiteBalance;
        private readonly FilmGrain grain;
        private readonly LensDistortion distortion;
        private readonly Tonemapping tonemapping;
        private PrototypeLookProfile profile = PrototypeLookProfile.For(PrototypeLook.Off);
        private Camera touchedCamera;
        private bool cameraPostWas;
        private AntialiasingMode cameraAaWas;
        private LayerMask cameraVolumeMaskWas;
        private float shock;
        public bool Active => profile.Look != PrototypeLook.Off;

        public PrototypePostFx(Transform parent)
        {
            GameObject host = new GameObject("PostFx");
            host.transform.SetParent(parent, false);
            host.layer = 0;

            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeProfile.name = "SP_Proto_VolumeProfile";
            volumeProfile.hideFlags = HideFlags.HideAndDontSave;

            bloom = volumeProfile.Add<Bloom>(true);
            vignette = volumeProfile.Add<Vignette>(true);
            chromatic = volumeProfile.Add<ChromaticAberration>(true);
            colour = volumeProfile.Add<ColorAdjustments>(true);
            whiteBalance = volumeProfile.Add<WhiteBalance>(true);
            grain = volumeProfile.Add<FilmGrain>(true);
            distortion = volumeProfile.Add<LensDistortion>(true);
            tonemapping = volumeProfile.Add<Tonemapping>(true);

            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.weight = 0f;
            volume.sharedProfile = volumeProfile;

            Apply();
        }

        public void Use(PrototypeLookProfile next)
        {
            profile = next;
            shock = 0f;

            if (!Active)
            {
                volume.weight = 0f;
                ReleaseCamera();
                return;
            }

            volume.weight = 1f;
            Apply();
        }

        public void Punch(float strength)
        {
            shock = Mathf.Clamp01(Mathf.Max(shock, strength));
        }

        public void Tick(float deltaTime)
        {
            if (!Active)
            {
                return;
            }

            AdoptCamera();

            if (shock > 0f)
            {
                shock = Mathf.Max(0f, shock - deltaTime * 2.6f);
                Apply();
            }
        }

        private void AdoptCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || camera == touchedCamera)
            {
                return;
            }

            ReleaseCamera();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null)
            {
                return;
            }

            touchedCamera = camera;
            cameraPostWas = data.renderPostProcessing;
            cameraAaWas = data.antialiasing;
            cameraVolumeMaskWas = data.volumeLayerMask;

            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            data.volumeLayerMask = ~0;
        }

        private void ReleaseCamera()
        {
            if (touchedCamera == null)
            {
                touchedCamera = null;
                return;
            }

            UniversalAdditionalCameraData data = touchedCamera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = cameraPostWas;
                data.antialiasing = cameraAaWas;
                data.volumeLayerMask = cameraVolumeMaskWas;
            }

            touchedCamera = null;
        }

        private void Apply()
        {
            bloom.active = profile.Bloom;
            bloom.threshold.Override(profile.BloomThreshold);
            bloom.intensity.Override(profile.BloomIntensity * (1f + shock * 0.9f));
            bloom.scatter.Override(profile.BloomScatter);
            bloom.highQualityFiltering.Override(true);

            vignette.intensity.Override(profile.Vignette + shock * 0.22f);
            vignette.smoothness.Override(0.45f);

            chromatic.intensity.Override(profile.ChromaticAberration + shock * 0.45f);

            colour.postExposure.Override(profile.PostExposure + shock * 0.35f);
            colour.contrast.Override(profile.Contrast);
            colour.saturation.Override(profile.Saturation);

            whiteBalance.temperature.Override(profile.Temperature);

            grain.active = profile.FilmGrain > 0f;
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(profile.FilmGrain);
            grain.response.Override(0.8f);

            distortion.active = shock > 0.001f;
            distortion.intensity.Override(-shock * 0.28f);
            distortion.scale.Override(1f);

            tonemapping.mode.Override(profile.AcesTonemap ? TonemappingMode.ACES : TonemappingMode.Neutral);
        }

        public void Dispose()
        {
            ReleaseCamera();

            if (volumeProfile != null)
            {
                Object.Destroy(volumeProfile);
            }
        }
    }
}
