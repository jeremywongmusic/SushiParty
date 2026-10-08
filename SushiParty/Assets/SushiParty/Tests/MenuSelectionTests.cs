using NUnit.Framework;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.Menu;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class MenuSelectionTests
    {
        private static MenuSelection Menu()
        {
            return Menu(new MatchSetup());
        }

        private static MenuSelection Menu(MatchSetup setup)
        {
            return new MenuSelection(MinigameLibrary.All, setup);
        }

        private static MinigameDefinition Fake(MinigameId id, bool implemented = true)
        {
            return new MinigameDefinition(
                id,
                id.ToString(),
                id.ToString(),
                "objective",
                "seat one",
                "seat two",
                30f,
                implemented,
                Color.white,
                "design notes");
        }

        private static IReadOnlyList<MinigameDefinition> OneRow()
        {
            return new List<MinigameDefinition>
            {
                Fake(MinigameId.BumperSparks),
                Fake(MinigameId.SandTrap),
                Fake(MinigameId.CrossfireCaverns),
            };
        }

        [Test]
        public void The_roster_is_the_two_by_three_grid_these_tests_assume()
        {
            MenuSelection menu = Menu();

            Assert.That(menu.Columns, Is.EqualTo(3));
            Assert.That(menu.Entries.Count, Is.EqualTo(6), "the wrap rules below are written for a full 2x3 grid");
        }

        [Test]
        public void The_cursor_opens_on_whatever_the_setup_points_at()
        {
            MatchSetup setup = new MatchSetup { Minigame = MinigameId.SandTrap };

            MenuSelection menu = Menu(setup);

            Assert.That(menu.Selected.Id, Is.EqualTo(MinigameId.SandTrap));
        }

        [Test]
        public void An_unknown_target_falls_back_to_the_first_card()
        {
            MatchSetup setup = new MatchSetup { Minigame = (MinigameId)99 };

            MenuSelection menu = Menu(setup);

            Assert.That(menu.SelectedIndex, Is.EqualTo(0), "the screen always opens on something");
        }

        [Test]
        public void Left_off_the_start_of_a_row_wraps_to_its_far_end()
        {
            MenuSelection menu = Menu();

            Assert.That(menu.Navigate(Vector2Int.left), Is.True);
            Assert.That(menu.SelectedIndex, Is.EqualTo(2));
        }

        [Test]
        public void Right_off_the_end_of_a_row_wraps_back_to_its_start()
        {
            MenuSelection menu = Menu();
            menu.SelectIndex(2);

            menu.Navigate(Vector2Int.right);

            Assert.That(menu.SelectedIndex, Is.EqualTo(0), "the wrap must not carry into the row below");
        }

        [Test]
        public void The_second_row_wraps_inside_itself_too()
        {
            MenuSelection menu = Menu();
            menu.SelectIndex(3);

            menu.Navigate(Vector2Int.left);
            Assert.That(menu.SelectedIndex, Is.EqualTo(5));

            menu.Navigate(Vector2Int.right);
            Assert.That(menu.SelectedIndex, Is.EqualTo(3));
        }

        [Test]
        public void Stepping_along_a_row_moves_one_card_at_a_time()
        {
            MenuSelection menu = Menu();

            for (int expected = 1; expected <= 2; expected++)
            {
                Assert.That(menu.Navigate(Vector2Int.right), Is.True);
                Assert.That(menu.SelectedIndex, Is.EqualTo(expected));
            }
        }

        [Test]
        public void Up_and_down_swap_rows_and_hold_the_column()
        {
            MenuSelection menu = Menu();
            menu.SelectIndex(2);

            menu.Navigate(Vector2Int.down);
            Assert.That(menu.SelectedIndex, Is.EqualTo(5), "screen down is the row below");

            menu.Navigate(Vector2Int.down);
            Assert.That(menu.SelectedIndex, Is.EqualTo(2), "and the bottom row wraps back to the top");

            menu.Navigate(Vector2Int.up);
            Assert.That(menu.SelectedIndex, Is.EqualTo(5));
        }

        [Test]
        public void A_diagonal_moves_both_axes_in_one_step()
        {
            MenuSelection menu = Menu();

            menu.Navigate(new Vector2Int(1, -1));

            Assert.That(menu.SelectedIndex, Is.EqualTo(4), "one right and one down from the first card");
        }

        [Test]
        public void A_nudge_with_nowhere_to_go_reports_no_move()
        {
            MenuSelection menu = new MenuSelection(OneRow(), new MatchSetup());
            menu.SelectIndex(2);

            Assert.That(menu.Navigate(Vector2Int.up), Is.False, "a single row has nowhere vertical to go");
            Assert.That(menu.SelectedIndex, Is.EqualTo(2));
        }

        [Test]
        public void Hovering_a_card_selects_it_and_reports_only_a_real_move()
        {
            MenuSelection menu = Menu();

            Assert.That(menu.SelectIndex(3), Is.True);
            Assert.That(menu.SelectIndex(3), Is.False, "the pointer is already resting on it");
            Assert.That(menu.SelectedIndex, Is.EqualTo(3));
        }

        [Test]
        public void Pointing_at_nothing_leaves_the_highlight_alone()
        {
            MenuSelection menu = Menu();
            menu.SelectIndex(4);

            Assert.That(menu.SelectIndex(-1), Is.False);
            Assert.That(menu.SelectIndex(6), Is.False);
            Assert.That(menu.SelectedIndex, Is.EqualTo(4));
        }

        [Test]
        public void Seat_one_toggles_between_a_person_and_the_partner_AI()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);

            Assert.That(menu.SeatKind(ParticipantSlot.One), Is.EqualTo(ControllerKind.Human));

            menu.ToggleSeat(ParticipantSlot.One);
            Assert.That(menu.SeatKind(ParticipantSlot.One), Is.EqualTo(ControllerKind.Cpu));
            Assert.That(setup.SlotOneKind, Is.EqualTo(ControllerKind.Cpu), "written through to the live setup");

            menu.ToggleSeat(ParticipantSlot.One);
            Assert.That(menu.SeatKind(ParticipantSlot.One), Is.EqualTo(ControllerKind.Human), "two states, no third");
        }

        [Test]
        public void The_two_seats_toggle_independently()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);

            menu.ToggleSeat(ParticipantSlot.Two);

            Assert.That(setup.SlotTwoKind, Is.EqualTo(ControllerKind.Human), "a second person took the partner's chair");
            Assert.That(setup.SlotOneKind, Is.EqualTo(ControllerKind.Human), "seat 1 must not move with it");
        }

        [Test]
        public void Partner_skill_cycles_relaxed_standard_sharp_and_wraps()
        {
            MatchSetup setup = new MatchSetup { PartnerSkill = CpuSkill.Relaxed };
            MenuSelection menu = Menu(setup);

            menu.CycleSkill(SkillDial.Partner);
            Assert.That(menu.PartnerSkill, Is.EqualTo(CpuSkill.Standard));

            menu.CycleSkill(SkillDial.Partner);
            Assert.That(menu.PartnerSkill, Is.EqualTo(CpuSkill.Sharp));

            menu.CycleSkill(SkillDial.Partner);
            Assert.That(menu.PartnerSkill, Is.EqualTo(CpuSkill.Relaxed), "three notches and back round");
            Assert.That(setup.PartnerSkill, Is.EqualTo(CpuSkill.Relaxed));
        }

        [Test]
        public void Chef_Tako_turns_on_a_dial_of_his_own()
        {
            MatchSetup setup = new MatchSetup { PartnerSkill = CpuSkill.Relaxed, ChefSkill = CpuSkill.Relaxed };
            MenuSelection menu = Menu(setup);

            menu.CycleSkill(SkillDial.Chef);

            Assert.That(menu.ChefSkill, Is.EqualTo(CpuSkill.Standard));
            Assert.That(setup.ChefSkill, Is.EqualTo(CpuSkill.Standard));
            Assert.That(menu.PartnerSkill, Is.EqualTo(CpuSkill.Relaxed), "your partner must not improve because the rival did");
        }

        [Test]
        public void There_is_only_one_copy_of_the_setup()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);

            setup.PartnerSkill = CpuSkill.Sharp;

            Assert.That(menu.PartnerSkill, Is.EqualTo(CpuSkill.Sharp), "the menu edits the live setup rather than a copy of it");
        }

        [Test]
        public void Seat_labels_read_the_way_the_team_panel_prints_them()
        {
            Assert.That(MenuSelection.Describe(ControllerKind.Human), Is.EqualTo("Human"));
            Assert.That(MenuSelection.Describe(ControllerKind.Cpu), Is.EqualTo("CPU"));
        }

        [Test]
        public void The_device_behind_each_seat_is_part_of_the_read_model()
        {
            MenuSelection menu = Menu();

            Assert.That(menu.SeatDevice(ParticipantSlot.One), Is.EqualTo(InputDeviceChoice.KeyboardLeft));
            Assert.That(menu.SeatDevice(ParticipantSlot.Two), Is.EqualTo(InputDeviceChoice.KeyboardRight));
        }

        [Test]
        public void Confirming_records_the_target_and_hands_the_entry_back()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);
            menu.SelectIndex(3);

            MinigameDefinition confirmed = menu.Confirm();

            Assert.That(confirmed, Is.SameAs(menu.Entries[3]));
            Assert.That(setup.Minigame, Is.EqualTo(confirmed.Id), "the target rides on the setup, not on the caller");
        }

        [Test]
        public void An_unbuilt_game_can_still_be_opened()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = new MenuSelection(
                new List<MinigameDefinition> { Fake(MinigameId.ZoomRoom, implemented: false) },
                setup);

            Assert.That(menu.CanLaunch(0), Is.True, "opening it shows the design spec, which is the point of it");
            Assert.That(menu.Confirm(), Is.Not.Null);
            Assert.That(setup.Minigame, Is.EqualTo(MinigameId.ZoomRoom));
        }

        [Test]
        public void The_team_and_the_cursor_both_survive_a_launch()
        {
            MatchSetup setup = new MatchSetup();
            MenuSelection menu = Menu(setup);

            menu.SelectIndex(4);
            menu.ToggleSeat(ParticipantSlot.Two);
            menu.CycleSkill(SkillDial.Chef);
            menu.Confirm();

            MenuSelection reopened = Menu(setup);

            Assert.That(reopened.SelectedIndex, Is.EqualTo(4), "the cursor is still on the card you played");
            Assert.That(reopened.SeatKind(ParticipantSlot.Two), Is.EqualTo(ControllerKind.Human));
            Assert.That(reopened.ChefSkill, Is.EqualTo(CpuSkill.Sharp));
        }
    }
}
