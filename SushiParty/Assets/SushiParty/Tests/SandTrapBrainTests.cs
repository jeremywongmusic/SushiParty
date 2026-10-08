using NUnit.Framework;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Minigames.SandTrap;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class SandTrapBrainTests
    {
        private static readonly Vector2Int Centre = new Vector2Int(2, 2);

        private static SandTrapSeat Seat(
            float track,
            bool grounded = true,
            bool airborne = false,
            bool pounding = false,
            float highlight = 0f)
        {
            return new SandTrapSeat(true, track, grounded, airborne, pounding, highlight);
        }

        private static SandTrapPartnerBrain RowBrain(System.Func<SandTrapSnapshot> world)
        {
            return new SandTrapPartnerBrain(world, ParticipantSlot.One, CpuSkill.Standard);
        }

        private static SandTrapPartnerBrain ColumnBrain(System.Func<SandTrapSnapshot> world)
        {
            return new SandTrapPartnerBrain(world, ParticipantSlot.Two, CpuSkill.Standard);
        }

        private static void SettleIntoTheBeat(SandTrapPartnerBrain brain)
        {
            for (int i = 0; i < 60; i++)
            {
                brain.Tick(0.016f);
            }
        }

        [Test]
        public void Each_seat_walks_along_its_own_axis_toward_the_tile_he_is_stepping_onto()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(
                Seat(0f),
                Seat(0f),
                new Vector2Int(4, 0));

            SandTrapPartnerBrain rows = RowBrain(() => world);
            SandTrapPartnerBrain columns = ColumnBrain(() => world);

            rows.Tick(0.016f);
            columns.Tick(0.016f);

            Assert.That(rows.Move.y, Is.GreaterThan(0.9f), "seat 1 walks up its track toward row 4");
            Assert.That(Mathf.Abs(rows.Move.x), Is.LessThan(0.01f), "and never sideways");

            Assert.That(columns.Move.x, Is.LessThan(-0.9f), "seat 2 walks down its track toward column 0");
            Assert.That(Mathf.Abs(columns.Move.y), Is.LessThan(0.01f), "and never forward");
        }

        [Test]
        public void A_seat_already_on_the_switch_stops_walking()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(Seat(0f), Seat(0f), Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            brain.Tick(0.016f);

            Assert.That(brain.Move.magnitude, Is.LessThan(0.01f),
                "shuffling on the pad would only cost it the pound");
        }

        [Test]
        public void It_opens_with_a_pound_so_a_human_partner_has_a_beat_to_join()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(Seat(0f), Seat(0f), Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            brain.Tick(0.016f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "jumps on its own cadence");

            brain.Tick(0.016f);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False, "one pound at a time");
        }

        [Test]
        public void It_answers_a_partner_s_burning_highlight()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(Seat(0f), Seat(0f), Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            SettleIntoTheBeat(brain);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False, "quiet before the cue");

            world = new SandTrapSnapshot(Seat(0f), Seat(0f, highlight: 1f), Centre);
            brain.Tick(0.016f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True,
                "a lit column is worth nothing until a row crosses it");
        }

        [Test]
        public void It_answers_a_partner_who_is_still_mid_slam()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(Seat(0f), Seat(0f), Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            SettleIntoTheBeat(brain);

            world = new SandTrapSnapshot(Seat(0f), Seat(0f, grounded: false, pounding: true), Centre);
            brain.Tick(0.016f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True);
        }

        [Test]
        public void It_will_not_pound_while_it_is_still_walking_to_the_switch()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(
                Seat(SandGrid.CoordinateFor(4)),
                Seat(0f, highlight: 1f),
                Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            brain.Tick(0.016f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False, "walk first, pound second");
            Assert.That(brain.Move.y, Is.LessThan(-0.9f), "and keep walking back toward the switch");
        }

        [Test]
        public void An_airborne_seat_gets_the_second_press_that_lands_the_slam()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(Seat(0f), Seat(0f), Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            brain.Tick(0.016f);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "the jump");

            world = new SandTrapSnapshot(Seat(0f, grounded: false, airborne: true), Seat(0f), Centre);
            brain.Tick(0.25f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "the slam");
        }

        [Test]
        public void A_seat_whose_character_does_not_exist_yet_stands_still()
        {
            SandTrapSnapshot world = new SandTrapSnapshot(default(SandTrapSeat), Seat(0f), Centre);

            SandTrapPartnerBrain brain = RowBrain(() => world);
            brain.Tick(0.016f);

            Assert.That(brain.Move.magnitude, Is.LessThan(0.01f));
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }
    }
}
