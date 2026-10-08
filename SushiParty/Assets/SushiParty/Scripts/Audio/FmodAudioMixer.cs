#if SUSHIPARTY_FMOD
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace SushiParty.Audio
{
    public sealed class FmodAudioMixer : IAudioMixer
    {
        private readonly Dictionary<string, Bus> buses = new Dictionary<string, Bus>();
        private readonly Dictionary<string, VCA> vcas = new Dictionary<string, VCA>();
        private readonly HashSet<string> missing = new HashSet<string>();
        private readonly HashSet<string> reported = new HashSet<string>();
        private readonly HashSet<string> portWritten = new HashSet<string>();
        public bool IsLive => true;

        public void SetVolume(MixerChannel channel, float volume)
        {
            if (channel.IsBus)
            {
                if (TryBus(channel, out Bus bus))
                {
                    Check(channel, "setVolume", bus.setVolume(volume));
                }

                return;
            }

            if (TryVca(channel, out VCA vca))
            {
                Check(channel, "setVolume", vca.setVolume(volume));
            }
        }

        public float GetVolume(MixerChannel channel)
        {
            if (channel.IsBus)
            {
                if (TryBus(channel, out Bus bus) && Check(channel, "getVolume", bus.getVolume(out float volume)))
                {
                    return volume;
                }

                return channel.DefaultVolume;
            }

            if (TryVca(channel, out VCA vca) && Check(channel, "getVolume", vca.getVolume(out float vcaVolume)))
            {
                return vcaVolume;
            }

            return channel.DefaultVolume;
        }

        public float GetFinalVolume(MixerChannel channel)
        {
            if (channel.IsBus)
            {
                if (TryBus(channel, out Bus bus)
                    && Check(channel, "getVolume(final)", bus.getVolume(out _, out float busFinal)))
                {
                    return busFinal;
                }

                return channel.DefaultVolume;
            }

            if (TryVca(channel, out VCA vca)
                && Check(channel, "getVolume(final)", vca.getVolume(out _, out float vcaFinal)))
            {
                return vcaFinal;
            }

            return channel.DefaultVolume;
        }

        public void SetMuted(MixerChannel channel, bool muted)
        {
            if (!channel.IsBus || !TryBus(channel, out Bus bus))
            {
                return;
            }

            Check(channel, "setMute", bus.setMute(muted));
        }

        public bool IsMuted(MixerChannel channel)
        {
            if (!channel.IsBus || !TryBus(channel, out Bus bus))
            {
                return false;
            }

            return Check(channel, "getMute", bus.getMute(out bool muted)) && muted;
        }

        public void SetPaused(MixerChannel channel, bool paused)
        {
            if (!channel.IsBus || !TryBus(channel, out Bus bus))
            {
                return;
            }

            Check(channel, "setPaused", bus.setPaused(paused));
        }

        public bool IsPaused(MixerChannel channel)
        {
            if (!channel.IsBus || !TryBus(channel, out Bus bus))
            {
                return false;
            }

            return Check(channel, "getPaused", bus.getPaused(out bool paused)) && paused;
        }

        public void StopAllEvents(MixerChannel channel, bool allowFadeout)
        {
            if (!channel.IsBus || !TryBus(channel, out Bus bus))
            {
                return;
            }

            // Fully qualified: FMODUnity declares its own STOP_MODE, so the bare name is ambiguous.
            FMOD.Studio.STOP_MODE mode = allowFadeout
                ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT
                : FMOD.Studio.STOP_MODE.IMMEDIATE;

            Check(channel, "stopAllEvents", bus.stopAllEvents(mode));
        }

        public void HoldChannelGroup(MixerChannel channel, bool held)
        {
            if (!channel.IsBus || !TryBus(channel, out Bus bus))
            {
                return;
            }

            if (held)
            {
                Check(channel, "lockChannelGroup", bus.lockChannelGroup());

                Check(channel, "flushCommands", RuntimeManager.StudioSystem.flushCommands());
                return;
            }

            Check(channel, "unlockChannelGroup", bus.unlockChannelGroup());
        }

        public MixerReading Read(MixerChannel channel)
        {
            if (channel.IsBus)
            {
                return ReadBus(channel);
            }

            return ReadVca(channel);
        }

        private MixerReading ReadBus(MixerChannel channel)
        {
            if (!TryBus(channel, out Bus bus))
            {
                return MixerReading.Unresolved(channel.Path);
            }

            string path = Check(channel, "getPath", bus.getPath(out string resolvedPath))
                ? resolvedPath
                : channel.Path;

            string guid = Check(channel, "getID", bus.getID(out FMOD.GUID id))
                ? id.ToString()
                : string.Empty;

            float volume = channel.DefaultVolume;
            float finalVolume = channel.DefaultVolume;
            if (Check(channel, "getVolume(final)", bus.getVolume(out float v, out float f)))
            {
                volume = v;
                finalVolume = f;
            }

            bool muted = Check(channel, "getMute", bus.getMute(out bool m)) && m;
            bool paused = Check(channel, "getPaused", bus.getPaused(out bool p)) && p;

            bool groupLive = Check(channel, "getChannelGroup", bus.getChannelGroup(out FMOD.ChannelGroup group))
                             && group.hasHandle();

            uint cpuExclusive = 0u;
            uint cpuInclusive = 0u;
            if (Check(channel, "getCPUUsage", bus.getCPUUsage(out uint exclusiveCpu, out uint inclusiveCpu)))
            {
                cpuExclusive = exclusiveCpu;
                cpuInclusive = inclusiveCpu;
            }

            int memoryExclusive = 0;
            int memoryInclusive = 0;
            int memorySampleData = 0;
            if (Check(channel, "getMemoryUsage", bus.getMemoryUsage(out MEMORY_USAGE memory)))
            {
                memoryExclusive = memory.exclusive;
                memoryInclusive = memory.inclusive;
                memorySampleData = memory.sampledata;
            }

            ReadPort(channel, bus, out ulong portIndex, out bool portSupported);

            return new MixerReading(
                resolved: true,
                path: path,
                guid: guid,
                volume: volume,
                finalVolume: finalVolume,
                muted: muted,
                paused: paused,
                channelGroupLive: groupLive,
                cpuExclusive: cpuExclusive,
                cpuInclusive: cpuInclusive,
                memoryExclusive: memoryExclusive,
                memoryInclusive: memoryInclusive,
                memorySampleData: memorySampleData,
                portIndex: portIndex,
                portIndexSupported: portSupported);
        }

        private void ReadPort(MixerChannel channel, Bus bus, out ulong portIndex, out bool supported)
        {
            portIndex = 0ul;
            supported = false;

            if (bus.getPortIndex(out ulong index) != FMOD.RESULT.OK)
            {
                return;
            }

            portIndex = index;
            supported = true;

            if (portWritten.Add(channel.Path))
            {
                Check(channel, "setPortIndex", bus.setPortIndex(index));
            }
        }

        private MixerReading ReadVca(MixerChannel channel)
        {
            if (!TryVca(channel, out VCA vca))
            {
                return MixerReading.Unresolved(channel.Path);
            }

            string path = Check(channel, "getPath", vca.getPath(out string resolvedPath))
                ? resolvedPath
                : channel.Path;

            string guid = Check(channel, "getID", vca.getID(out FMOD.GUID id))
                ? id.ToString()
                : string.Empty;

            float volume = channel.DefaultVolume;
            float finalVolume = channel.DefaultVolume;
            if (Check(channel, "getVolume(final)", vca.getVolume(out float v, out float f)))
            {
                volume = v;
                finalVolume = f;
            }

            return new MixerReading(
                resolved: true,
                path: path,
                guid: guid,
                volume: volume,
                finalVolume: finalVolume,
                muted: false,
                paused: false,
                channelGroupLive: false,
                cpuExclusive: 0u,
                cpuInclusive: 0u,
                memoryExclusive: 0,
                memoryInclusive: 0,
                memorySampleData: 0,
                portIndex: 0ul,
                portIndexSupported: false);
        }

        private bool TryBus(MixerChannel channel, out Bus bus)
        {
            bus = default;

            if (!channel.IsValid || missing.Contains(channel.Path))
            {
                return false;
            }

            if (buses.TryGetValue(channel.Path, out bus))
            {
                if (bus.isValid())
                {
                    return true;
                }

                buses.Remove(channel.Path);
            }

            try
            {
                bus = RuntimeManager.GetBus(channel.Path);
                if (bus.isValid())
                {
                    buses[channel.Path] = bus;
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SushiParty] FMOD could not resolve bus '{channel.Path}': {exception.Message}");
            }

            missing.Add(channel.Path);
            Debug.LogWarning($"[SushiParty] Bus '{channel.Path}' is not in any loaded bank. Its slider will do nothing.");
            return false;
        }

        private bool TryVca(MixerChannel channel, out VCA vca)
        {
            vca = default;

            if (!channel.IsValid || missing.Contains(channel.Path))
            {
                return false;
            }

            if (vcas.TryGetValue(channel.Path, out vca))
            {
                if (vca.isValid())
                {
                    return true;
                }

                vcas.Remove(channel.Path);
            }

            try
            {
                vca = RuntimeManager.GetVCA(channel.Path);
                if (vca.isValid())
                {
                    vcas[channel.Path] = vca;
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SushiParty] FMOD could not resolve VCA '{channel.Path}': {exception.Message}");
            }

            missing.Add(channel.Path);
            Debug.LogWarning($"[SushiParty] VCA '{channel.Path}' is not in any loaded bank. Its slider will do nothing.");
            return false;
        }

        public void Invalidate()
        {
            buses.Clear();
            vcas.Clear();
            missing.Clear();
            portWritten.Clear();

            reported.Clear();
        }

        private bool Check(MixerChannel channel, string call, FMOD.RESULT result)
        {
            if (result == FMOD.RESULT.OK)
            {
                return true;
            }

            string key = channel.Path + "#" + call;
            if (reported.Add(key))
            {
                Debug.LogWarning(
                    $"[SushiParty] FMOD {call} on '{channel.Path}' failed: {FMOD.Error.String(result)}");
            }

            return false;
        }
    }
}
#endif
