using NUnit.Framework;
using SushiParty.Board;

namespace SushiParty.Tests
{
    public sealed class ContinueGateTests
    {
        private const float MinimumHold = 1f;
        private const float UnattendedHold = 3f;
        private const float Frame = 1f / 60f;

        [Test]
        public void A_gate_that_has_not_been_opened_holds_nothing()
        {
            ContinueGate gate = new ContinueGate(MinimumHold, UnattendedHold);

            Assert.That(gate.AwaitingPress, Is.False, "no card is up");
            Assert.That(gate.Held, Is.EqualTo(0f));
            Assert.That(
                gate.Tick(10f, true, true),
                Is.False,
                "a press with nothing on screen moves nothing on");
        }

        [Test]
        public void A_press_inside_the_minimum_hold_is_not_the_press_that_counts()
        {
            ContinueGate gate = Opened();

            for (int frame = 0; frame < 30; frame++)
            {
                Assert.That(
                    gate.Tick(Frame, true, true),
                    Is.False,
                    $"the call-out went after {(frame + 1) * Frame:0.00}s");
            }

            Assert.That(gate.Held, Is.EqualTo(0.5f).Within(0.02f), "and it is still up");
        }

        [Test]
        public void A_press_after_the_minimum_hold_moves_the_beat_on()
        {
            ContinueGate gate = Opened();

            Assert.That(gate.Tick(MinimumHold, false, true), Is.False, "nothing presses it yet");
            Assert.That(gate.AwaitingPress, Is.True, "but it is asking now");

            Assert.That(gate.Tick(Frame, true, true), Is.True);
            Assert.That(gate.AwaitingPress, Is.False, "the card has gone");
        }

        [Test]
        public void The_hint_stays_down_until_pressing_would_actually_do_something()
        {
            ContinueGate gate = new ContinueGate(MinimumHold, UnattendedHold);

            Assert.That(gate.AwaitingPress, Is.False, "before the beat opens");

            gate.Open();
            Assert.That(gate.AwaitingPress, Is.False, "on the frame it opens");

            gate.Tick(MinimumHold * 0.5f, false, true);
            Assert.That(gate.AwaitingPress, Is.False, "half way through the hold");

            gate.Tick(MinimumHold * 0.5f, false, true);
            Assert.That(gate.AwaitingPress, Is.True, "and the moment the hold is served");
        }

        [Test]
        public void A_beat_somebody_is_sitting_at_waits_as_long_as_it_takes()
        {
            ContinueGate gate = Opened();

            for (int frame = 0; frame < 3600; frame++)
            {
                Assert.That(
                    gate.Tick(Frame, false, true),
                    Is.False,
                    $"it let itself out on frame {frame}");
            }

            Assert.That(gate.AwaitingPress, Is.True, "still asking");
            Assert.That(gate.Held, Is.EqualTo(60f).Within(0.1f));

            Assert.That(gate.Tick(Frame, true, true), Is.True, "and it still answers a press");
        }

        [Test]
        public void A_board_with_nobody_in_a_seat_lets_itself_out()
        {
            ContinueGate gate = Opened();

            float held = 0f;
            bool released = false;

            for (int frame = 0; frame < 600 && !released; frame++)
            {
                held += Frame;
                released = gate.Tick(Frame, false, false);
            }

            Assert.That(released, Is.True, "nobody is coming to press it, so it cannot need a press");
            Assert.That(
                held,
                Is.EqualTo(UnattendedHold).Within(0.05f),
                "and it holds its full unattended beat first, rather than flashing past");
        }

        [Test]
        public void An_unattended_beat_serves_its_minimum_hold_first()
        {
            ContinueGate gate = Opened(minimumHold: 1.5f, unattendedHold: 0.5f);

            Assert.That(gate.Tick(1.4f, false, false), Is.False, "the minimum wins");
            Assert.That(gate.AwaitingPress, Is.False);

            Assert.That(gate.Tick(0.1f, false, false), Is.True);
        }

        [Test]
        public void A_press_from_the_room_still_beats_the_unattended_hold()
        {
            ContinueGate gate = Opened();

            Assert.That(gate.Tick(MinimumHold, false, false), Is.False);

            Assert.That(gate.Tick(Frame, true, false), Is.True);
        }

        [Test]
        public void A_beat_is_only_finished_with_once()
        {
            ContinueGate gate = Opened();
            gate.Tick(MinimumHold, false, true);

            Assert.That(gate.Tick(Frame, true, true), Is.True);

            for (int frame = 0; frame < 600; frame++)
            {
                Assert.That(
                    gate.Tick(Frame, true, false),
                    Is.False,
                    "a controller that keeps ticking must not be sent on twice");
            }

            Assert.That(gate.Held, Is.EqualTo(0f), "nothing is being held");
        }

        [Test]
        public void The_next_beat_starts_its_hold_from_scratch()
        {
            ContinueGate gate = Opened();
            gate.Tick(MinimumHold, false, true);
            gate.Tick(Frame, true, true);

            gate.Open();

            Assert.That(gate.Held, Is.EqualTo(0f));
            Assert.That(gate.AwaitingPress, Is.False, "the last card's hold does not pay for this one");
            Assert.That(gate.Tick(Frame, true, true), Is.False, "so this press is too early all over again");
        }

        private static ContinueGate Opened(
            float minimumHold = MinimumHold,
            float unattendedHold = UnattendedHold)
        {
            ContinueGate gate = new ContinueGate(minimumHold, unattendedHold);
            gate.Open();
            return gate;
        }
    }
}
