#if SUSHIPARTY_FMOD
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace SushiParty.Audio
{
    public sealed class FmodAudioBackend : IAudioBackend
    {
        private readonly Dictionary<int, EventInstance> loops = new Dictionary<int, EventInstance>();
        private readonly HashSet<string> missingEvents = new HashSet<string>();
        private int nextId = 1;

        public void OnSceneReady()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                return;
            }

            if (camera.GetComponent<StudioListener>() == null)
            {
                camera.gameObject.AddComponent<StudioListener>();
            }
        }

        public void PlayOneShot(string path, Vector3? position, AudioParameter parameter)
        {
            if (!EventExists(path))
            {
                return;
            }

            if (!parameter.IsValid)
            {
                if (position.HasValue)
                {
                    RuntimeManager.PlayOneShot(path, position.Value);
                }
                else
                {
                    RuntimeManager.PlayOneShot(path);
                }

                return;
            }

            EventInstance instance = RuntimeManager.CreateInstance(path);
            if (position.HasValue)
            {
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position.Value));
            }

            Apply(instance, parameter);
            instance.start();
            instance.release();
        }

        private static void Apply(EventInstance instance, AudioParameter parameter)
        {
            if (!parameter.IsValid)
            {
                return;
            }

            FMOD.RESULT result = parameter.IsLabelled
                ? instance.setParameterByNameWithLabel(parameter.Name, parameter.Label, parameter.IgnoreSeekSpeed)
                : instance.setParameterByName(parameter.Name, parameter.Value, parameter.IgnoreSeekSpeed);

            if (result != FMOD.RESULT.OK)
            {
                Debug.LogWarning(
                    $"[SushiParty] FMOD could not set {parameter}: {FMOD.Error.String(result)}");
            }
        }

        public int StartLoop(string path, Vector3? position)
        {
            if (!EventExists(path))
            {
                return 0;
            }

            EventInstance instance = RuntimeManager.CreateInstance(path);
            if (position.HasValue)
            {
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position.Value));
            }

            instance.start();

            int id = nextId++;
            loops[id] = instance;
            return id;
        }

        public void SetLoopParameter(int id, AudioParameter parameter)
        {
            if (loops.TryGetValue(id, out EventInstance instance) && instance.isValid())
            {
                Apply(instance, parameter);
            }
        }

        public void SetLoopPosition(int id, Vector3 position)
        {
            if (loops.TryGetValue(id, out EventInstance instance) && instance.isValid())
            {
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            }
        }

        public AudioPlayback GetLoopState(int id)
        {
            if (!loops.TryGetValue(id, out EventInstance instance) || !instance.isValid())
            {
                return AudioPlayback.Stopped;
            }

            if (instance.getPlaybackState(out PLAYBACK_STATE state) != FMOD.RESULT.OK)
            {
                return AudioPlayback.Stopped;
            }

            switch (state)
            {
                case PLAYBACK_STATE.PLAYING:
                    return AudioPlayback.Playing;
                case PLAYBACK_STATE.SUSTAINING:
                    return AudioPlayback.Sustaining;
                case PLAYBACK_STATE.STARTING:
                    return AudioPlayback.Starting;
                case PLAYBACK_STATE.STOPPING:
                    return AudioPlayback.Stopping;
                default:
                    return AudioPlayback.Stopped;
            }
        }

        public void StopLoop(int id)
        {
            if (!loops.TryGetValue(id, out EventInstance instance))
            {
                return;
            }

            loops.Remove(id);

            if (instance.isValid())
            {
                instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                instance.release();
            }
        }

        public void StopAll()
        {
            foreach (EventInstance instance in loops.Values)
            {
                if (instance.isValid())
                {
                    instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    instance.release();
                }
            }

            loops.Clear();
        }

        public void SetGlobal(string parameter, float value)
        {
            RuntimeManager.StudioSystem.setParameterByName(parameter, value);
        }

        public void SetGlobalLabel(string parameter, string label)
        {
            RuntimeManager.StudioSystem.setParameterByNameWithLabel(parameter, label);
        }

        private bool EventExists(string path)
        {
            if (missingEvents.Contains(path))
            {
                return false;
            }

            try
            {
                // Depending on the FMOD for Unity version an unknown path either throws or
                // comes back invalid.
                EventDescription description = RuntimeManager.GetEventDescription(path);
                if (description.isValid())
                {
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SushiParty] FMOD could not resolve '{path}': {exception.Message}");
            }

            missingEvents.Add(path);
            Debug.LogWarning($"[SushiParty] FMOD event '{path}' is not in any loaded bank. Muting it.");
            return false;
        }
    }
}
#endif
