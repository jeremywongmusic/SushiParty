using NUnit.Framework;
using SushiParty.Core;
using SushiParty.Presentation;

namespace SushiParty.Tests
{
    public sealed class AllCpuAdvanceTests
    {
        [Test]
        public void A_round_can_be_dealt_two_computer_seats()
        {
            MinigameContext context = AllCpu();

            Assert.That(context.Participants.Length, Is.EqualTo(2));
            Assert.That(
                AnySeatIsAPerson(context),
                Is.False,
                "both seats on CPU is a setting the select screen offers, not a broken context");
        }

        [Test]
        public void The_results_card_never_moves_on_a_seat_scan_alone()
        {
            RoundClock clock = AtResults();

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Finished));

            for (int frame = 0; frame < 600; frame++)
            {
                Assert.That(
                    clock.Advance(1f / 60f, false).Has(RoundEvent.RetryRequested),
                    Is.False,
                    $"still stuck on frame {frame}");
            }

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Finished));
        }

        [Test]
        public void An_all_cpu_results_card_takes_its_press_from_the_room()
        {
            MinigameContext context = AllCpu();
            RoundClock clock = AtResults();

            Assert.That(
                MinigameController.SpectatorMayAdvance(clock.Phase, context),
                Is.True,
                "somebody is watching this card even though nobody owns a seat");

            RoundTick tick = clock.Advance(1f / 60f, true);

            Assert.That(tick.Has(RoundEvent.RetryRequested), Is.True);
            Assert.That(
                clock.Phase,
                Is.EqualTo(MinigamePhase.Finished),
                "asking is all the press does; leaving the scene is the controller's job");
        }

        [Test]
        public void A_card_with_a_person_in_a_seat_still_answers_only_to_that_seat()
        {
            Assert.That(
                MinigameController.SpectatorMayAdvance(
                    MinigamePhase.Finished,
                    Context(ControllerKind.Human, ControllerKind.Cpu)),
                Is.False,
                "player one is holding the button that matters");

            Assert.That(
                MinigameController.SpectatorMayAdvance(
                    MinigamePhase.Finished,
                    Context(ControllerKind.Cpu, ControllerKind.Human)),
                Is.False,
                "either seat being a person is enough");

            Assert.That(
                MinigameController.SpectatorMayAdvance(
                    MinigamePhase.Finished,
                    Context(ControllerKind.Human, ControllerKind.Human)),
                Is.False,
                "and a hot-seat pair certainly do not need a bystander");
        }

        [Test]
        public void No_other_phase_reads_a_button_no_seat_owns()
        {
            MinigameContext context = AllCpu();

            foreach (MinigamePhase phase in System.Enum.GetValues(typeof(MinigamePhase)))
            {
                if (phase == MinigamePhase.Finished)
                {
                    continue;
                }

                Assert.That(
                    MinigameController.SpectatorMayAdvance(phase, context),
                    Is.False,
                    $"{phase} is not the results card");
            }
        }

        [Test]
        public void A_controller_with_no_context_yet_asks_nothing_of_the_room()
        {
            Assert.That(
                MinigameController.SpectatorMayAdvance(MinigamePhase.Finished, null),
                Is.False,
                "a scene whose Initialize has not run has no seats to count");
        }

        [Test]
        public void The_rules_card_never_needed_a_press_to_leave()
        {
            RoundClock clock = new RoundClock(30f);
            clock.Begin();

            Assert.That(
                clock.Advance(RoundClock.DefaultBriefingAutoAdvance, false).Has(RoundEvent.BriefingHidden),
                Is.True,
                "which is why the results card is the only screen that has to listen to the room");
        }

        [Test]
        public void A_board_round_says_where_its_keys_actually_go()
        {
            string prompt = MinigameHud.ResultsPrompt(canReturnToMenu: true, returnsToBoard: true);

            Assert.That(prompt, Does.Contain("board"));
            Assert.That(prompt, Does.Not.Contain("menu"), "a board round leaves to the ring, not the select screen");
            Assert.That(
                prompt,
                Does.Not.Contain("play again"),
                "the stake was paid the moment the round resolved, so there is nothing to replay for");
        }

        [Test]
        public void A_menu_launched_round_still_offers_a_retry_and_the_way_out()
        {
            string prompt = MinigameHud.ResultsPrompt(canReturnToMenu: true, returnsToBoard: false);

            Assert.That(prompt, Does.Contain("play again"));
            Assert.That(prompt, Does.Contain("Esc"));
            Assert.That(prompt, Does.Contain("menu"));
        }

        [Test]
        public void Direct_play_is_not_offered_a_menu_that_was_never_loaded()
        {
            string prompt = MinigameHud.ResultsPrompt(canReturnToMenu: false, returnsToBoard: false);

            Assert.That(prompt, Does.Contain("play again"));
            Assert.That(prompt, Does.Not.Contain("Esc"), "there is no select screen behind a scene played directly");
        }

        private static MinigameContext AllCpu()
        {
            return Context(ControllerKind.Cpu, ControllerKind.Cpu);
        }

        private static MinigameContext Context(ControllerKind one, ControllerKind two)
        {
            MatchSetup setup = new MatchSetup
            {
                Minigame = MinigameId.BumperSparks,
                SlotOneKind = one,
                SlotTwoKind = two,
            };

            return new MinigameContext(MinigameLibrary.Get(setup.Minigame), setup, isDirectPlay: false);
        }

        private static bool AnySeatIsAPerson(MinigameContext context)
        {
            foreach (Participant participant in context.Participants)
            {
                if (!participant.IsCpu)
                {
                    return true;
                }
            }

            return false;
        }

        private static RoundClock AtResults()
        {
            RoundClock clock = new RoundClock(1f);
            clock.Begin();
            clock.Advance(RoundClock.DefaultBriefingAutoAdvance, false);

            for (int step = 0; step < 4; step++)
            {
                clock.Advance(RoundClock.DefaultCountdownStep, false);
            }

            clock.Advance(1f, false);
            clock.Resolve(MinigameOutcome.Lose());
            clock.Advance(RoundClock.DefaultSettleDuration, false);

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Finished), "the card is up before the test starts");
            return clock;
        }
    }
}
