using NUnit.Framework;
using SushiParty.Board;
using SushiParty.Core;
using SushiParty.Menu;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class BoardFlowTests
    {
        private static BoardLayout Ring()
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
            return new BoardLayout(kinds, positions, exits);
        }

        private static BoardSession AwaitingMinigame(int startingCoins = 5, int totalRounds = 10)
        {
            BoardSession session = new BoardSession(Ring(), new ScriptedDie(1), totalRounds, startingCoins);

            for (int turn = 0; turn < 3; turn++)
            {
                session.TakeTurn();
            }

            return session;
        }

        private static void Deliver(BoardSession session, MinigameOutcome result)
        {
            if (MinigameFlow.SettlesBoardRound(session, result))
            {
                session.ApplyMinigameOutcome(result.Won);
            }
        }

        private static MenuSelection Menu(MatchSetup setup)
        {
            return new MenuSelection(MinigameLibrary.All, setup);
        }

        [Test]
        public void A_fresh_setup_opens_one_minigame_rather_than_a_board()
        {
            Assert.That(new MatchSetup().Mode, Is.EqualTo(SessionMode.SingleMinigame));
        }

        [Test]
        public void Direct_play_from_the_editor_is_never_a_board_game()
        {
            Assert.That(MatchSetup.Debug(MinigameId.ZoomRoom).Mode, Is.EqualTo(SessionMode.SingleMinigame));
        }

        [Test]
        public void The_mode_sits_beside_the_team_without_disturbing_it()
        {
            MatchSetup setup = new MatchSetup { Mode = SessionMode.BoardGame };

            Assert.That(setup.Mode, Is.EqualTo(SessionMode.BoardGame));
            Assert.That(setup.SlotOneKind, Is.EqualTo(ControllerKind.Human), "asking for a board must not reseat anyone");
            Assert.That(setup.SlotTwoKind, Is.EqualTo(ControllerKind.Cpu));
            Assert.That(setup.ChefSkill, Is.EqualTo(CpuSkill.Standard));
        }

        [Test]
        public void A_board_game_still_seats_exactly_two()
        {
            MatchSetup setup = new MatchSetup { Mode = SessionMode.BoardGame };

            Participant[] seats = setup.CreateParticipants();

            Assert.That(seats.Length, Is.EqualTo(2), "Chef Tako moves on the board but is never a Participant");
            Assert.That(seats[0].Slot, Is.EqualTo(ParticipantSlot.One));
            Assert.That(seats[1].Slot, Is.EqualTo(ParticipantSlot.Two));
        }

        [Test]
        public void Choosing_the_board_records_it_on_the_live_setup()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);

            menu.ConfirmBoardGame();

            Assert.That(menu.Mode, Is.EqualTo(SessionMode.BoardGame));
            Assert.That(setup.Mode, Is.EqualTo(SessionMode.BoardGame), "written through, not kept on the screen");
        }

        [Test]
        public void Choosing_the_board_leaves_the_highlighted_card_alone()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);
            menu.SelectIndex(4);

            menu.ConfirmBoardGame();

            Assert.That(menu.SelectedIndex, Is.EqualTo(4), "the cursor has not moved");
            Assert.That(
                setup.Minigame,
                Is.EqualTo(MinigameId.BumperSparks),
                "and the tile is only recorded when a tile is what was chosen");
        }

        [Test]
        public void Picking_a_card_afterwards_puts_the_screen_back_to_one_minigame()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);
            menu.ConfirmBoardGame();

            menu.SelectIndex(3);
            MinigameDefinition chosen = menu.Confirm();

            Assert.That(menu.Mode, Is.EqualTo(SessionMode.SingleMinigame), "one card means one game");
            Assert.That(setup.Minigame, Is.EqualTo(chosen.Id));
        }

        [Test]
        public void The_board_inherits_the_seats_and_dials_already_set()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);

            menu.ToggleSeat(ParticipantSlot.Two);
            menu.CycleSkill(SkillDial.Chef);
            menu.ConfirmBoardGame();

            Assert.That(setup.SlotTwoKind, Is.EqualTo(ControllerKind.Human), "a second person is still in seat 2");
            Assert.That(setup.ChefSkill, Is.EqualTo(CpuSkill.Sharp), "and Chef Tako is still as sharp as he was set");
            Assert.That(setup.Mode, Is.EqualTo(SessionMode.BoardGame));
        }

        [Test]
        public void The_cursor_still_wraps_the_way_it_always_did()
        {
            MenuSelection menu = Menu(new MatchSetup());
            menu.ConfirmBoardGame();

            Assert.That(menu.Navigate(Vector2Int.left), Is.True);
            Assert.That(menu.SelectedIndex, Is.EqualTo(2), "left off the start of a row comes back on its far end");
        }

        [Test]
        public void Seats_still_toggle_once_the_board_has_been_chosen()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);
            menu.ConfirmBoardGame();

            menu.ToggleSeat(ParticipantSlot.One);

            Assert.That(menu.SeatKind(ParticipantSlot.One), Is.EqualTo(ControllerKind.Cpu));
            Assert.That(setup.Mode, Is.EqualTo(SessionMode.BoardGame), "and the choice survives the toggle");
        }

        [Test]
        public void The_choice_is_still_there_when_the_screen_is_rebuilt_over_it()
        {
            MatchSetup setup = new MatchSetup();
            Menu(setup).ConfirmBoardGame();

            MenuSelection reopened = Menu(setup);

            Assert.That(reopened.Mode, Is.EqualTo(SessionMode.BoardGame), "the setup is the live one, not a copy");
        }

        [Test]
        public void A_won_minigame_pays_the_round_it_was_launched_for()
        {
            BoardSession session = AwaitingMinigame();
            int before = session.TokenFor(BoardSeat.One).Coins;

            Deliver(session, MinigameOutcome.Win(4.5f));

            Assert.That(session.TokenFor(BoardSeat.One).Coins, Is.EqualTo(before + MinigameLibrary.CoinStake));
            Assert.That(session.AwaitingMinigame, Is.False);
            Assert.That(session.Round, Is.EqualTo(2), "and the board has moved on to the next round");
        }

        [Test]
        public void A_lost_minigame_settles_the_round_just_as_hard()
        {
            BoardSession session = AwaitingMinigame(startingCoins: 20);
            int chefBefore = session.TokenFor(BoardSeat.Chef).Coins;

            Deliver(session, MinigameOutcome.Lose());

            Assert.That(
                session.TokenFor(BoardSeat.Chef).Coins,
                Is.EqualTo(chefBefore + MinigameLibrary.CoinStake * 2),
                "he takes the stake off each of them");
            Assert.That(session.AwaitingMinigame, Is.False);
        }

        [Test]
        public void The_same_result_arriving_twice_only_settles_once()
        {
            BoardSession session = AwaitingMinigame();
            int before = session.TokenFor(BoardSeat.One).Coins;
            MinigameOutcome won = MinigameOutcome.Win(1f);

            Deliver(session, won);
            Deliver(session, won);

            Assert.That(
                session.TokenFor(BoardSeat.One).Coins,
                Is.EqualTo(before + MinigameLibrary.CoinStake),
                "the stake is paid once per round, however many times the result is announced");
            Assert.That(session.Round, Is.EqualTo(2), "and the board advanced one round, not two");
        }

        [Test]
        public void An_abandoned_minigame_settles_nothing()
        {
            BoardSession session = AwaitingMinigame();
            int before = session.TokenFor(BoardSeat.One).Coins;

            Deliver(session, MinigameOutcome.Abandon());

            Assert.That(session.TokenFor(BoardSeat.One).Coins, Is.EqualTo(before), "backing out never carried stakes");
            Assert.That(
                session.AwaitingMinigame,
                Is.True,
                "and Chef Tako must not be handed a round nobody finished playing");
        }

        [Test]
        public void An_abandoned_round_is_still_playable_afterwards()
        {
            BoardSession session = AwaitingMinigame();

            Deliver(session, MinigameOutcome.Abandon());
            Deliver(session, MinigameOutcome.Win(2f));

            Assert.That(session.AwaitingMinigame, Is.False, "the abandon must not have left the board stuck");
            Assert.That(session.Round, Is.EqualTo(2));
        }

        [Test]
        public void A_round_still_being_moved_through_is_never_settled_by_a_minigame()
        {
            BoardSession session = new BoardSession(Ring(), new ScriptedDie(1));
            session.TakeTurn();

            Assert.That(
                MinigameFlow.SettlesBoardRound(session, MinigameOutcome.Win(1f)),
                Is.False,
                "seats two and three have not had their turn yet");
            Assert.That(session.Turn, Is.EqualTo(BoardSeat.Two));
        }

        [Test]
        public void A_finished_session_takes_no_further_results()
        {
            BoardSession session = AwaitingMinigame(totalRounds: 1);
            Deliver(session, MinigameOutcome.Win(1f));

            Assert.That(session.Finished, Is.True);
            Assert.That(MinigameFlow.SettlesBoardRound(session, MinigameOutcome.Win(1f)), Is.False);
        }

        [Test]
        public void A_minigame_with_no_board_behind_it_settles_nothing()
        {
            Assert.That(
                MinigameFlow.SettlesBoardRound(null, MinigameOutcome.Win(1f)),
                Is.False,
                "a game opened straight from a tile has no round to pay");
        }

        [Test]
        public void The_board_has_a_scene_all_of_its_own()
        {
            Assert.That(MinigameFlow.BoardSceneName, Is.Not.EqualTo(MinigameFlow.MenuSceneName));
            Assert.That(
                MinigameLibrary.TryGetBySceneName(MinigameFlow.BoardSceneName, out _),
                Is.False,
                "scenes load single and by name, so no minigame may answer to the board's");
        }

        [Test]
        public void There_is_always_a_built_minigame_for_a_round_to_hand_off_to()
        {
            bool any = false;
            foreach (MinigameDefinition definition in MinigameLibrary.All)
            {
                any |= definition.Implemented;
            }

            Assert.That(any, Is.True, "a round that could find nothing to play would strand its session");
        }
    }
}
