using NUnit.Framework;
using SushiParty.Audio;

namespace SushiParty.Tests
{
    public sealed class MixerSettingsTests
    {
        private SilentAudioMixer mixer;
        private MemoryPreferenceStore store;

        [SetUp]
        public void SetUp()
        {
            mixer = new SilentAudioMixer();
            store = new MemoryPreferenceStore();
        }

        private MixerSettings Settings()
        {
            return new MixerSettings(Mixer.All, mixer, store);
        }

        [Test]
        public void The_screen_lists_every_bus_first_and_then_every_VCA()
        {
            MixerSettings settings = Settings();

            Assert.That(settings.Channels.Count, Is.EqualTo(Mixer.Buses.Count + Mixer.Vcas.Count));
            Assert.That(settings.Channels[0].Path, Is.EqualTo(Mixer.Master.Path), "master leads");
            Assert.That(settings.Channels[Mixer.Buses.Count].Kind, Is.EqualTo(MixerChannelKind.Vca),
                "the VCAs come after the last bus");
        }

        [Test]
        public void It_opens_on_the_first_channel()
        {
            Assert.That(Settings().SelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void The_cursor_wraps_off_both_ends_of_the_list()
        {
            MixerSettings settings = Settings();

            Assert.That(settings.Navigate(-1), Is.True);
            Assert.That(settings.SelectedIndex, Is.EqualTo(settings.Channels.Count - 1),
                "up off the top comes back at the bottom");

            settings.Navigate(1);
            Assert.That(settings.SelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void Pointing_at_nothing_leaves_the_cursor_alone()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(2);

            Assert.That(settings.SelectIndex(-1), Is.False);
            Assert.That(settings.SelectIndex(99), Is.False);
            Assert.That(settings.SelectIndex(2), Is.False, "already resting on it");
            Assert.That(settings.SelectedIndex, Is.EqualTo(2));
        }

        [Test]
        public void A_nudge_moves_the_selected_channel_by_one_step()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(1);
            float before = settings.VolumeOf(settings.Selected);

            Assert.That(settings.Nudge(1), Is.True);

            Assert.That(settings.VolumeOf(settings.Selected), Is.EqualTo(before + MixerSettings.Step).Within(0.0001f));
        }

        [Test]
        public void A_nudge_writes_straight_through_to_the_mixer()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(1);

            settings.Nudge(-1);

            Assert.That(mixer.GetVolume(settings.Selected),
                Is.EqualTo(settings.VolumeOf(settings.Selected)).Within(0.0001f),
                "the slider and the bus must never disagree");
        }

        [Test]
        public void Volume_stops_at_unity_and_at_silence()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(0);

            for (int i = 0; i < 100; i++)
            {
                settings.Nudge(1);
            }

            Assert.That(settings.VolumeOf(settings.Selected), Is.EqualTo(1f).Within(0.0001f));

            for (int i = 0; i < 100; i++)
            {
                settings.Nudge(-1);
            }

            Assert.That(settings.VolumeOf(settings.Selected), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void A_nudge_that_cannot_move_reports_no_change()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(0);

            for (int i = 0; i < 100; i++)
            {
                settings.Nudge(1);
            }

            Assert.That(settings.Nudge(1), Is.False, "there is no click of sound for a slider already at the end");
        }

        [Test]
        public void Every_channel_turns_independently()
        {
            MixerSettings settings = Settings();

            settings.SelectIndex(1);
            settings.Nudge(-1);
            float music = settings.VolumeOf(settings.Selected);

            settings.SelectIndex(2);

            Assert.That(settings.VolumeOf(settings.Channels[1]), Is.EqualTo(music).Within(0.0001f));
            Assert.That(settings.VolumeOf(settings.Selected), Is.EqualTo(settings.Selected.DefaultVolume).Within(0.0001f),
                "moving one slider must not drag the next one with it");
        }

        [Test]
        public void A_VCA_pulls_a_bus_final_volume_down_without_moving_its_volume()
        {
            MixerSettings settings = Settings();
            float busVolumeBefore = settings.VolumeOf(Mixer.Music);
            float finalBefore = settings.FinalVolumeOf(Mixer.Music);

            settings.SelectIndex(settings.IndexOf(Mixer.MusicVca));
            settings.Nudge(-1);

            Assert.That(settings.VolumeOf(Mixer.Music), Is.EqualTo(busVolumeBefore).Within(0.0001f),
                "the bus was never told to change");
            Assert.That(settings.FinalVolumeOf(Mixer.Music), Is.LessThan(finalBefore),
                "but what it finally plays at did — which is the whole of what a VCA is");
        }

        [Test]
        public void One_VCA_trims_two_buses_at_once()
        {
            MixerSettings settings = Settings();
            float gameBefore = settings.FinalVolumeOf(Mixer.Game);
            float interfaceBefore = settings.FinalVolumeOf(Mixer.Interface);

            settings.SelectIndex(settings.IndexOf(Mixer.EffectsVca));
            settings.Nudge(-1);

            Assert.That(settings.FinalVolumeOf(Mixer.Game), Is.LessThan(gameBefore));
            Assert.That(settings.FinalVolumeOf(Mixer.Interface), Is.LessThan(interfaceBefore),
                "cutting across the routing tree is the thing a bus cannot do");
        }

        [Test]
        public void The_master_bus_carries_every_other_bus_with_it()
        {
            MixerSettings settings = Settings();
            float musicBefore = settings.FinalVolumeOf(Mixer.Music);

            settings.SelectIndex(settings.IndexOf(Mixer.Master));
            settings.Nudge(-1);

            Assert.That(settings.FinalVolumeOf(Mixer.Music), Is.LessThan(musicBefore));
        }

        [Test]
        public void A_bus_can_be_muted_and_unmuted()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(settings.IndexOf(Mixer.Music));

            Assert.That(settings.ToggleMute(), Is.True);
            Assert.That(settings.IsMuted(Mixer.Music), Is.True);
            Assert.That(mixer.IsMuted(Mixer.Music), Is.True, "written through to the bus");

            settings.ToggleMute();
            Assert.That(settings.IsMuted(Mixer.Music), Is.False);
        }

