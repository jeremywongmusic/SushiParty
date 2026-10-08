using NUnit.Framework;
using System.Collections.Generic;
using SushiParty.Audio;

namespace SushiParty.Tests
{
    public sealed class PauseControllerTests
    {
        private SilentAudioMixer mixer;
        private RecordedGlobals published;

        [SetUp]
        public void SetUp()
        {
            mixer = new SilentAudioMixer();
            published = new RecordedGlobals();
        }

        private PauseController Controller()
        {
            return new PauseController(mixer, Mixer.PausedByThePauseMenu, published);
        }

        [Test]
        public void It_starts_unpaused()
        {
            Assert.That(Controller().IsPaused, Is.False);
        }

        [Test]
        public void Pausing_holds_the_music_and_game_buses()
        {
            PauseController controller = Controller();

            Assert.That(controller.Pause(), Is.True);

            Assert.That(controller.IsPaused, Is.True);
            Assert.That(mixer.IsPaused(Mixer.Music), Is.True);
            Assert.That(mixer.IsPaused(Mixer.Game), Is.True);
        }

        [Test]
        public void Pausing_leaves_the_interface_bus_running()
        {
            PauseController controller = Controller();

            controller.Pause();

            Assert.That(mixer.IsPaused(Mixer.Interface), Is.False,
                "the pause menu still has to be able to click and confirm");
        }

        [Test]
        public void Pausing_leaves_the_master_bus_running()
        {
            PauseController controller = Controller();

            controller.Pause();

            Assert.That(mixer.IsPaused(Mixer.Master), Is.False,
                "pausing the master would take everything under it, interface included");
        }

        [Test]
        public void Resuming_lets_every_held_bus_go()
        {
            PauseController controller = Controller();
            controller.Pause();

            Assert.That(controller.Resume(), Is.True);

            Assert.That(controller.IsPaused, Is.False);
            Assert.That(mixer.IsPaused(Mixer.Music), Is.False);
            Assert.That(mixer.IsPaused(Mixer.Game), Is.False);
        }

        [Test]
        public void Pausing_twice_is_not_a_second_pause()
        {
            PauseController controller = Controller();
            controller.Pause();

            Assert.That(controller.Pause(), Is.False, "nothing changed, so nothing to announce");
            Assert.That(controller.IsPaused, Is.True);
        }

        [Test]
        public void Resuming_when_nothing_is_held_reports_no_change()
        {
            Assert.That(Controller().Resume(), Is.False);
        }

        [Test]
        public void Toggling_walks_in_and_back_out()
        {
            PauseController controller = Controller();

            Assert.That(controller.Toggle(), Is.True, "toggling reports where it landed");
            Assert.That(controller.IsPaused, Is.True);

            Assert.That(controller.Toggle(), Is.False);
            Assert.That(controller.IsPaused, Is.False);
        }

        [Test]
        public void Suspending_lets_the_buses_go_without_ending_the_pause()
        {
            PauseController controller = Controller();
            controller.Pause();

            Assert.That(controller.Suspend(), Is.True);

            Assert.That(mixer.IsPaused(Mixer.Music), Is.False, "a slider you cannot hear is not a volume control");
            Assert.That(controller.IsPaused, Is.True, "but the game is still paused");
            Assert.That(controller.IsHolding, Is.False);
        }

        [Test]
        public void Restoring_puts_the_hold_back()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Suspend();

            Assert.That(controller.Restore(), Is.True);

            Assert.That(mixer.IsPaused(Mixer.Music), Is.True);
            Assert.That(mixer.IsPaused(Mixer.Game), Is.True);
            Assert.That(controller.IsHolding, Is.True);
        }

        [Test]
        public void Suspending_an_unpaused_mix_does_nothing()
        {
            PauseController controller = Controller();

            Assert.That(controller.Suspend(), Is.False, "there is no hold to let go of");
            Assert.That(controller.IsPaused, Is.False);
        }

        [Test]
        public void Restoring_after_the_pause_has_ended_underneath_does_not_re_hold()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Suspend();

            controller.Resume();

