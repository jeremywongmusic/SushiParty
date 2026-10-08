using UnityEngine;
using UnityEngine.SceneManagement;

using MixerCatalogue = SushiParty.Audio.Mixer;

namespace SushiParty.Audio
{
    public static class GameAudio
    {
        private static readonly System.Collections.Generic.Dictionary<string, float> lastGlobals =
            new System.Collections.Generic.Dictionary<string, float>();

        private static readonly System.Collections.Generic.Dictionary<string, string> lastGlobalLabels =
            new System.Collections.Generic.Dictionary<string, string>();

        private static IAudioBackend backend;
        private static IAudioMixer mixer;
        private static MixerSettings settings;
        private static PauseController pause;

        public static bool LogEvents { get; set; }

        public static IAudioMixer Mixer
        {
            get
            {
                if (mixer == null)
                {
                    Initialize();
                }

                return mixer;
            }
        }

        public static MixerSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    Initialize();
                }

                return settings;
            }
        }

        public static PauseController Pause
        {
            get
            {
                if (pause == null)
                {
                    Initialize();
                }

                return pause;
            }
        }

        public static bool IsLive
        {
            get
            {
#if SUSHIPARTY_FMOD
                return true;
#else
                return false;
#endif
            }
        }

        private static IAudioBackend Backend
        {
            get
            {
                if (backend == null)
                {
                    Initialize();
                }

                return backend;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (backend != null)
            {
                return;
            }

#if SUSHIPARTY_FMOD
            backend = new FmodAudioBackend();
            mixer = new FmodAudioMixer();
#else
            backend = new SilentAudioBackend();
            mixer = new SilentAudioMixer();
#endif

            settings = new MixerSettings(MixerCatalogue.All, mixer);
            pause = new PauseController(mixer, MixerCatalogue.PausedByThePauseMenu, new GameAudioGlobals());

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            OnSceneReady();
        }

        public static void OnSceneReady()
        {
            Backend.StopAll();

            Pause.StopEverything();

            Backend.OnSceneReady();

            // Before Apply(): Initialize() runs ahead of the first bank load, so the mixer
            // has cached a miss on every bus.
            Mixer.Invalidate();

            Settings.Apply();

            lastGlobals.Clear();
            lastGlobalLabels.Clear();
        }

        public static void Play(AudioEvent sound)
        {
            if (sound.IsValid)
            {
                Backend.PlayOneShot(sound.Path, null, AudioParameter.None);
            }
        }

        public static void PlayAt(AudioEvent sound, Vector3 position)
        {
            if (sound.IsValid)
            {
                Backend.PlayOneShot(sound.Path, position, AudioParameter.None);
            }
        }

        public static void Play(AudioEvent sound, string parameter, float value)
        {
            if (sound.IsValid)
            {
                Backend.PlayOneShot(sound.Path, null, AudioParameter.Number(parameter, value));
            }
        }

        public static void PlayAt(AudioEvent sound, Vector3 position, string parameter, float value)
        {
            if (sound.IsValid)
            {
                Backend.PlayOneShot(sound.Path, position, AudioParameter.Number(parameter, value));
            }
        }

        public static void PlayLabelled(AudioEvent sound, string parameter, string label)
        {
            if (sound.IsValid)
            {
                Backend.PlayOneShot(sound.Path, null, AudioParameter.Labelled(parameter, label));
            }
        }

        public static void PlayLabelledAt(AudioEvent sound, Vector3 position, string parameter, string label)
        {
            if (sound.IsValid)
            {
                Backend.PlayOneShot(sound.Path, position, AudioParameter.Labelled(parameter, label));
            }
        }

        public static AudioHandle Loop(AudioEvent sound)
        {
            return sound.IsValid ? new AudioHandle(Backend.StartLoop(sound.Path, null)) : AudioHandle.None;
        }

        public static AudioHandle LoopAt(AudioEvent sound, Vector3 position)
        {
            return sound.IsValid ? new AudioHandle(Backend.StartLoop(sound.Path, position)) : AudioHandle.None;
        }

        public static void SetParameter(
            AudioHandle handle,
            string parameter,
            float value,
            bool ignoreSeekSpeed = false)
        {
            if (handle.IsValid)
            {
                Backend.SetLoopParameter(handle.Id, AudioParameter.Number(parameter, value, ignoreSeekSpeed));
            }
        }

        public static void SetParameterLabel(AudioHandle handle, string parameter, string label)
        {
            if (handle.IsValid)
            {
                Backend.SetLoopParameter(handle.Id, AudioParameter.Labelled(parameter, label));
            }
        }

        public static AudioPlayback StateOf(AudioHandle handle)
        {
            return handle.IsValid ? Backend.GetLoopState(handle.Id) : AudioPlayback.Stopped;
        }

        public static void SetPosition(AudioHandle handle, Vector3 position)
        {
            if (handle.IsValid)
            {
                Backend.SetLoopPosition(handle.Id, position);
            }
        }

        public static void Stop(ref AudioHandle handle)
        {
            if (handle.IsValid)
            {
                Backend.StopLoop(handle.Id);
                handle = AudioHandle.None;
            }
        }

        public static void StopAll()
        {
            Backend.StopAll();
        }

        public static void SetGlobal(string parameter, float value, float epsilon = 0.005f)
        {
            if (lastGlobals.TryGetValue(parameter, out float previous)
                && Mathf.Abs(previous - value) < epsilon)
            {
                return;
            }

            lastGlobals[parameter] = value;
            Backend.SetGlobal(parameter, value);
        }

        public static void SetGlobalLabel(string parameter, string label)
        {
            if (lastGlobalLabels.TryGetValue(parameter, out string previous) && previous == label)
            {
                return;
            }

            lastGlobalLabels[parameter] = label;
            Backend.SetGlobalLabel(parameter, label);
        }
    }
}
