using System.Collections.Generic;
using UnityEngine;

namespace SushiParty.Audio
{
    public sealed class MixerSettings
    {
        public const float Step = 0.05f;
        private const string MuteKeySuffix = ".Muted";
        private readonly IReadOnlyList<MixerChannel> channels;
        private readonly IAudioMixer mixer;
        private readonly IPreferenceStore store;
        private readonly Dictionary<string, float> volumes = new Dictionary<string, float>();
        private readonly HashSet<string> muted = new HashSet<string>();
        private int selectedIndex;

        public MixerSettings(IReadOnlyList<MixerChannel> channels, IAudioMixer mixer, IPreferenceStore store = null)
        {
            this.channels = channels;
            this.mixer = mixer;
            this.store = store ?? new PlayerPrefsStore();

            Load();
        }

        public IReadOnlyList<MixerChannel> Channels => channels;
        public int SelectedIndex => selectedIndex;

        public MixerChannel Selected =>
            selectedIndex >= 0 && selectedIndex < channels.Count ? channels[selectedIndex] : default;

        public int IndexOf(MixerChannel channel)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].Path == channel.Path)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool Navigate(int direction)
        {
            if (channels.Count == 0 || direction == 0)
            {
                return false;
            }

            int step = direction > 0 ? 1 : -1;
            int next = ((selectedIndex + step) % channels.Count + channels.Count) % channels.Count;

            if (next == selectedIndex)
            {
                return false;
            }

            selectedIndex = next;
            return true;
        }

        public bool SelectIndex(int index)
        {
            if (index < 0 || index >= channels.Count || index == selectedIndex)
            {
                return false;
            }

            selectedIndex = index;
            return true;
        }

        public bool Nudge(int direction)
        {
            if (direction == 0 || !Selected.IsValid)
            {
                return false;
            }

            MixerChannel channel = Selected;
            float before = VolumeOf(channel);
            float after = Mathf.Clamp01(before + (direction > 0 ? Step : -Step));

            if (Mathf.Abs(after - before) < 0.0001f)
            {
                return false;
            }

            SetVolume(channel, after);
            return true;
        }

        public void SetVolume(MixerChannel channel, float volume)
        {
            if (!channel.IsValid)
            {
                return;
            }

            volumes[channel.Path] = Mathf.Clamp01(volume);
            mixer.SetVolume(channel, volumes[channel.Path]);
        }

        public float VolumeOf(MixerChannel channel)
        {
            if (!channel.IsValid)
            {
                return 0f;
            }

            return volumes.TryGetValue(channel.Path, out float volume) ? volume : channel.DefaultVolume;
        }

        public float FinalVolumeOf(MixerChannel channel)
        {
            return channel.IsValid ? mixer.GetFinalVolume(channel) : 0f;
        }

        public bool IsMuted(MixerChannel channel)
        {
            return channel.IsValid && muted.Contains(channel.Path);
        }

        public bool ToggleMute()
        {
            MixerChannel channel = Selected;
            if (!channel.IsValid || !channel.IsBus)
            {
                return false;
            }

            bool next = !IsMuted(channel);
            if (next)
            {
                muted.Add(channel.Path);
            }
            else
            {
                muted.Remove(channel.Path);
            }

            mixer.SetMuted(channel, next);
            return true;
        }

        public bool Reset()
        {
            bool changed = muted.Count > 0;

            for (int i = 0; i < channels.Count; i++)
            {
                MixerChannel channel = channels[i];
                if (Mathf.Abs(VolumeOf(channel) - channel.DefaultVolume) > 0.0001f)
                {
                    changed = true;
                }

                volumes[channel.Path] = channel.DefaultVolume;
            }

            muted.Clear();
            Apply();

            return changed;
        }

        public void Apply()
        {
            for (int i = 0; i < channels.Count; i++)
            {
                MixerChannel channel = channels[i];
                mixer.SetVolume(channel, VolumeOf(channel));

                if (channel.IsBus)
                {
                    mixer.SetMuted(channel, IsMuted(channel));
                }
            }
        }

        public void Save()
        {
            for (int i = 0; i < channels.Count; i++)
            {
                MixerChannel channel = channels[i];
                store.SetFloat(channel.PreferenceKey, VolumeOf(channel));

                if (channel.IsBus)
                {
                    store.SetBool(channel.PreferenceKey + MuteKeySuffix, IsMuted(channel));
                }
            }

            store.Save();
        }

        public void Load()
        {
            volumes.Clear();
            muted.Clear();

            for (int i = 0; i < channels.Count; i++)
            {
                MixerChannel channel = channels[i];
                volumes[channel.Path] = Mathf.Clamp01(
                    store.GetFloat(channel.PreferenceKey, channel.DefaultVolume));

                if (channel.IsBus && store.GetBool(channel.PreferenceKey + MuteKeySuffix, false))
                {
                    muted.Add(channel.Path);
                }
            }

            Apply();
        }
    }
}
