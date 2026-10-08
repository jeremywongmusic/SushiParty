using System.Collections.Generic;

namespace SushiParty.Audio
{
    public readonly struct MixerReading
    {
        public readonly bool Resolved;
        public readonly string Path;
        public readonly string Guid;
        public readonly float Volume;
        public readonly float FinalVolume;
        public readonly bool Muted;
        public readonly bool Paused;
        public readonly bool ChannelGroupLive;
        public readonly uint CpuExclusive;
        public readonly uint CpuInclusive;
        public readonly int MemoryExclusive;
        public readonly int MemoryInclusive;
        public readonly int MemorySampleData;
        public readonly ulong PortIndex;
        public readonly bool PortIndexSupported;

        public MixerReading(
            bool resolved,
            string path,
            string guid,
            float volume,
            float finalVolume,
            bool muted,
            bool paused,
            bool channelGroupLive,
            uint cpuExclusive,
            uint cpuInclusive,
            int memoryExclusive,
            int memoryInclusive,
            int memorySampleData,
            ulong portIndex,
            bool portIndexSupported)
        {
            Resolved = resolved;
            Path = path;
            Guid = guid;
            Volume = volume;
            FinalVolume = finalVolume;
            Muted = muted;
            Paused = paused;
            ChannelGroupLive = channelGroupLive;
            CpuExclusive = cpuExclusive;
            CpuInclusive = cpuInclusive;
            MemoryExclusive = memoryExclusive;
            MemoryInclusive = memoryInclusive;
            MemorySampleData = memorySampleData;
            PortIndex = portIndex;
            PortIndexSupported = portIndexSupported;
        }

        public static MixerReading Unresolved(string path)
        {
            return new MixerReading(
                false, path, string.Empty, 0f, 0f, false, false, false, 0u, 0u, 0, 0, 0, 0ul, false);
        }
    }

    public interface IAudioMixer
    {
        bool IsLive { get; }

        void SetVolume(MixerChannel channel, float volume);

        float GetVolume(MixerChannel channel);

        float GetFinalVolume(MixerChannel channel);

        void SetMuted(MixerChannel channel, bool muted);

        bool IsMuted(MixerChannel channel);

        void SetPaused(MixerChannel channel, bool paused);

        bool IsPaused(MixerChannel channel);

        void StopAllEvents(MixerChannel channel, bool allowFadeout);

        MixerReading Read(MixerChannel channel);

        void HoldChannelGroup(MixerChannel channel, bool held);

        void Invalidate();
    }

    public sealed class SilentAudioMixer : IAudioMixer
    {
        private readonly Dictionary<string, float> volumes = new Dictionary<string, float>();
        private readonly HashSet<string> muted = new HashSet<string>();
        private readonly HashSet<string> paused = new HashSet<string>();
        private readonly HashSet<string> held = new HashSet<string>();

        public int StopAllEventsCalls { get; private set; }

        public bool IsHeld(MixerChannel channel) => held.Contains(channel.Path);
        public bool IsLive => false;

        public void SetVolume(MixerChannel channel, float volume)
        {
            if (!channel.IsValid)
            {
                return;
            }

            volumes[channel.Path] = volume;
        }

        public float GetVolume(MixerChannel channel)
        {
            if (channel.IsValid && volumes.TryGetValue(channel.Path, out float volume))
            {
                return volume;
            }

            return channel.IsValid ? channel.DefaultVolume : 0f;
        }

        public float GetFinalVolume(MixerChannel channel)
        {
            if (!channel.IsValid)
            {
                return 0f;
            }

            if (muted.Contains(channel.Path))
            {
                return 0f;
            }

            float gain = GetVolume(channel);

            if (!channel.IsBus)
            {
                return gain;
            }

            if (channel.Path != Mixer.Master.Path)
            {
                gain *= GetFinalVolume(Mixer.Master);
            }

            IReadOnlyList<MixerChannel> trims = Mixer.VcasTrimming(channel);
            for (int i = 0; i < trims.Count; i++)
            {
                gain *= GetVolume(trims[i]);
            }

            return gain;
        }

        public void SetMuted(MixerChannel channel, bool muted)
        {
            if (!channel.IsValid || !channel.IsBus)
            {
                return;
            }

            if (muted)
            {
                this.muted.Add(channel.Path);
            }
            else
            {
                this.muted.Remove(channel.Path);
            }
        }

        public bool IsMuted(MixerChannel channel)
        {
            return channel.IsValid && muted.Contains(channel.Path);
        }

        public void SetPaused(MixerChannel channel, bool paused)
        {
            if (!channel.IsValid || !channel.IsBus)
            {
                return;
            }

            if (paused)
            {
                this.paused.Add(channel.Path);
            }
            else
            {
                this.paused.Remove(channel.Path);
            }
        }

        public bool IsPaused(MixerChannel channel)
        {
            return channel.IsValid && paused.Contains(channel.Path);
        }

        public void StopAllEvents(MixerChannel channel, bool allowFadeout)
        {
            if (!channel.IsValid || !channel.IsBus)
            {
                return;
            }

            StopAllEventsCalls++;
        }

        public MixerReading Read(MixerChannel channel)
        {
            if (!channel.IsValid)
            {
                return MixerReading.Unresolved(string.Empty);
            }

            return new MixerReading(
                resolved: true,
                path: channel.Path,
                guid: "00000000-0000-0000-0000-000000000000",
                volume: GetVolume(channel),
                finalVolume: GetFinalVolume(channel),
                muted: IsMuted(channel),
                paused: IsPaused(channel),
                channelGroupLive: channel.IsBus && held.Contains(channel.Path),
                cpuExclusive: 0u,
                cpuInclusive: 0u,
                memoryExclusive: 0,
                memoryInclusive: 0,
                memorySampleData: 0,
                portIndex: 0ul,
                portIndexSupported: false);
        }

        public void HoldChannelGroup(MixerChannel channel, bool held)
        {
            if (!channel.IsValid || !channel.IsBus)
            {
                return;
            }

            if (held)
            {
                this.held.Add(channel.Path);
            }
            else
            {
                this.held.Remove(channel.Path);
            }
        }

        public void Invalidate()
        {
        }

        public void Clear()
        {
            volumes.Clear();
            muted.Clear();
            paused.Clear();
            held.Clear();
            StopAllEventsCalls = 0;
        }
    }
}
