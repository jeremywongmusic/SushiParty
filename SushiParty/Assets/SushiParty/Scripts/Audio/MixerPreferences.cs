using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Audio
{
    public interface IPreferenceStore
    {
        float GetFloat(string key, float fallback);

        void SetFloat(string key, float value);

        bool GetBool(string key, bool fallback);

        void SetBool(string key, bool value);

        void Save();
    }

    public sealed class PlayerPrefsStore : IPreferenceStore
    {
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class MemoryPreferenceStore : IPreferenceStore
    {
        private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();

        public int Saves { get; private set; }

        public float GetFloat(string key, float fallback)
        {
            return floats.TryGetValue(key, out float value) ? value : fallback;
        }

        public void SetFloat(string key, float value) => floats[key] = value;

        public bool GetBool(string key, bool fallback)
        {
            return bools.TryGetValue(key, out bool value) ? value : fallback;
        }

        public void SetBool(string key, bool value) => bools[key] = value;
        public void Save() => Saves++;
    }
}
