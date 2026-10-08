using System.Collections.Generic;
using NUnit.Framework;
using SushiParty.Core;

namespace SushiParty.Tests
{
    public sealed class RoundClockTests
    {
        [Test]
        public void Nothing_happens_before_the_round_begins()
        {
            RoundClock clock = new RoundClock(30f);

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Idle));
            Assert.That(clock.Outcome.HasValue, Is.False, "nobody has won yet");
            Assert.That(clock.Advance(1f, true).Events, Is.EqualTo(RoundEvent.None));
            Assert.That(clock.TimeRemaining, Is.EqualTo(30f));
        }

        [Test]
        public void A_round_opens_on_the_rules_card()
        {
            RoundClock clock = new RoundClock(30f);

            RoundTick tick = clock.Begin();

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Briefing));
            Assert.That(tick.Has(RoundEvent.BriefingShown), Is.True);
            Assert.That(tick.Has(RoundEvent.PhaseChanged), Is.True);
            Assert.That(tick.Phase, Is.EqualTo(MinigamePhase.Briefing));
        }

        [Test]
        public void The_rules_card_holds_for_eight_seconds()
        {
            RoundClock clock = new RoundClock(30f);
            clock.Begin();

            for (int second = 1; second <= 7; second++)
            {
                Assert.That(
                    clock.Advance(1f, false).Has(RoundEvent.BriefingHidden),
                    Is.False,
                    $"card left after {second}s");
            }

            RoundTick eighth = clock.Advance(1f, false);

            Assert.That(eighth.Has(RoundEvent.BriefingHidden), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Countdown));
        }

        [Test]
        public void A_press_cuts_the_rules_card_short()
        {
            RoundClock clock = new RoundClock(30f);
            clock.Begin();
            clock.Advance(0.5f, false);

            RoundTick tick = clock.Advance(0.016f, true);

            Assert.That(tick.Has(RoundEvent.BriefingHidden), Is.True, "half a second in, not eight");
            Assert.That(tick.Has(RoundEvent.CountdownStepped), Is.True, "the card leaving is also the 3");
            Assert.That(tick.CountdownValue, Is.EqualTo(3));
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Countdown));
        }

        [Test]
        public void The_countdown_is_three_two_one_go_at_point_eight_second_steps()
        {
            RoundClock clock = new RoundClock(30f);
            clock.Begin();

            RoundTick three = clock.Advance(RoundClock.DefaultBriefingAutoAdvance, false);
            Assert.That(three.CountdownValue, Is.EqualTo(3));

            Assert.That(
                clock.Advance(0.4f, false).Events,
                Is.EqualTo(RoundEvent.None),
                "half a step is not a step");

            RoundTick two = clock.Advance(0.4f, false);
            Assert.That(two.Has(RoundEvent.CountdownStepped), Is.True);
            Assert.That(two.CountdownValue, Is.EqualTo(2));

            RoundTick one = clock.Advance(RoundClock.DefaultCountdownStep, false);
            Assert.That(one.Has(RoundEvent.CountdownStepped), Is.True);
            Assert.That(one.CountdownValue, Is.EqualTo(1));

            RoundTick go = clock.Advance(RoundClock.DefaultCountdownStep, false);
            Assert.That(go.Has(RoundEvent.GoShown), Is.True);
            Assert.That(go.Has(RoundEvent.CountdownStepped), Is.False, "GO is not a number");
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Countdown), "GO still belongs to the countdown");

            RoundTick live = clock.Advance(RoundClock.DefaultCountdownStep, false);
            Assert.That(live.Has(RoundEvent.BannerCleared), Is.True);
            Assert.That(live.Has(RoundEvent.PlayBegan), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Playing));
        }

        [Test]
        public void The_clock_does_not_run_until_play_begins()
        {
            RoundClock clock = new RoundClock(30f);
            clock.Begin();
            clock.Advance(RoundClock.DefaultBriefingAutoAdvance, false);
            clock.Advance(RoundClock.DefaultCountdownStep, false);

            Assert.That(clock.TimeRemaining, Is.EqualTo(30f), "the rules card is not on the clock");
        }

        [Test]
        public void Play_frames_drain_the_clock()
        {
            RoundClock clock = AtKickoff(30f);

            RoundTick tick = clock.Advance(0.5f, false);

            Assert.That(tick.Has(RoundEvent.PlayTicked), Is.True);
            Assert.That(tick.Has(RoundEvent.ClockChanged), Is.True);
            Assert.That(tick.DeltaTime, Is.EqualTo(0.5f));
            Assert.That(clock.TimeRemaining, Is.EqualTo(29.5f).Within(1e-4f));
        }

        [Test]
        public void The_clock_stops_at_zero_rather_than_going_negative()
        {
            RoundClock clock = AtKickoff(1f);

            clock.Advance(10f, false);

            Assert.That(clock.TimeRemaining, Is.EqualTo(0f));
            Assert.That(clock.TimeExpired, Is.True);
        }

        [Test]
        public void The_five_second_warning_fires_exactly_once()
        {
            RoundClock clock = AtKickoff(8f);

            int warnings = 0;
            for (int frame = 0; frame < 16; frame++)
            {
                if (clock.Advance(0.5f, false).Has(RoundEvent.ClockWarning))
                {
                    warnings++;
                }
            }

            Assert.That(warnings, Is.EqualTo(1), "one crossing, one warning");
            Assert.That(clock.TimeRemaining, Is.EqualTo(0f));
        }

        [Test]
        public void Time_put_back_on_the_clock_re_arms_the_warning()
        {
            RoundClock clock = AtKickoff(8f);

            int warnings = 0;
            for (int frame = 0; frame < 6; frame++)
            {
                if (clock.Advance(0.5f, false).Has(RoundEvent.ClockWarning))
                {
                    warnings++;
                }
            }

            Assert.That(warnings, Is.EqualTo(1));
            Assert.That(clock.TimeRemaining, Is.EqualTo(5f).Within(1e-4f));

            RoundTick bonus = clock.AddTime(4f);
            Assert.That(bonus.Has(RoundEvent.ClockChanged), Is.True);
            Assert.That(clock.TimeRemaining, Is.EqualTo(9f).Within(1e-4f));

            for (int frame = 0; frame < 8; frame++)
            {
                if (clock.Advance(0.5f, false).Has(RoundEvent.ClockWarning))
                {
                    warnings++;
                }
            }

            Assert.That(warnings, Is.EqualTo(2), "the second crossing is worth hearing too");
        }

        [Test]
        public void Time_running_out_resolves_the_round_as_a_loss()
        {
            RoundClock clock = AtKickoff(1f);

            RoundTick tick = clock.Advance(1f, false);

            Assert.That(tick.Has(RoundEvent.TimeExpired), Is.True);
            Assert.That(clock.TimeExpired, Is.True, "still expired once the game has had its frame");

            RoundTick resolved = clock.Resolve(MinigameOutcome.Lose());

            Assert.That(resolved.Has(RoundEvent.Resolved), Is.True);
            Assert.That(resolved.Has(RoundEvent.PhaseChanged), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Settling));
            Assert.That(clock.Outcome.HasValue, Is.True);
            Assert.That(clock.Outcome.Value.Result, Is.EqualTo(MinigameResult.ChefTakoWins));
            Assert.That(clock.TimeExpired, Is.False, "a settled round cannot expire again");
        }

        [Test]
        public void A_win_during_play_cannot_be_taken_back_by_the_clock()
        {
            RoundClock clock = AtKickoff(1f);
            clock.Advance(0.5f, false);

            clock.Resolve(MinigameOutcome.Win(clock.TimeRemaining, "beat the buzzer"));
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Settling));

            for (int frame = 0; frame < 10; frame++)
            {
                Assert.That(
                    clock.Advance(0.1f, false).Has(RoundEvent.TimeExpired),
                    Is.False,
                    "a settled round has no clock left to run out");
            }

            RoundTick late = clock.Resolve(MinigameOutcome.Lose());

            Assert.That(late.Events, Is.EqualTo(RoundEvent.None), "the second result is ignored");
            Assert.That(clock.Outcome.Value.Won, Is.True);
            Assert.That(clock.TimeRemaining, Is.EqualTo(0.5f).Within(1e-4f), "the clock froze on resolve");
        }

        [Test]
        public void Settling_hands_over_to_the_results_card()
        {
            RoundClock clock = AtKickoff(30f);
            clock.Advance(0.1f, false);
            clock.Resolve(MinigameOutcome.Win(clock.TimeRemaining));

            RoundTick early = clock.Advance(1f, false);
            Assert.That(early.Has(RoundEvent.SettleTicked), Is.True);
            Assert.That(early.Has(RoundEvent.ResultsShown), Is.False, "the banner is still holding");

            RoundTick last = clock.Advance(2f, false);

            Assert.That(last.Has(RoundEvent.SettleTicked), Is.True, "the flourish gets its last frame too");
            Assert.That(last.Has(RoundEvent.BannerCleared), Is.True);
            Assert.That(last.Has(RoundEvent.ResultsShown), Is.True);
            Assert.That(last.Outcome.Won, Is.True, "the results card is handed the outcome");
            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Finished));
        }

        [Test]
        public void A_press_on_the_results_card_asks_for_a_retry()
        {
            RoundClock clock = AtKickoff(1f);
            clock.Advance(1f, false);
            clock.Resolve(MinigameOutcome.Lose());
            clock.Advance(RoundClock.DefaultSettleDuration, false);

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Finished));
            Assert.That(clock.Advance(0.016f, false).Has(RoundEvent.RetryRequested), Is.False);
            Assert.That(clock.Advance(0.016f, true).Has(RoundEvent.RetryRequested), Is.True);
            Assert.That(
                clock.Phase,
                Is.EqualTo(MinigamePhase.Finished),
                "asking is all it does; reloading the scene is not the clock's job");
        }

        [Test]
        public void A_screen_without_the_round_structure_starts_live()
        {
            RoundClock clock = new RoundClock(0f, usesRoundStructure: false);

            RoundTick begin = clock.Begin();

            Assert.That(clock.Phase, Is.EqualTo(MinigamePhase.Playing));
            Assert.That(begin.Has(RoundEvent.PlayBegan), Is.True);
            Assert.That(begin.Has(RoundEvent.BriefingShown), Is.False, "no rules card");

            RoundTick tick = clock.Advance(0.5f, false);

            Assert.That(tick.Has(RoundEvent.PlayTicked), Is.True, "it still gets frames");
            Assert.That(tick.Has(RoundEvent.ClockChanged), Is.False, "but there is no clock");
            Assert.That(clock.TimeExpired, Is.False, "and it can never run out");
        }

        [Test]
        public void The_phase_order_never_varies()
        {
            RoundClock clock = new RoundClock(2f);
            List<MinigamePhase> order = new List<MinigamePhase> { clock.Phase };

            Note(order, clock.Begin());

            for (int frame = 0; frame < 400 && clock.Phase != MinigamePhase.Finished; frame++)
            {
                RoundTick tick = clock.Advance(0.1f, false);
                Note(order, tick);

                if (tick.Has(RoundEvent.PlayTicked) && clock.TimeExpired)
                {
                    Note(order, clock.Resolve(MinigameOutcome.Lose()));
                }
            }

            Assert.That(
                order,
                Is.EqualTo(new[]
                {
                    MinigamePhase.Idle,
                    MinigamePhase.Briefing,
                    MinigamePhase.Countdown,
                    MinigamePhase.Playing,
                    MinigamePhase.Settling,
                    MinigamePhase.Finished,
                }));
        }

        private static RoundClock AtKickoff(float timeLimit)
        {
            RoundClock clock = new RoundClock(timeLimit);
            clock.Begin();
            clock.Advance(RoundClock.DefaultBriefingAutoAdvance, false);

            for (int step = 0; step < 4; step++)
            {
                clock.Advance(RoundClock.DefaultCountdownStep, false);
            }

            return clock;
        }

        private static void Note(List<MinigamePhase> order, RoundTick tick)
        {
            if (tick.Has(RoundEvent.PhaseChanged))
            {
                order.Add(tick.Phase);
            }
        }
    }
}
