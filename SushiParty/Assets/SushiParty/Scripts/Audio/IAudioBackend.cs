using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Audio
{
    public enum AudioPlayback
    {
        Stopped = 0,

        Starting = 1,

        Playing = 2,

        Sustaining = 3,

        Stopping = 4,
    }

    public interface IAudioBackend
    {
        void OnSceneReady();

        void PlayOneShot(string path, Vector3? position, AudioParameter parameter);

        int StartLoop(string path, Vector3? position);

        void SetLoopParameter(int id, AudioParameter parameter);

        void SetLoopPosition(int id, Vector3 position);

        AudioPlayback GetLoopState(int id);

        void StopLoop(int id);

        void StopAll();

        void SetGlobal(string parameter, float value);

        void SetGlobalLabel(string parameter, string label);
    }

    public sealed class SilentAudioBackend : IAudioBackend
    {
        private readonly HashSet<int> loops = new HashSet<int>();
        private int nextId = 1;

        public void OnSceneReady()
        {
        }

        public void PlayOneShot(string path, Vector3? position, AudioParameter parameter)
        {
            if (!GameAudio.LogEvents)
            {
                return;
            }

            Debug.Log(parameter.IsValid
                ? $"[audio] one-shot {path} ({parameter})"
                : $"[audio] one-shot {path}");
        }

        public int StartLoop(string path, Vector3? position)
        {
            int id = nextId++;
            loops.Add(id);

            if (GameAudio.LogEvents)
            {
                Debug.Log($"[audio] loop start {path} (#{id})");
            }

            return id;
        }

        public void SetLoopParameter(int id, AudioParameter parameter)
        {
        }

        public void SetLoopPosition(int id, Vector3 position)
        {
        }

        public AudioPlayback GetLoopState(int id)
        {
            return loops.Contains(id) ? AudioPlayback.Playing : AudioPlayback.Stopped;
        }

        public void StopLoop(int id)
        {
            if (loops.Remove(id) && GameAudio.LogEvents)
            {
                Debug.Log($"[audio] loop stop (#{id})");
            }
        }

        public void StopAll()
        {
            loops.Clear();
        }

        public void SetGlobal(string parameter, float value)
        {
            if (GameAudio.LogEvents)
            {
                Debug.Log($"[audio] global {parameter} = {value:0.00}");
            }
        }

        public void SetGlobalLabel(string parameter, string label)
        {
            if (GameAudio.LogEvents)
            {
                Debug.Log($"[audio] global {parameter} = \"{label}\"");
            }
        }
    }
}
