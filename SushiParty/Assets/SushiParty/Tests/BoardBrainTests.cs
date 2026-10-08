using NUnit.Framework;
using SushiParty.Board;
using SushiParty.Core;
using SushiParty.InputLayer;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class BoardBrainTests
    {
        private const float Frame = 1f / 60f;
        private const int PatienceFrames = 300;
        private const int BigFork = 5;
        private const int LongWay = 6;
        private const int Chord = 21;
        private const int ShrineSpace = 10;
        private const int LongWayToShrine = 4;
        private const int ChordToShrine = 19;
        private const int LongWayOption = 0;
        private const int ChordOption = 1;
        private const int Rich = 12;
        private const int Poor = 2;
        private const int Trials = 240;
        private const float FaceInterval = 0.08f;
        private const int Faces = 6;
        private const float CyclePeriod = FaceInterval * Faces;
        private const float LongestBoardWait = 2.6f;
        private const int NoAnswer = int.MinValue;

        private static BoardSnapshot Board(
            BoardSeat turn,
            bool awaitingRoll = true,
            int selfSpace = 4,
            int selfCoins = 5,
            bool awaitingStop = false)
        {
            return new BoardSnapshot(
                turn,
                awaitingRoll,
                round: 3,
                totalRounds: 10,
                selfSpace: selfSpace,
                selfCoins: selfCoins,
                selfGoldenCoins: 0,
                shrineSpace: 12,
                distanceToShrine: 12 - selfSpace,
                awaitingStop: awaitingStop);
        }

        private static BoardSnapshot Cycling(BoardSeat turn)
        {
            return Board(turn, awaitingRoll: false, awaitingStop: true);
        }

        private static BoardSnapshot Fork(
            BoardSeat turn,
            int selfCoins,
            int highlighted = LongWayOption,
            bool awaitingChoice = true,
            int[] choices = null,
            int[] stepsToShrine = null)
        {
            return new BoardSnapshot(
                turn,
                awaitingRoll: false,
                round: 3,
                totalRounds: 10,
                selfSpace: BigFork,
                selfCoins: selfCoins,
                selfGoldenCoins: 0,
                shrineSpace: ShrineSpace,
                distanceToShrine: 5,
                awaitingChoice: awaitingChoice,
                choices: choices == null ? new[] { LongWay, Chord } : choices,
                highlightedChoice: highlighted,
                choiceStepsToShrine: stepsToShrine == null
                    ? new[] { LongWayToShrine, ChordToShrine }
                    : stepsToShrine);
        }

        private static BoardBrain BrainFor(BoardSeat seat, BoardSnapshot board, CpuSkill skill = CpuSkill.Standard)
        {
            return new BoardBrain(() => board, seat, skill);
        }

        private static float SecondsUntilItRolls(BoardBrain brain, int frames = PatienceFrames)
        {
            float elapsed = 0f;

            for (int i = 0; i < frames; i++)
            {
                brain.Tick(Frame);
                elapsed += Frame;

                if (brain.WasPressed(MinigameAction.Primary))
                {
                    return elapsed;
                }
            }

            return -1f;
        }

        private static float SecondsUntilItConfirms(BoardBrain brain, int frames = PatienceFrames)
        {
            return SecondsUntilItRolls(brain, frames);
        }

        private static float SecondsUntilItStops(BoardBrain brain, int frames = PatienceFrames)
        {
            return SecondsUntilItRolls(brain, frames);
        }

        private static float[] StopWaitsAcross(int turns, CpuSkill skill)
        {
            BoardSnapshot live = Cycling(BoardSeat.One);
            BoardBrain brain = new BoardBrain(() => live, BoardSeat.One, skill);
            float[] waits = new float[turns];

            for (int turn = 0; turn < turns; turn++)
            {
                live = Cycling(BoardSeat.One);
                waits[turn] = SecondsUntilItStops(brain);

                live = Board(BoardSeat.Two);
                brain.Tick(Frame);
            }

            return waits;
        }

        private static float SpreadOf(float[] waits)
        {
            float shortest = float.MaxValue;
            float longest = float.MinValue;

            for (int i = 0; i < waits.Length; i++)
            {
                Assert.That(waits[i], Is.GreaterThan(0f), "every turn has to end in the block being caught");

                shortest = Mathf.Min(shortest, waits[i]);
                longest = Mathf.Max(longest, waits[i]);
            }

            return longest - shortest;
        }

        private static void AssertEveryFaceGetsCaught(CpuSkill skill)
        {
            float[] waits = StopWaitsAcross(Trials, skill);
            bool[] caught = new bool[Faces];

            for (int i = 0; i < waits.Length; i++)
            {
                Assert.That(waits[i], Is.GreaterThan(0f), "every turn has to end in the block being caught");

                caught[(int)(waits[i] / FaceInterval) % Faces] = true;
            }

            for (int face = 0; face < Faces; face++)
            {
                Assert.That(caught[face], Is.True,
                    $"a {skill} seat never once caught a steady block on face {face + 1} across {Trials} turns, "
                    + "which is a computer player rolling the same handful of numbers all game");
            }
        }

        private static float WhichWayItPushes(BoardBrain brain, int frames = PatienceFrames)
        {
            for (int i = 0; i < frames; i++)
            {
                brain.Tick(Frame);

                if (Mathf.Abs(brain.Move.x) > 0.5f)
                {
                    return brain.Move.x;
                }
            }

            return 0f;
        }

        private static float SecondsUntilItActs(BoardBrain brain, int frames = PatienceFrames)
        {
            float elapsed = 0f;

            for (int i = 0; i < frames; i++)
            {
                brain.Tick(Frame);
                elapsed += Frame;

                if (Mathf.Abs(brain.Move.x) > 0.5f || brain.WasPressed(MinigameAction.Primary))
                {
                    return elapsed;
                }
            }

            return -1f;
        }

        private static int WayOnItTakes(BoardSnapshot fork, CpuSkill skill)
        {
            BoardBrain brain = new BoardBrain(() => fork, fork.Turn, skill);

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                if (brain.Move.x > 0.5f)
                {
                    return fork.HighlightedChoice + 1;
                }

                if (brain.Move.x < -0.5f)
                {
                    return fork.HighlightedChoice - 1;
                }

                if (brain.WasPressed(MinigameAction.Primary))
                {
                    return fork.HighlightedChoice;
                }
            }

            return NoAnswer;
        }

        [Test]
        public void Sits_still_while_another_seat_is_up()
        {
            BoardBrain brain = BrainFor(BoardSeat.Two, Board(BoardSeat.One));

            Assert.That(SecondsUntilItRolls(brain), Is.EqualTo(-1f),
                "rolling on somebody else's turn would take a turn that is not owed to it");
        }

        [Test]
        public void Sits_still_while_the_board_is_not_asking_for_a_roll()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Board(BoardSeat.One, awaitingRoll: false));

            Assert.That(SecondsUntilItRolls(brain), Is.EqualTo(-1f),
                "the board is busy; a press here would be swallowed or, worse, queued");
        }

        [Test]
        public void Chef_Tako_takes_his_own_turn_and_nobody_elses()
        {
            BoardBrain chef = BrainFor(BoardSeat.Chef, Board(BoardSeat.Chef), CpuSkill.Sharp);
            BoardBrain waiting = BrainFor(BoardSeat.Chef, Board(BoardSeat.Two), CpuSkill.Sharp);

            Assert.That(SecondsUntilItRolls(chef), Is.GreaterThan(0f), "he is a mover on this board");
            Assert.That(SecondsUntilItRolls(waiting), Is.EqualTo(-1f));
        }

        [Test]
        public void Waits_a_beat_before_rolling_rather_than_pressing_on_arrival()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Board(BoardSeat.One));

            brain.Tick(Frame);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                "an instant roll reads as a glitch, not as a player");
        }

        [Test]
        public void Rolls_once_its_dwell_has_elapsed()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Board(BoardSeat.One));

            float waited = SecondsUntilItRolls(brain);

            Assert.That(waited, Is.GreaterThan(0f), "a CPU turn must never stall the board");
            Assert.That(waited, Is.LessThan(2f), "and it must not sit there long enough to look broken");
        }

        [Test]
        public void A_sharp_seat_rolls_sooner_than_a_relaxed_one()
        {
            float sharp = SecondsUntilItRolls(BrainFor(BoardSeat.One, Board(BoardSeat.One), CpuSkill.Sharp));
            float relaxed = SecondsUntilItRolls(BrainFor(BoardSeat.One, Board(BoardSeat.One), CpuSkill.Relaxed));

            Assert.That(sharp, Is.GreaterThan(0f));
            Assert.That(relaxed, Is.GreaterThan(0f));

            Assert.That(sharp, Is.LessThan(relaxed), "a relaxed seat dithers; a sharp one is brisk");
        }

        [Test]
        public void Never_steers_while_it_is_only_being_asked_to_roll()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Board(BoardSeat.One));

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.Move, Is.EqualTo(Vector2.zero),
                    "a roll is one button; the stick belongs to the fork and nothing else");
                Assert.That(brain.WasPressed(MinigameAction.Secondary), Is.False,
                    "there is no second button on the board either");
            }
        }

        [Test]
        public void The_wait_starts_over_when_the_turn_moves_on_and_comes_back()
        {
            BoardSnapshot live = Board(BoardSeat.One);
            BoardBrain brain = new BoardBrain(() => live, BoardSeat.One, CpuSkill.Standard);

            for (int i = 0; i < 20; i++)
            {
                brain.Tick(Frame);
            }

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);

            live = Board(BoardSeat.Two);
            brain.Tick(Frame);
            live = Board(BoardSeat.One);

            for (int i = 0; i < 20; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "a half-finished wait must not carry across a turn");
            }

            Assert.That(SecondsUntilItRolls(brain), Is.GreaterThan(0f), "and it does still roll");
        }

        [Test]
        public void A_board_brain_is_still_just_a_participant_input()
        {
            IParticipantInput input = BrainFor(BoardSeat.One, Board(BoardSeat.One), CpuSkill.Sharp);

            bool rolled = false;
            for (int i = 0; i < PatienceFrames && !rolled; i++)
            {
                input.Tick(Frame);
                rolled = input.WasPressed(MinigameAction.Primary);
            }

            Assert.That(rolled, Is.True,
                "the turn loop must not be able to tell this seat from the person sitting next to it");
        }

        [Test]
        public void A_brain_without_a_board_to_read_is_rejected_at_construction()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new BoardBrain(null, BoardSeat.One, CpuSkill.Standard));
        }

        [Test]
        public void A_board_asks_for_one_press_or_the_other_and_never_both()
        {
            BoardSnapshot waiting = Board(BoardSeat.One);
            BoardSnapshot cycling = Cycling(BoardSeat.One);

            Assert.That(waiting.AwaitingRoll, Is.True);
            Assert.That(waiting.AwaitingStop, Is.False, "the block is still sat over its head; there is nothing to catch");

            Assert.That(cycling.AwaitingStop, Is.True);
            Assert.That(cycling.AwaitingRoll, Is.False, "the block is already in the air; it cannot be knocked up twice");
        }

        [Test]
        public void Bangs_the_block_to_a_stop_once_it_has_watched_it_tumble()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Cycling(BoardSeat.One));

            float waited = SecondsUntilItStops(brain);

            Assert.That(waited, Is.GreaterThan(0f), "a block nobody catches is a turn that never ends");
            Assert.That(waited, Is.LessThan(LongestBoardWait), "and one caught this late reads as a seat that has hung");
        }

        [Test]
        public void Lets_the_block_tumble_a_while_rather_than_catching_it_off_the_punch()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Cycling(BoardSeat.One), CpuSkill.Sharp);

            for (int i = 0; i < 18; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "hit and caught in one breath is not a roll, it is a number appearing");
            }
        }

        [Test]
        public void Catches_nobody_elses_block()
        {
            BoardBrain brain = BrainFor(BoardSeat.Two, Cycling(BoardSeat.One));

            Assert.That(SecondsUntilItStops(brain), Is.EqualTo(-1f),
                "stopping somebody else's die picks a number that was theirs to pick");
            Assert.That(brain.Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Sits_still_when_the_board_is_asking_for_neither_press()
        {
            BoardSnapshot idle = Board(BoardSeat.One, awaitingRoll: false);

            Assert.That(idle.AwaitingRoll, Is.False);
            Assert.That(idle.AwaitingStop, Is.False);

            BoardBrain brain = BrainFor(BoardSeat.One, idle);

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "a press with nothing to answer is swallowed or, worse, queued onto the next roll");
                Assert.That(brain.Move, Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void Never_steers_while_it_is_only_being_asked_to_stop()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Cycling(BoardSeat.One));

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.Move, Is.EqualTo(Vector2.zero),
                    "catching the block is one button; there is nothing to steer at a die");
                Assert.That(brain.WasPressed(MinigameAction.Secondary), Is.False);
            }
        }

        [Test]
        public void Bangs_the_block_once_rather_than_hammering_it()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Cycling(BoardSeat.One), CpuSkill.Sharp);

            Assert.That(SecondsUntilItStops(brain), Is.GreaterThan(0f));

            for (int i = 0; i < 15; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "a seat that holds the button down would answer the next question too");
            }
        }

        [Test]
        public void A_half_watched_block_does_not_carry_across_a_turn()
        {
            BoardSnapshot live = Cycling(BoardSeat.One);
            BoardBrain brain = new BoardBrain(() => live, BoardSeat.One, CpuSkill.Sharp);

            for (int i = 0; i < 25; i++)
            {
                brain.Tick(Frame);
            }

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);

            live = Board(BoardSeat.Two);
            brain.Tick(Frame);
            live = Cycling(BoardSeat.One);

            for (int i = 0; i < 25; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "banked watching is a shorter wait, and a shorter wait is a different face — "
                    + "carry it across turns and the seat drifts onto one number and stays there");
            }

            Assert.That(SecondsUntilItStops(brain), Is.GreaterThan(0f), "and it does still catch it");
        }

        [Test]
        public void The_wait_before_it_catches_the_block_is_different_every_turn()
        {
            Random.InitState(20260824);

            float spread = SpreadOf(StopWaitsAcross(Trials, CpuSkill.Standard));

            Assert.That(spread, Is.GreaterThan(CyclePeriod),
                "the spread of waits has to be wider than the block's whole cycle, or the seat "
                + "is landing on the same face over and over and calling it a roll");
        }

        [Test]
        public void A_steady_cycle_would_still_be_caught_on_every_face_there_is()
        {
            Random.InitState(20260824);

            AssertEveryFaceGetsCaught(CpuSkill.Relaxed);
            AssertEveryFaceGetsCaught(CpuSkill.Standard);
            AssertEveryFaceGetsCaught(CpuSkill.Sharp);
        }

        [Test]
        public void A_sharp_seat_is_no_better_at_timing_the_block_than_a_relaxed_one()
        {
            Random.InitState(20260824);

            float sharp = SpreadOf(StopWaitsAcross(Trials, CpuSkill.Sharp));
            float relaxed = SpreadOf(StopWaitsAcross(Trials, CpuSkill.Relaxed));

            Assert.That(sharp, Is.GreaterThan(CyclePeriod), "no seat gets to guess at the face it wants");
            Assert.That(relaxed, Is.GreaterThan(CyclePeriod));

            Assert.That(Mathf.Abs(sharp - relaxed), Is.LessThan(FaceInterval * 2f),
                "sharpness must buy a quicker press, never a tighter grouping on the block");
        }

        [Test]
        public void A_board_with_no_fork_on_it_asks_nothing_and_allocates_nothing()
        {
            BoardSnapshot first = Board(BoardSeat.One);
            BoardSnapshot second = Board(BoardSeat.Two);

            Assert.That(first.AwaitingChoice, Is.False);
            Assert.That(first.Choices, Is.Empty, "empty rather than null, so no brain has to defend itself");
            Assert.That(first.ChoiceStepsToShrine, Is.Empty);

            // Away from a fork both fields share one array, so a snapshot allocates nothing.
            Assert.That(first.ChoiceStepsToShrine, Is.SameAs(first.Choices));
            Assert.That(second.Choices, Is.SameAs(first.Choices));
        }

        [Test]
        public void A_fork_carries_the_options_and_what_they_cost_in_steps()
        {
            BoardSnapshot fork = Fork(BoardSeat.One, Rich);

            Assert.That(fork.AwaitingChoice, Is.True);
            Assert.That(fork.Choices, Is.EqualTo(new[] { LongWay, Chord }));
            Assert.That(fork.ChoiceStepsToShrine, Is.EqualTo(new[] { LongWayToShrine, ChordToShrine }));
            Assert.That(fork.HighlightedChoice, Is.EqualTo(LongWayOption));
        }

        [Test]
        public void Shoves_the_highlight_toward_the_shrine_when_it_can_pay_for_it()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Rich, ChordOption), CpuSkill.Sharp);

            Assert.That(WhichWayItPushes(brain), Is.LessThan(0f),
                "with the price in hand the long way round is the only route worth points");
        }

        [Test]
        public void Shoves_the_highlight_toward_the_shortcut_when_it_cannot()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Poor, LongWayOption), CpuSkill.Sharp);

            Assert.That(WhichWayItPushes(brain), Is.GreaterThan(0f),
                "with nothing to spend, being round again sooner is worth more than passing a shop");
        }

        [Test]
        public void Commits_to_the_long_way_while_it_is_still_a_plate_of_coins_short()
        {
            BoardSnapshot nearly = Fork(BoardSeat.One, selfCoins: BoardSession.ShrinePrice - 3, highlighted: ChordOption);

            Assert.That(WhichWayItPushes(BrainFor(BoardSeat.One, nearly, CpuSkill.Sharp)), Is.LessThan(0f),
                "a seat that will be able to pay by the time it arrives should already be going");
        }

        [Test]
        public void Gives_up_on_the_shrine_once_it_is_further_off_than_one_lobe_can_close()
        {
            BoardSnapshot hopeless = Fork(BoardSeat.One, selfCoins: 5, highlighted: LongWayOption);

            Assert.That(WhichWayItPushes(BrainFor(BoardSeat.One, hopeless, CpuSkill.Sharp)), Is.GreaterThan(0f),
                "walking the long way to a shop it cannot afford is the worst of both bets");
        }

        [Test]
        public void Will_not_confirm_a_way_on_the_highlight_is_not_sitting_on()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Rich, ChordOption), CpuSkill.Sharp);

            Assert.That(SecondsUntilItConfirms(brain), Is.EqualTo(-1f),
                "confirming here would buy the option it spent the whole time steering away from");
        }

        [Test]
        public void Confirms_once_the_highlight_is_already_where_it_wants_it()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Rich, LongWayOption), CpuSkill.Sharp);

            brain.Tick(Frame);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                "a fork answered on the frame it appears reads as the board answering itself");

            float waited = SecondsUntilItConfirms(brain);
            Assert.That(waited, Is.GreaterThan(0f), "and a CPU seat must never leave the question up");
            Assert.That(waited, Is.LessThan(2f), "nor sit in front of it long enough to look stuck");
        }

        [Test]
        public void Leaves_the_stick_alone_when_the_highlight_is_already_right()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Rich, LongWayOption), CpuSkill.Sharp);

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.Move, Is.EqualTo(Vector2.zero),
                    "shuffling a highlight that is already where it wants it would cost it the option");
            }
        }

        [Test]
        public void Nudges_the_highlight_rather_than_leaning_on_the_stick()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Rich, ChordOption), CpuSkill.Sharp);

            int flicks = 0;
            int centred = 0;
            bool pushing = false;

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);
                bool nowPushing = Mathf.Abs(brain.Move.x) > 0.5f;

                if (nowPushing && !pushing)
                {
                    flicks++;
                }

                if (!nowPushing)
                {
                    centred++;
                }

                pushing = nowPushing;
            }

            Assert.That(flicks, Is.GreaterThan(1), "it keeps asking until the highlight moves");
            Assert.That(centred, Is.GreaterThan(0), "and lets go of the stick between asks");
        }

        [Test]
        public void Argues_the_fork_once_rather_than_every_frame()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.One, Rich, ChordOption), CpuSkill.Sharp);

            float first = WhichWayItPushes(brain);
            Assert.That(first, Is.Not.EqualTo(0f), "it does push at some point");

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                if (Mathf.Abs(brain.Move.x) > 0.5f)
                {
                    Assert.That(Mathf.Sign(brain.Move.x), Is.EqualTo(Mathf.Sign(first)),
                        "a brain that re-decided every frame would leave the highlight twitching");
                }
            }
        }

        [Test]
        public void Ignores_a_way_on_that_the_shrine_cannot_be_reached_from()
        {
            BoardSnapshot fork = Fork(
                BoardSeat.One,
                Rich,
                highlighted: LongWayOption,
                stepsToShrine: new[] { -1, 7 });

            Assert.That(WhichWayItPushes(BrainFor(BoardSeat.One, fork, CpuSkill.Sharp)), Is.GreaterThan(0f),
                "a route the prize is not on is no use to a seat that came to buy it");
        }

        [Test]
        public void Takes_the_way_on_the_shrine_is_not_on_when_it_is_broke()
        {
            BoardSnapshot fork = Fork(
                BoardSeat.One,
                Poor,
                highlighted: ChordOption,
                stepsToShrine: new[] { -1, 7 });

            Assert.That(WhichWayItPushes(BrainFor(BoardSeat.One, fork, CpuSkill.Sharp)), Is.LessThan(0f),
                "the branch the shrine is not even on is the one that gets it round again soonest");
        }

        [Test]
        public void Answers_nobody_elses_fork()
        {
            BoardBrain brain = BrainFor(BoardSeat.One, Fork(BoardSeat.Two, Rich, ChordOption), CpuSkill.Sharp);

            Assert.That(SecondsUntilItConfirms(brain), Is.EqualTo(-1f), "that is not its decision to make");
            Assert.That(brain.Move, Is.EqualTo(Vector2.zero), "and not its highlight to shove about");
        }

        [Test]
        public void Leaves_the_fork_alone_once_it_is_no_longer_being_asked()
        {
            BoardSnapshot settled = Fork(BoardSeat.One, Rich, ChordOption, awaitingChoice: false);
            BoardBrain brain = BrainFor(BoardSeat.One, settled, CpuSkill.Sharp);

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "a press into a walking token is either swallowed or queued onto the next roll");
                Assert.That(brain.Move, Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void Does_nothing_at_all_with_a_fork_that_offers_nothing()
        {
            BoardSnapshot broken = Fork(
                BoardSeat.One,
                Rich,
                choices: new int[0],
                stepsToShrine: new int[0]);

            BoardBrain brain = BrainFor(BoardSeat.One, broken, CpuSkill.Sharp);

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                    "there is no press that could be right, so it waits instead of guessing");
                Assert.That(brain.Move, Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void Waits_out_a_highlight_that_is_on_something_it_cannot_see()
        {
            BoardSnapshot fork = Fork(BoardSeat.One, Rich, highlighted: 7);
            BoardBrain brain = BrainFor(BoardSeat.One, fork, CpuSkill.Sharp);

            for (int i = 0; i < PatienceFrames; i++)
            {
                brain.Tick(Frame);

                Assert.That(brain.Move, Is.EqualTo(Vector2.zero),
                    "pushing a highlight it cannot locate is worse than sitting still");
                Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
            }
        }

        [Test]
        public void A_sharp_seat_reads_the_fork_more_reliably_than_a_relaxed_one()
        {
            Random.InitState(20260824);

            BoardSnapshot fork = Fork(BoardSeat.One, Rich, LongWayOption);

            int sharpRight = 0;
            int relaxedRight = 0;

            for (int i = 0; i < Trials; i++)
            {
                if (WayOnItTakes(fork, CpuSkill.Sharp) == LongWayOption)
                {
                    sharpRight++;
                }

                if (WayOnItTakes(fork, CpuSkill.Relaxed) == LongWayOption)
                {
                    relaxedRight++;
                }
            }

            Assert.That(sharpRight, Is.EqualTo(Trials),
                "there is one decision on this board and the sharpest setting owns it");
            Assert.That(relaxedRight, Is.LessThan(sharpRight),
                "a relaxed seat talks itself onto the chord often enough to notice");
            Assert.That(relaxedRight, Is.GreaterThan(Trials / 3),
                "but it is a party game opponent, not a coin toss");
        }

        [Test]
        public void A_relaxed_seat_is_wrong_about_forks_rather_than_slow_at_them()
        {
            Random.InitState(20260824);

            BoardSnapshot fork = Fork(BoardSeat.One, Rich, LongWayOption);

            int wrong = 0;
            for (int i = 0; i < Trials; i++)
            {
                if (WayOnItTakes(fork, CpuSkill.Relaxed) != LongWayOption)
                {
                    wrong++;
                }
            }

            Assert.That(wrong, Is.GreaterThan(0), "a relaxed seat that never erred would just be a sharp one");

            for (int i = 0; i < Trials; i++)
            {
                float acted = SecondsUntilItActs(BrainFor(BoardSeat.One, fork, CpuSkill.Relaxed));

                Assert.That(acted, Is.GreaterThan(0f), "it always answers");
                Assert.That(acted, Is.LessThan(2f), "and always within a beat of being asked");
            }
        }
    }
}
