using NUnit.Framework;
using SushiParty.Board;
using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class BoardSessionTests
    {
        private static BoardLayout RingOf(params (int index, SpaceKind kind)[] overrides)
        {
            const int count = 24;
            SpaceKind[] kinds = new SpaceKind[count];
            Vector2[] positions = new Vector2[count];
            int[][] exits = new int[count][];

            for (int i = 0; i < count; i++)
            {
                kinds[i] = SpaceKind.CoinGain;
                positions[i] = new Vector2(i, 0f);
                exits[i] = new[] { (i + 1) % count };
            }

            kinds[0] = SpaceKind.Start;
            foreach ((int index, SpaceKind kind) in overrides)
            {
                kinds[index] = kind;
            }

            return new BoardLayout(kinds, positions, exits);
        }

        private static BoardLayout ForkedLoop()
        {
            SpaceKind[] kinds = new SpaceKind[11];
            Vector2[] positions = new Vector2[11];
            int[][] exits = new int[11][];

            for (int i = 0; i < kinds.Length; i++)
            {
                kinds[i] = SpaceKind.CoinGain;
                positions[i] = new Vector2(i, 0f);
                exits[i] = new[] { (i + 1) % 10 };
            }

            kinds[0] = SpaceKind.Start;
            exits[2] = new[] { 3, 10 };   // the fork
            exits[10] = new[] { 6 };      // the chord rejoins
            exits[9] = new[] { 0 };

            return new BoardLayout(kinds, positions, exits);
        }

        private static TurnReport Play(BoardSession session)
        {
            return session.TakeTurn();
        }

        [Test]
        public void Turns_run_player_one_then_two_then_chef()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));

            Assert.That(s.Turn, Is.EqualTo(BoardSeat.One));
            Play(s);
            Assert.That(s.Turn, Is.EqualTo(BoardSeat.Two));
            Play(s);
            Assert.That(s.Turn, Is.EqualTo(BoardSeat.Chef));
            Play(s);
            Assert.That(s.AwaitingMinigame, Is.True, "the round ends with a minigame owed");
        }

        [Test]
        public void A_round_will_not_take_a_fourth_turn_before_its_minigame()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));
            Play(s);
            Play(s);
            Play(s);

            Assert.Throws<System.InvalidOperationException>(() => s.BeginTurn());
        }

        [Test]
        public void Completing_a_turn_that_never_began_is_rejected()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));

            Assert.Throws<System.InvalidOperationException>(() => s.CompleteTurn());
        }

        [Test]
        public void A_roll_moves_that_many_spaces()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(4));

            TurnReport r = Play(s);

            Assert.That(r.Rolled, Is.EqualTo(4));
            Assert.That(r.From, Is.EqualTo(0));
            Assert.That(r.To, Is.EqualTo(4));
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(4));
        }

        [Test]
        public void Movement_wraps_around_the_ring()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(6));
            for (int round = 0; round < 4; round++)
            {
                Play(s); // one
                Play(s); // two
                Play(s); // chef
                s.ApplyMinigameOutcome(true);
            }

            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(0));
            Assert.That(s.TokenFor(BoardSeat.One).TotalSteps, Is.EqualTo(24));
        }

        [Test]
        public void A_move_is_walked_one_space_at_a_time()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(3));

            TurnPlan plan = s.BeginTurn();
            Assert.That(plan.Rolled, Is.EqualTo(3));
            Assert.That(s.StepsRemaining, Is.EqualTo(3));
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(0), "rolling does not move anyone");

            Assert.That(s.StepOnce(), Is.True);
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(1));
            Assert.That(s.StepOnce(), Is.True);
            Assert.That(s.StepOnce(), Is.False, "the last step reports the walk is over");

            TurnReport report = s.CompleteTurn();
            Assert.That(report.To, Is.EqualTo(3));
        }

        [Test]
        public void A_turn_cannot_be_completed_mid_walk()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(3));
            s.BeginTurn();
            s.StepOnce();

            Assert.Throws<System.InvalidOperationException>(() => s.CompleteTurn());
        }

        [Test]
        public void A_face_the_player_stopped_on_moves_that_many_spaces()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));

            TurnPlan plan = s.BeginTurn(4);
            Assert.That(plan.Rolled, Is.EqualTo(4), "the face they stopped on is the number they get");
            Assert.That(plan.Skipped, Is.False);
            Assert.That(plan.From, Is.EqualTo(0));
            Assert.That(s.StepsRemaining, Is.EqualTo(4));
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(0), "stopping the block does not move anyone");

            while (s.StepsRemaining > 0)
            {
                s.StepOnce();
            }

            TurnReport report = s.CompleteTurn();

            Assert.That(report.Rolled, Is.EqualTo(4));
            Assert.That(report.To, Is.EqualTo(4));
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(4));
        }

        [Test]
        public void A_face_the_player_stopped_on_does_not_consume_the_die()
        {
            ScriptedDie die = new ScriptedDie(2, 5);
            BoardSession s = new BoardSession(RingOf(), die);

            s.BeginTurn(4);
            Assert.That(die.RollCount, Is.EqualTo(0), "nobody asked the die anything");

            while (s.StepsRemaining > 0)
            {
                s.StepOnce();
            }

            s.CompleteTurn();

            Assert.That(s.BeginTurn().Rolled, Is.EqualTo(2), "the next seat still gets the roll it was owed");
            Assert.That(die.RollCount, Is.EqualTo(1));
        }

        [Test]
        public void Only_a_face_the_die_actually_has_is_accepted()
        {
            ScriptedDie die = new ScriptedDie(1);
            BoardSession s = new BoardSession(RingOf(), die);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.BeginTurn(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.BeginTurn(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.BeginTurn(die.Faces + 1));
            Assert.DoesNotThrow(
                () => s.BeginTurn(die.Faces),
                "the top face is a real face, and no rejected one left a half-started turn behind");

            BoardSession fresh = new BoardSession(RingOf(), new ScriptedDie(1));
            Assert.DoesNotThrow(() => fresh.BeginTurn(1), "and so is the bottom one");
        }

        [Test]
        public void A_skipped_turn_ignores_the_face_it_was_handed()
        {
            BoardSession s = new BoardSession(RingOf((2, SpaceKind.Wasabi)), new ScriptedDie(2));

            Play(s); // one takes the wasabi
            Play(s); // two
            Play(s); // chef
            s.ApplyMinigameOutcome(true);

            int before = s.TokenFor(BoardSeat.One).Space;
            TurnPlan plan = s.BeginTurn(6);

            Assert.That(plan.Skipped, Is.True);
            Assert.That(plan.Rolled, Is.EqualTo(0), "a sit-out has no number, however hard the block was hit");
            Assert.That(s.StepsRemaining, Is.EqualTo(0));

            TurnReport report = s.CompleteTurn();

            Assert.That(report.Skipped, Is.True);
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(before), "he does not move");
            Assert.That(s.TokenFor(BoardSeat.One).SkipTurns, Is.EqualTo(0), "and the debt is paid");
        }

        [Test]
        public void A_face_cannot_be_handed_in_mid_walk()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));
            s.BeginTurn(3);
            s.StepOnce();

            Assert.Throws<System.InvalidOperationException>(() => s.BeginTurn(4), "a turn is already under way");
            Assert.Throws<System.InvalidOperationException>(() => s.BeginTurn(), "by either door");
            Assert.Throws<System.InvalidOperationException>(() => s.CompleteTurn(), "and it is not finished");
        }

        [Test]
        public void A_face_cannot_be_handed_in_before_the_rounds_minigame()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));
            Play(s);
            Play(s);
            Play(s);

            Assert.Throws<System.InvalidOperationException>(() => s.BeginTurn(4));
        }

        [Test]
        public void A_stopped_turn_hands_the_board_on_when_it_is_done()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));
            s.BeginTurn(2);
            s.StepOnce();
            s.StepOnce();
            s.CompleteTurn();

            Assert.That(s.Turn, Is.EqualTo(BoardSeat.Two));
            Assert.Throws<System.InvalidOperationException>(() => s.CompleteTurn(), "no turn is in progress");
            Assert.Throws<System.InvalidOperationException>(() => s.StepOnce(), "and nothing left to walk");
        }

        [Test]
        public void A_fork_stops_the_walk_until_someone_answers_it()
        {
            BoardSession s = new BoardSession(ForkedLoop(), new ScriptedDie(4));
            s.BeginTurn();
            s.StepOnce(); // 0 -> 1
            s.StepOnce(); // 1 -> 2, which forks

            Assert.That(s.AwaitingChoice, Is.True);
            Assert.That(s.Choices, Is.EquivalentTo(new[] { 3, 10 }));
            Assert.Throws<System.InvalidOperationException>(() => s.StepOnce(), "cannot walk through an unanswered fork");
        }

        [Test]
        public void Taking_the_long_way_and_the_chord_end_up_somewhere_different()
        {
            BoardSession longWay = new BoardSession(ForkedLoop(), new ScriptedDie(4));
            longWay.TakeTurn(options => options[0]);   // 0,1,2 -> 3 -> 4
            Assert.That(longWay.TokenFor(BoardSeat.One).Space, Is.EqualTo(4));

            BoardSession chord = new BoardSession(ForkedLoop(), new ScriptedDie(4));
            chord.TakeTurn(options => options[1]);     // 0,1,2 -> 10 -> 6
            Assert.That(chord.TokenFor(BoardSeat.One).Space, Is.EqualTo(6), "the chord skips ahead");
        }

        [Test]
        public void Only_a_real_way_on_can_be_chosen()
        {
            BoardSession s = new BoardSession(ForkedLoop(), new ScriptedDie(4));
            s.BeginTurn();
            s.StepOnce();
            s.StepOnce();

            Assert.Throws<System.ArgumentException>(() => s.ChooseExit(7));
        }

        [Test]
        public void Choosing_when_there_is_no_fork_is_rejected()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(3));
            s.BeginTurn();

            Assert.Throws<System.InvalidOperationException>(() => s.ChooseExit(1));
        }

        [Test]
        public void A_fork_answered_once_does_not_stay_answered()
        {
            BoardSession s = new BoardSession(ForkedLoop(), new ScriptedDie(6));
            s.BeginTurn();
            s.StepOnce();
            s.StepOnce();
            s.ChooseExit(3);
            s.StepOnce();

            Assert.That(s.AwaitingChoice, Is.False);
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(3));
            Assert.DoesNotThrow(() => s.StepOnce(), "a plain space needs no answer");
        }

        [Test]
        public void A_plate_of_coins_pays_out()
        {
            BoardSession s = new BoardSession(RingOf((2, SpaceKind.CoinGain)), new ScriptedDie(2), startingCoins: 5);

            TurnReport r = Play(s);

            Assert.That(r.Landed, Is.EqualTo(SpaceKind.CoinGain));
            Assert.That(r.CoinDelta, Is.EqualTo(BoardSession.CoinGainAmount));
            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(5 + BoardSession.CoinGainAmount));
        }

        [Test]
        public void Chef_Takos_cut_costs_coins()
        {
            BoardSession s = new BoardSession(RingOf((2, SpaceKind.CoinLoss)), new ScriptedDie(2), startingCoins: 5);

            TurnReport r = Play(s);

            Assert.That(r.CoinDelta, Is.EqualTo(-BoardSession.CoinLossAmount));
            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(2));
        }

        [Test]
        public void Coins_never_go_negative()
        {
            BoardSession s = new BoardSession(RingOf((2, SpaceKind.CoinLoss)), new ScriptedDie(2), startingCoins: 1);

            Play(s);

            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(0));
        }

        [Test]
        public void Wasabi_costs_the_next_turn()
        {
            BoardSession s = new BoardSession(RingOf((2, SpaceKind.Wasabi)), new ScriptedDie(2));

            TurnReport hit = Play(s);
            Assert.That(hit.LostNextTurn, Is.True);
            Assert.That(s.TokenFor(BoardSeat.One).SkipTurns, Is.EqualTo(1));

            Play(s); // two
            Play(s); // chef
            s.ApplyMinigameOutcome(true);

            int before = s.TokenFor(BoardSeat.One).Space;
            TurnReport skipped = Play(s);

            Assert.That(skipped.Skipped, Is.True);
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(before), "he does not move");
            Assert.That(s.TokenFor(BoardSeat.One).SkipTurns, Is.EqualTo(0), "and the debt is paid");
        }

        [Test]
        public void The_shrine_sells_a_golden_coin_when_you_can_pay()
        {
            BoardSession s = new BoardSession(
                RingOf((3, SpaceKind.Shrine)), new ScriptedDie(3), startingCoins: BoardSession.ShrinePrice);

            TurnReport r = Play(s);

            Assert.That(r.ClaimedGoldenCoin, Is.True);
            Assert.That(s.TokenFor(BoardSeat.One).GoldenCoins, Is.EqualTo(1));
            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(0));
        }

        [Test]
        public void The_shrine_turns_you_away_if_you_cannot_pay()
        {
            BoardSession s = new BoardSession(
                RingOf((3, SpaceKind.Shrine)), new ScriptedDie(3), startingCoins: BoardSession.ShrinePrice - 1);

            TurnReport r = Play(s);

            Assert.That(r.ClaimedGoldenCoin, Is.False);
            Assert.That(s.TokenFor(BoardSeat.One).GoldenCoins, Is.EqualTo(0));
            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(BoardSession.ShrinePrice - 1), "and keeps his Coins");
        }

        [Test]
        public void Claiming_moves_the_shrine_so_it_cannot_be_camped()
        {
            BoardSession s = new BoardSession(
                RingOf((3, SpaceKind.Shrine)), new ScriptedDie(3), startingCoins: BoardSession.ShrinePrice);

            Assert.That(s.ShrineSpace, Is.EqualTo(3));
            Play(s);

            Assert.That(s.ShrineSpace, Is.Not.EqualTo(3), "it has to move on");
            Assert.That(s.ShrineSpace, Is.GreaterThanOrEqualTo(0), "and there is still exactly one");
        }

        [Test]
        public void The_shrine_never_lands_on_start()
        {
            for (int roll = 1; roll <= 6; roll++)
            {
                BoardSession s = new BoardSession(
                    RingOf((3, SpaceKind.Shrine)), new ScriptedDie(3, roll), startingCoins: BoardSession.ShrinePrice);
                Play(s);

                Assert.That(s.Layout.KindAt(0), Is.EqualTo(SpaceKind.Start), $"clobbered Start on roll {roll}");
            }
        }

        [Test]
        public void A_current_swaps_you_with_whoever_is_furthest_ahead()
        {
            BoardSession s = new BoardSession(RingOf((7, SpaceKind.Swap)), new ScriptedDie(1, 6, 1, 6));

            Play(s); // one -> 1
            Play(s); // two -> 6
            Play(s); // chef -> 1
            s.ApplyMinigameOutcome(true);

            int twoSpaceBefore = s.TokenFor(BoardSeat.Two).Space;
            TurnReport r = Play(s); // one: 1 + 6 = 7, the Swap

            Assert.That(r.Landed, Is.EqualTo(SpaceKind.Swap));
            Assert.That(r.Swapped, Is.True);
            Assert.That(r.SwappedWith, Is.EqualTo(BoardSeat.Two), "Two has rolled furthest");
            Assert.That(s.TokenFor(BoardSeat.One).Space, Is.EqualTo(twoSpaceBefore));
            Assert.That(s.TokenFor(BoardSeat.Two).Space, Is.EqualTo(7));
        }

        [Test]
        public void Winning_the_minigame_pays_both_octopuses_the_stake()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1), startingCoins: 5);
            Play(s); Play(s); Play(s);

            s.ApplyMinigameOutcome(pairWon: true);

            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(5 + 3 + MinigameLibrary.CoinStake));
            Assert.That(s.TokenFor(BoardSeat.Two).Coins, Is.EqualTo(5 + 3 + MinigameLibrary.CoinStake));
        }

        [Test]
        public void Losing_hands_what_they_lose_straight_to_chef_tako()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1), startingCoins: 20);
            Play(s); Play(s); Play(s);
            int chefBefore = s.TokenFor(BoardSeat.Chef).Coins;

            s.ApplyMinigameOutcome(pairWon: false);

            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(23 - MinigameLibrary.CoinStake));
            Assert.That(
                s.TokenFor(BoardSeat.Chef).Coins,
                Is.EqualTo(chefBefore + MinigameLibrary.CoinStake * 2),
                "he takes five from each of them");
        }

        [Test]
        public void A_broke_octopus_cannot_be_taken_below_zero()
        {
            BoardSession s = new BoardSession(RingOf((1, SpaceKind.CoinLoss)), new ScriptedDie(1), startingCoins: 1);
            Play(s); Play(s); Play(s);

            s.ApplyMinigameOutcome(pairWon: false);

            Assert.That(s.TokenFor(BoardSeat.One).Coins, Is.EqualTo(0));
        }

        [Test]
        public void Settling_the_minigame_starts_the_next_round_at_player_one()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1), totalRounds: 3);
            Play(s); Play(s); Play(s);

            s.ApplyMinigameOutcome(true);

            Assert.That(s.Round, Is.EqualTo(2));
            Assert.That(s.Turn, Is.EqualTo(BoardSeat.One));
            Assert.That(s.Finished, Is.False);
        }

        [Test]
        public void A_minigame_cannot_be_settled_twice()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));
            Play(s); Play(s); Play(s);
            s.ApplyMinigameOutcome(true);

            Assert.Throws<System.InvalidOperationException>(() => s.ApplyMinigameOutcome(true));
        }

        [Test]
        public void The_session_ends_after_its_last_round()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1), totalRounds: 2);

            for (int round = 0; round < 2; round++)
            {
                Play(s); Play(s); Play(s);
                s.ApplyMinigameOutcome(true);
            }

            Assert.That(s.Finished, Is.True);
            Assert.Throws<System.InvalidOperationException>(() => s.BeginTurn());
        }

        [Test]
        public void The_pair_win_on_combined_golden_coins()
        {
            BoardSession s = new BoardSession(
                RingOf((2, SpaceKind.Shrine)),
                new ScriptedDie(2, 1, 2, 1, 1),
                totalRounds: 1,
                startingCoins: BoardSession.ShrinePrice);

            Play(s); // one lands the shrine, claims, shrine relocates (consumes a roll)
            Play(s); // two
            Play(s); // chef
            s.ApplyMinigameOutcome(true);

            BoardResult result = s.Result();

            Assert.That(result.PairGoldenCoins, Is.GreaterThan(0));
            Assert.That(result.ChefGoldenCoins, Is.EqualTo(0));
            Assert.That(result.PairWon, Is.True);
        }

        [Test]
        public void A_drawn_board_with_no_golden_coins_goes_to_chef_tako()
        {
            BoardSession s = new BoardSession(RingOf((1, SpaceKind.CoinLoss)), new ScriptedDie(1), totalRounds: 1);
            Play(s); Play(s); Play(s);
            s.ApplyMinigameOutcome(pairWon: false);

            BoardResult result = s.Result();

            Assert.That(result.PairGoldenCoins, Is.EqualTo(result.ChefGoldenCoins));
            Assert.That(result.PairWon, Is.False, "the house edge every party game gives its rival");
        }

        [Test]
        public void Chef_Tako_is_a_mover_but_never_a_participant()
        {
            BoardSession s = new BoardSession(RingOf(), new ScriptedDie(1));

            Assert.That(s.TokenFor(BoardSeat.Chef).IsPlayer, Is.False);
            Assert.That(s.TokenFor(BoardSeat.One).IsPlayer, Is.True);
            Assert.That(s.TokenFor(BoardSeat.Two).IsPlayer, Is.True);
        }
    }

    public sealed class BoardLayoutTests
    {
        [Test]
        public void The_default_board_has_exactly_one_shrine_and_starts_at_zero()
        {
            BoardLayout layout = BoardLayout.CreateDefault();

            Assert.That(layout.IndicesOf(SpaceKind.Shrine).Count, Is.EqualTo(1));
            Assert.That(layout.KindAt(0), Is.EqualTo(SpaceKind.Start));
        }

        [Test]
        public void The_default_board_has_forks_in_it()
        {
            BoardLayout layout = BoardLayout.CreateDefault();

            int junctions = 0;
            for (int i = 0; i < layout.Count; i++)
            {
                if (layout.IsJunction(i))
                {
                    junctions++;
                }
            }

            Assert.That(junctions, Is.GreaterThanOrEqualTo(2), "a board with no choices is not a board");
        }

        [Test]
        public void Every_space_leads_somewhere_and_is_reachable_from_the_start()
        {
            BoardLayout layout = BoardLayout.CreateDefault();

            for (int i = 0; i < layout.Count; i++)
            {
                Assert.That(layout.ExitsOf(i).Count, Is.GreaterThan(0), $"space {i} is a dead end");
                Assert.That(
                    layout.StepsBetween(0, i),
                    Is.GreaterThanOrEqualTo(0),
                    $"space {i} cannot be reached from the start");
            }
        }

        [Test]
        public void Every_space_can_get_back_to_the_start_so_nobody_is_stranded()
        {
            BoardLayout layout = BoardLayout.CreateDefault();

            for (int i = 0; i < layout.Count; i++)
            {
                Assert.That(
                    layout.StepsBetween(i, 0),
                    Is.GreaterThanOrEqualTo(0),
                    $"a token on space {i} could never get round again");
            }
        }

        [Test]
        public void The_shortcut_really_is_shorter_than_the_long_way()
        {
            BoardLayout layout = BoardLayout.CreateDefault();
            int shrine = layout.IndicesOf(SpaceKind.Shrine)[0];

            Assert.That(layout.IsJunction(5), Is.True, "space 5 is the big fork");
            Assert.That(layout.StepsBetween(6, shrine), Is.GreaterThan(0), "the long way reaches the shrine");
            Assert.That(
                layout.StepsBetween(5, 15),
                Is.LessThan(layout.StepsBetween(6, 15)),
                "the chord has to actually save steps or the fork is not a decision");
        }

        [Test]
        public void Steps_between_follows_the_arrows_rather_than_the_crow()
        {
            BoardLayout layout = BoardLayout.CreateDefault();

            Assert.That(layout.StepsBetween(0, 1), Is.EqualTo(1));
            Assert.That(layout.StepsBetween(5, 5), Is.EqualTo(0));
            Assert.That(layout.StepsBetween(1, 0), Is.GreaterThan(1), "you cannot walk backwards");
        }

        [Test]
        public void Advance_takes_the_default_route_at_a_fork()
        {
            BoardLayout layout = BoardLayout.CreateDefault();

            Assert.That(layout.Advance(0, 1), Is.EqualTo(1));
            Assert.That(layout.Advance(5, 1), Is.EqualTo(layout.ExitsOf(5)[0]));
        }

        [Test]
        public void A_board_needs_more_than_one_space()
        {
            Assert.Throws<System.ArgumentException>(
                () => new BoardLayout(
                    new[] { SpaceKind.Start },
                    new[] { Vector2.zero },
                    new[] { new[] { 0 } }));
        }

        [Test]
        public void A_dead_end_is_rejected()
        {
            Assert.Throws<System.ArgumentException>(
                () => new BoardLayout(
                    new[] { SpaceKind.Start, SpaceKind.CoinGain },
                    new[] { Vector2.zero, Vector2.one },
                    new[] { new[] { 1 }, new int[0] }));
        }

        [Test]
        public void An_exit_to_nowhere_is_rejected()
        {
            Assert.Throws<System.ArgumentException>(
                () => new BoardLayout(
                    new[] { SpaceKind.Start, SpaceKind.CoinGain },
                    new[] { Vector2.zero, Vector2.one },
                    new[] { new[] { 9 }, new[] { 0 } }));
        }
    }

    public sealed class DieTests
    {
        [Test]
        public void A_scripted_die_returns_its_values_in_order_then_holds_the_last()
        {
            ScriptedDie die = new ScriptedDie(3, 1, 6);

            Assert.That(die.Roll(), Is.EqualTo(3));
            Assert.That(die.Roll(), Is.EqualTo(1));
            Assert.That(die.Roll(), Is.EqualTo(6));
            Assert.That(die.Roll(), Is.EqualTo(6), "holds so a test never runs dry mid-session");
            Assert.That(die.RollCount, Is.EqualTo(4));
        }

        [Test]
        public void A_random_die_only_ever_rolls_one_through_six()
        {
            RandomDie die = new RandomDie(seed: 12345);

            for (int i = 0; i < 500; i++)
            {
                int value = die.Roll();
                Assert.That(value, Is.InRange(1, 6));
            }
        }

        [Test]
        public void The_same_seed_replays_the_same_board()
        {
            RandomDie a = new RandomDie(seed: 99);
            RandomDie b = new RandomDie(seed: 99);

            for (int i = 0; i < 50; i++)
            {
                Assert.That(a.Roll(), Is.EqualTo(b.Roll()));
            }
        }
    }
}