            Assert.That(controller.Restore(), Is.False);
            Assert.That(mixer.IsPaused(Mixer.Music), Is.False,
                "restoring a pause nobody is in would silence a running game");
        }

        [Test]
        public void Resuming_from_suspended_leaves_the_buses_running()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Suspend();

            Assert.That(controller.Resume(), Is.True);

            Assert.That(controller.IsPaused, Is.False);
            Assert.That(controller.IsHolding, Is.False);
            Assert.That(mixer.IsPaused(Mixer.Game), Is.False);
        }

        [Test]
        public void Pausing_again_after_a_suspend_holds_properly()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Suspend();
            controller.Resume();

            Assert.That(controller.Pause(), Is.True);

            Assert.That(controller.IsHolding, Is.True, "a stale suspend must not swallow the next pause");
            Assert.That(mixer.IsPaused(Mixer.Music), Is.True);
        }

        [Test]
        public void Suspending_twice_is_not_a_second_suspend()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Suspend();

            Assert.That(controller.Suspend(), Is.False);
        }

        [Test]
        public void Leaving_a_round_stops_every_bus_rather_than_holding_it()
        {
            PauseController controller = Controller();
            controller.Pause();

            controller.StopEverything();

            Assert.That(mixer.StopAllEventsCalls, Is.EqualTo(Mixer.Buses.Count),
                "one stopAllEvents per bus, so nothing survives the scene change");
            Assert.That(controller.IsPaused, Is.False,
                "a stopped bus that is still paused would swallow the next scene");
        }

        [Test]
        public void A_paused_bus_is_released_before_it_is_stopped()
        {
            PauseController controller = Controller();
            controller.Pause();

            controller.StopEverything();

            Assert.That(mixer.IsPaused(Mixer.Music), Is.False);
            Assert.That(mixer.IsPaused(Mixer.Game), Is.False);
        }

        [Test]
        public void The_held_set_is_whatever_it_was_handed_rather_than_every_bus()
        {
            PauseController controller = new PauseController(
                mixer,
                new List<MixerChannel> { Mixer.Music });

            controller.Pause();

            Assert.That(mixer.IsPaused(Mixer.Music), Is.True);
            Assert.That(mixer.IsPaused(Mixer.Game), Is.False, "the caller decides what a pause means");
        }

        [Test]
        public void A_VCA_in_the_held_set_is_quietly_ignored()
        {
            PauseController controller = new PauseController(
                mixer,
                new List<MixerChannel> { Mixer.Music, Mixer.MusicVca });

            Assert.That(controller.Pause(), Is.True);

            Assert.That(mixer.IsPaused(Mixer.MusicVca), Is.False, "there is no setPaused on a VCA to call");
            Assert.That(mixer.IsPaused(Mixer.Music), Is.True, "and the bus beside it still pauses");
        }

        [Test]
        public void Pausing_tells_the_mix_it_is_paused()
        {
            Controller().Pause();

            Assert.That(published.Number(Global.Paused), Is.EqualTo(1f));
        }

        [Test]
        public void Resuming_tells_the_mix_it_is_not()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Resume();

            Assert.That(published.Number(Global.Paused), Is.EqualTo(0f));
        }

        [Test]
        public void The_global_follows_the_held_state_rather_than_the_menu_being_up()
        {
            PauseController controller = Controller();
            controller.Pause();
            controller.Suspend();

            Assert.That(published.Number(Global.Paused), Is.EqualTo(0f),
                "the settings screen is auditioning the mix over a pause, and a snapshot " +
                "ducking it now would fight the thing the player is trying to listen to");
            Assert.That(controller.IsPaused, Is.True, "while the game itself is still paused");

            controller.Restore();
            Assert.That(published.Number(Global.Paused), Is.EqualTo(1f));
        }

        [Test]
        public void A_controller_with_nowhere_to_publish_still_works()
        {
            PauseController controller = new PauseController(mixer, Mixer.PausedByThePauseMenu);

            Assert.DoesNotThrow(() => controller.Pause());
            Assert.That(mixer.IsPaused(Mixer.Music), Is.True);
        }

        [Test]
        public void The_catalogue_only_nominates_buses_for_pausing()
        {
            for (int i = 0; i < Mixer.PausedByThePauseMenu.Count; i++)
            {
                Assert.That(Mixer.PausedByThePauseMenu[i].IsBus, Is.True,
                    Mixer.PausedByThePauseMenu[i].Path);
            }
        }
    }
}
