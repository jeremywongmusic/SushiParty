using NUnit.Framework;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Minigames.PedalToThePaddle;

namespace SushiParty.Tests
{
    public sealed class PedalToThePaddleBrainTests
    {
        private static readonly PedalRiderState Sidelined = new PedalRiderState(0f, ready: false);

        private static PedalToThePaddleBrain BrainOn(PedalRiderState self)
        {
            return BrainOn(self, CpuSkill.Standard);
        }

        private static PedalToThePaddleBrain BrainOn(PedalRiderState self, CpuSkill skill)
        {
            PedalToThePaddleSnapshot world = new PedalToThePaddleSnapshot(self, Sidelined);
            return new PedalToThePaddleBrain(() => world, ParticipantSlot.One, skill);
        }

        [Test]
        public void A_partner_carried_past_the_sweet_spot_walks_back_against_the_wheel()
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(55f, ready: true));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.EqualTo(-0.6f).Within(1e-4f),
                "the wheel is carrying it forward, so holding station means walking back");
        }

        [Test]
        public void A_partner_already_in_the_sweet_spot_stands_still_and_keeps_stomping()
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(39f, ready: true));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.EqualTo(0f).Within(1e-4f), "no need to shuffle");
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True,
                "standing on the forward face is exactly where a stomp pays");
        }

        [Test]
        public void A_partner_about_to_slide_off_backs_away_instead_of_stomping()
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(70f, ready: true));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.EqualTo(-1f).Within(1e-4f), "full retreat, not a stroll");
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }

        [Test]
        public void A_partner_still_behind_the_top_of_the_wheel_walks_up_before_it_stomps()
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(8f, ready: true));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.GreaterThan(0f), "walk forward into the driving band");
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False, "no wasted stomp");
        }

        [Test]
        public void A_partner_who_is_off_the_wheel_lets_go_of_the_stick()
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(70f, ready: false));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(brain.Move.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }

        [Test]
        public void The_first_stomp_is_immediate_and_the_next_waits_out_the_interval()
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(39f, ready: true));

            brain.Tick(0.1f);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "opens with a stomp");
            Assert.That(brain.IsHeld(MinigameAction.Primary), Is.True, "and the button stays down");

            for (int i = 0; i < 5; i++)
            {
                brain.Tick(0.1f);
                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False, $"too soon at frame {i}");
            }

            brain.Tick(0.1f);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "and back on the beat");
        }

        [Test]
        public void A_sharp_partner_stomps_more_often_than_a_relaxed_one()
        {
            int sharp = StompsIn(CpuSkill.Sharp);
            int relaxed = StompsIn(CpuSkill.Relaxed);

            Assert.That(sharp, Is.GreaterThan(relaxed),
                "difficulty is the cadence of the help, not how fast the seat walks");
        }

        [Test]
        public void A_brain_reads_its_own_seat_and_not_its_teammates()
        {
            PedalToThePaddleSnapshot world = new PedalToThePaddleSnapshot(
                new PedalRiderState(70f, ready: true),
                new PedalRiderState(20f, ready: true));

            PedalToThePaddleBrain brain =
                new PedalToThePaddleBrain(() => world, ParticipantSlot.Two, CpuSkill.Standard);

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.EqualTo(0.6f).Within(1e-4f), "climb toward the sweet spot");
        }

        private static int StompsIn(CpuSkill skill)
        {
            PedalToThePaddleBrain brain = BrainOn(new PedalRiderState(40f, ready: true), skill);
            int stomps = 0;

            for (int i = 0; i < 60; i++)
            {
                brain.Tick(0.05f);
                if (brain.WasPressed(MinigameAction.Primary))
                {
                    stomps++;
                }
            }

            return stomps;
        }
    }
}
