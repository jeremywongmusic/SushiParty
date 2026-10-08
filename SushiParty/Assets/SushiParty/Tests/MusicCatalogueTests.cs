using NUnit.Framework;
using SushiParty.Audio;
using SushiParty.Core;
using System.Collections.Generic;
using System.Reflection;

namespace SushiParty.Tests
{
    public sealed class MusicCatalogueTests
    {
        private const string MusicRoot = "event:/Music/";
        private const string SfxRoot = "event:/SFX/";
        private const string MusicPrefix = "Music";
        private const int LeastMusicEntries = 8;

        [Test]
        public void Every_minigame_has_a_track()
        {
            foreach (MinigameId id in System.Enum.GetValues(typeof(MinigameId)))
            {
                AudioEvent track = Sfx.MusicFor(id);

                Assert.That(
                    track.IsValid,
                    Is.True,
                    $"{id} has no music. Add the event and a case for it to Sfx.MusicFor.");
            }
        }

        [Test]
        public void No_two_minigames_share_a_track()
        {
            Dictionary<string, MinigameId> owners = new Dictionary<string, MinigameId>();

            foreach (MinigameId id in System.Enum.GetValues(typeof(MinigameId)))
            {
                string path = Sfx.MusicFor(id).Path;

                if (owners.TryGetValue(path, out MinigameId first))
                {
                    Assert.Fail($"{id} and {first} both play '{path}'");
                }

                owners.Add(path, id);
            }
        }

        [Test]
        public void Every_minigame_track_is_music()
        {
            foreach (MinigameId id in System.Enum.GetValues(typeof(MinigameId)))
            {
                Assert.That(
                    Sfx.MusicFor(id).Path,
                    Does.StartWith(MusicRoot),
                    $"{id}'s track is not under the music root");
            }
        }

        [Test]
        public void An_id_with_no_track_is_silence_rather_than_an_exception()
        {
            AudioEvent missing = default(AudioEvent);

            Assert.DoesNotThrow(() => missing = Sfx.MusicFor((MinigameId)(-1)));
            Assert.That(missing.IsValid, Is.False, "an unmapped id must not hand back something playable");
        }

        [Test]
        public void The_round_is_bracketed_by_two_stingers()
        {
            Assert.That(Sfx.MusicRoundStart.Path, Is.EqualTo(MusicRoot + "Round/Start"));
            Assert.That(Sfx.MusicRoundStop.Path, Is.EqualTo(MusicRoot + "Round/Stop"));
            Assert.That(
                Sfx.MusicRoundStart.Path,
                Is.Not.EqualTo(Sfx.MusicRoundStop.Path),
                "opening and closing a round are not the same sound");
        }

        [Test]
        public void Music_and_sound_effects_live_under_their_own_roots()
        {
            foreach (FieldInfo field in CatalogueFields())
            {
                AudioEvent entry = (AudioEvent)field.GetValue(null);
                bool isMusic = field.Name.StartsWith(MusicPrefix);

                Assert.That(
                    entry.Path,
                    Does.StartWith(isMusic ? MusicRoot : SfxRoot),
                    isMusic
                        ? $"{field.Name} is music but is not under {MusicRoot}"
                        : $"{field.Name} is a sound effect but is not under {SfxRoot}");
            }
        }

        [Test]
        public void No_sound_effect_has_wandered_into_the_music_tree()
        {
            foreach (FieldInfo field in CatalogueFields())
            {
                if (field.Name.StartsWith(MusicPrefix))
                {
                    continue;
                }

                AudioEvent entry = (AudioEvent)field.GetValue(null);

                Assert.That(
                    entry.Path,
                    Does.Not.StartWith(MusicRoot),
                    $"{field.Name} is filed as a sound effect but sits in the music tree");
            }
        }

        [Test]
        public void Nothing_in_the_catalogue_shares_a_path_with_anything_else()
        {
            Dictionary<string, string> owners = new Dictionary<string, string>();

            foreach (FieldInfo field in CatalogueFields())
            {
                AudioEvent entry = (AudioEvent)field.GetValue(null);

                if (owners.TryGetValue(entry.Path, out string first))
                {
                    Assert.Fail($"{field.Name} and {first} are both '{entry.Path}'");
                }

                owners.Add(entry.Path, field.Name);
            }
        }

        [Test]
        public void The_music_section_is_worth_reflecting_over()
        {
            int found = 0;

            foreach (FieldInfo field in CatalogueFields())
            {
                if (field.Name.StartsWith(MusicPrefix))
                {
                    found++;
                }
            }

            Assert.That(
                found,
                Is.GreaterThanOrEqualTo(LeastMusicEntries),
                "expected at least two round stingers and six minigame tracks — has the section changed?");
        }

        private static IReadOnlyList<FieldInfo> CatalogueFields()
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            foreach (FieldInfo field in typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(AudioEvent))
                {
                    fields.Add(field);
                }
            }

            return fields;
        }
    }
}
