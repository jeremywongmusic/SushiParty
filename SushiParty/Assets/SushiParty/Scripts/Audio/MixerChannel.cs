using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace SushiParty.Audio
{
    public enum MixerChannelKind
    {
        Bus = 0,

        Vca = 1,
    }

    public readonly struct MixerChannel
    {
        public readonly string Path;
        public readonly string DisplayName;
        public readonly string Summary;
        public readonly MixerChannelKind Kind;
        public readonly bool PausesWithTheGame;
        public readonly float DefaultVolume;

        public MixerChannel(
            string path,
            string displayName,
            string summary,
            MixerChannelKind kind,
            bool pausesWithTheGame,
            float defaultVolume)
        {
            Path = path;
            DisplayName = displayName;
            Summary = summary;
            Kind = kind;
            PausesWithTheGame = pausesWithTheGame;
            DefaultVolume = defaultVolume;
        }

        public bool IsValid => !string.IsNullOrEmpty(Path);
        public bool IsBus => Kind == MixerChannelKind.Bus;
        public string PreferenceKey => "SushiParty.Mixer." + Path;
        public override string ToString() => Path;
    }

    public static class Mixer
    {
        private static MixerChannel Bus(
            string path,
            string name,
            string summary,
            bool pauses,
            float defaultVolume = 1f)
        {
            return new MixerChannel(path, name, summary, MixerChannelKind.Bus, pauses, defaultVolume);
        }

        private static MixerChannel Vca(string path, string name, string summary, float defaultVolume = 1f)
        {
            return new MixerChannel(path, name, summary, MixerChannelKind.Vca, false, defaultVolume);
        }

        public static readonly MixerChannel Master =
            Bus("bus:/", "Master", "Everything, all at once", pauses: false);

        public static readonly MixerChannel Music =
            Bus("bus:/Music", "Music", "Round tracks and stingers", pauses: true, defaultVolume: 0.8f);

        public static readonly MixerChannel Game =
            Bus("bus:/Game", "Game", "Impacts, footsteps, the clock", pauses: true);

        public static readonly MixerChannel Interface =
            Bus("bus:/UI", "Interface", "Menus and HUD — never paused", pauses: false);

        public static readonly MixerChannel MusicVca =
            Vca("vca:/Music", "Music VCA", "Trims music without moving its bus");

        public static readonly MixerChannel EffectsVca =
            Vca("vca:/Effects", "Effects VCA", "Trims game and interface together");

        public static readonly IReadOnlyList<MixerChannel> Buses = new ReadOnlyCollection<MixerChannel>(
            new List<MixerChannel> { Master, Music, Game, Interface });

        public static readonly IReadOnlyList<MixerChannel> Vcas = new ReadOnlyCollection<MixerChannel>(
            new List<MixerChannel> { MusicVca, EffectsVca });

        public static readonly IReadOnlyList<MixerChannel> All = new ReadOnlyCollection<MixerChannel>(
            new List<MixerChannel> { Master, Music, Game, Interface, MusicVca, EffectsVca });

        public static readonly IReadOnlyList<MixerChannel> PausedByThePauseMenu =
            new ReadOnlyCollection<MixerChannel>(new List<MixerChannel> { Music, Game });

        private static readonly IReadOnlyList<MixerChannel> NoVcas =
            new ReadOnlyCollection<MixerChannel>(new List<MixerChannel>());

        private static readonly IReadOnlyList<MixerChannel> MusicTrim =
            new ReadOnlyCollection<MixerChannel>(new List<MixerChannel> { MusicVca });

        private static readonly IReadOnlyList<MixerChannel> EffectsTrim =
            new ReadOnlyCollection<MixerChannel>(new List<MixerChannel> { EffectsVca });

        public static IReadOnlyList<MixerChannel> VcasTrimming(MixerChannel bus)
        {
            if (bus.Path == Music.Path)
            {
                return MusicTrim;
            }

            if (bus.Path == Game.Path || bus.Path == Interface.Path)
            {
                return EffectsTrim;
            }

            return NoVcas;
        }

        public static AudioEvent AuditionFor(MixerChannel channel)
        {
            if (channel.Path == Music.Path || channel.Path == MusicVca.Path)
            {
                return Sfx.MusicRoundStart;
            }

            if (channel.Path == Game.Path || channel.Path == EffectsVca.Path)
            {
                return Sfx.BoardDieLand;
            }

            return Sfx.MenuConfirm;
        }

        public static bool TryFind(string path, out MixerChannel channel)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Path == path)
                {
                    channel = All[i];
                    return true;
                }
            }

            channel = default;
            return false;
        }

        public static string Decibels(float linear)
        {
            if (linear <= 0.0001f)
            {
                return "-∞ dB";
            }

            float db = 20f * Mathf.Log10(linear);
            return db > -0.05f && db < 0.05f ? "0.0 dB" : $"{db:0.0} dB";
        }
    }
}