        [Test]
        public void Muting_a_bus_leaves_its_volume_where_it_was()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(settings.IndexOf(Mixer.Music));
            settings.Nudge(-1);
            float volume = settings.VolumeOf(Mixer.Music);

            settings.ToggleMute();
            settings.ToggleMute();

            Assert.That(settings.VolumeOf(Mixer.Music), Is.EqualTo(volume).Within(0.0001f),
                "unmuting must give back the volume the slider was on, not unity");
        }

        [Test]
        public void A_VCA_refuses_to_be_muted_because_the_API_has_no_such_call()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(settings.IndexOf(Mixer.MusicVca));

            Assert.That(settings.ToggleMute(), Is.False, "setMute does not exist on a VCA");
            Assert.That(settings.IsMuted(Mixer.MusicVca), Is.False);
        }

        [Test]
        public void Reset_puts_every_slider_back_where_it_started()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(settings.IndexOf(Mixer.Music));
            settings.Nudge(-1);
            settings.ToggleMute();

            Assert.That(settings.Reset(), Is.True);

            Assert.That(settings.VolumeOf(Mixer.Music), Is.EqualTo(Mixer.Music.DefaultVolume).Within(0.0001f));
            Assert.That(settings.IsMuted(Mixer.Music), Is.False, "a reset clears the mutes too");
        }

        [Test]
        public void Reset_on_untouched_settings_reports_no_change()
        {
            Assert.That(Settings().Reset(), Is.False, "nothing moved, so nothing to announce");
        }

        [Test]
        public void Slider_positions_and_mutes_survive_a_restart()
        {
            MixerSettings first = Settings();
            first.SelectIndex(first.IndexOf(Mixer.Game));
            first.Nudge(-1);
            first.Nudge(-1);
            first.ToggleMute();
            first.Save();

            float expected = first.VolumeOf(Mixer.Game);

            mixer = new SilentAudioMixer();
            MixerSettings reopened = Settings();

            Assert.That(reopened.VolumeOf(Mixer.Game), Is.EqualTo(expected).Within(0.0001f));
            Assert.That(reopened.IsMuted(Mixer.Game), Is.True);
        }

        [Test]
        public void Loading_pushes_everything_it_read_into_the_mixer()
        {
            MixerSettings first = Settings();
            first.SelectIndex(first.IndexOf(Mixer.Game));
            first.Nudge(-1);
            first.Save();

            mixer = new SilentAudioMixer();
            MixerSettings reopened = Settings();

            Assert.That(mixer.GetVolume(Mixer.Game), Is.EqualTo(reopened.VolumeOf(Mixer.Game)).Within(0.0001f),
                "a bus that was never told its saved volume would play at unity");
        }

        [Test]
        public void Saving_flushes_the_store_once_rather_than_per_channel()
        {
            MixerSettings settings = Settings();

            settings.Save();

            Assert.That(store.Saves, Is.EqualTo(1));
        }

        [Test]
        public void Apply_pushes_the_whole_state_at_a_mixer_that_has_lost_it()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(settings.IndexOf(Mixer.Music));
            settings.Nudge(-1);
            settings.ToggleMute();

            mixer.Clear();
            settings.Apply();

            Assert.That(mixer.GetVolume(Mixer.Music), Is.EqualTo(settings.VolumeOf(Mixer.Music)).Within(0.0001f));
            Assert.That(mixer.IsMuted(Mixer.Music), Is.True);
        }

        [Test]
        public void A_mixer_that_has_forgotten_which_channels_exist_still_takes_the_state_back()
        {
            MixerSettings settings = Settings();
            settings.SelectIndex(settings.IndexOf(Mixer.Music));
            settings.Nudge(-1);

            mixer.Clear();
            mixer.Invalidate();
            settings.Apply();

            Assert.That(mixer.GetVolume(Mixer.Music), Is.EqualTo(settings.VolumeOf(Mixer.Music)).Within(0.0001f),
                "a mixer that gave up on a bus before its bank arrived must not stay given up on it");
        }

        [Test]
        public void Unity_gain_prints_as_zero_decibels()
        {
            Assert.That(Mixer.Decibels(1f), Is.EqualTo("0.0 dB"));
        }

        [Test]
        public void Silence_prints_as_minus_infinity_rather_than_a_number()
        {
            Assert.That(Mixer.Decibels(0f), Is.EqualTo("-∞ dB"));
        }

        [Test]
        public void Half_gain_prints_as_about_minus_six_decibels()
        {
            Assert.That(Mixer.Decibels(0.5f), Is.EqualTo("-6.0 dB"));
        }

        [Test]
        public void A_channel_is_found_by_the_path_a_saved_preference_names()
        {
            Assert.That(Mixer.TryFind("bus:/Music", out MixerChannel channel), Is.True);
            Assert.That(channel.DisplayName, Is.EqualTo(Mixer.Music.DisplayName));
            Assert.That(Mixer.TryFind("bus:/Nonsense", out MixerChannel _), Is.False);
        }

        [Test]
        public void Only_the_buses_offer_the_bus_only_operations()
        {
            for (int i = 0; i < Mixer.Buses.Count; i++)
            {
                Assert.That(Mixer.Buses[i].IsBus, Is.True, Mixer.Buses[i].Path);
            }

            for (int i = 0; i < Mixer.Vcas.Count; i++)
            {
                Assert.That(Mixer.Vcas[i].IsBus, Is.False, Mixer.Vcas[i].Path);
                Assert.That(Mixer.Vcas[i].PausesWithTheGame, Is.False, "a VCA has no setPaused to call");
            }
        }
    }
}
