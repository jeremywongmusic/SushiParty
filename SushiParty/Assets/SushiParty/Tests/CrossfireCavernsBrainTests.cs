using NUnit.Framework;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Minigames.CrossfireCaverns;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class CrossfireCavernsBrainTests
    {
        private static CrossfireCartSnapshot Cart(
            float x,
            bool disabled = false,
            bool canFire = true,
            bool laneClear = true)
        {
            return new CrossfireCartSnapshot(x, disabled, canFire, laneClear);
        }

        private static CrossfireBrain Gunner(
            ParticipantSlot slot,
            Vector2 chef,
            bool chefVulnerable,
            CrossfireCartSnapshot one,
            CrossfireCartSnapshot two)
        {
            CrossfireCavernsSnapshot world =
                new CrossfireCavernsSnapshot(chef, chefVulnerable, one, two);

            return new CrossfireBrain(() => world, slot, CpuSkill.Sharp);
        }

        [Test]
        public void A_gunner_runs_its_cart_along_the_rail_toward_Chef_Takos_x()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(6f, 2f),
                chefVulnerable: false,
                Cart(0f),
                Cart(-9f));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.GreaterThan(0.9f), "he is off to the right");
            Assert.That(Mathf.Abs(brain.Move.y), Is.LessThan(0.01f), "the cart is on a rail");
        }

        [Test]
        public void Each_seat_reads_its_own_rail()
        {
            CrossfireCartSnapshot one = Cart(-4f);
            CrossfireCartSnapshot two = Cart(4f);

            CrossfireBrain first = Gunner(ParticipantSlot.One, Vector2.zero, false, one, two);
            CrossfireBrain second = Gunner(ParticipantSlot.Two, Vector2.zero, false, one, two);

            first.Tick(0.1f);
            second.Tick(0.1f);

            Assert.That(first.Move.x, Is.GreaterThan(0.9f), "seat one closes from the left");
            Assert.That(second.Move.x, Is.LessThan(-0.9f), "seat two closes from the right");
        }

        [Test]
        public void A_cart_knocked_out_by_friendly_fire_stops_dead()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(6f, 0f),
                chefVulnerable: true,
                Cart(0f, disabled: true),
                Cart(-9f));

            brain.Tick(0.1f);

            Assert.That(brain.Move.magnitude, Is.LessThan(0.001f), "it should not coast");
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }

        [Test]
        public void It_fires_once_it_is_lined_up_with_a_clear_lane_and_the_partner_well_clear()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(0f, 3f),
                chefVulnerable: true,
                Cart(0f),
                Cart(-8f));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True);
            Assert.That(brain.IsHeld(MinigameAction.Primary), Is.True);
        }

        [Test]
        public void It_holds_fire_when_the_partner_is_parked_straight_across_the_gap()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(0f, 3f),
                chefVulnerable: true,
                Cart(0f),
                Cart(0.5f));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                "a miss from here carries on into the teammate");
        }

        [Test]
        public void It_holds_fire_when_a_stalagmite_stands_in_the_lane()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(0f, 3f),
                chefVulnerable: true,
                Cart(0f, laneClear: false),
                Cart(-8f));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False,
                "that shot only breaks rock");
        }

        [Test]
        public void It_holds_fire_while_Chef_Tako_is_still_flashing_from_the_last_hit()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(0f, 3f),
                chefVulnerable: false,
                Cart(0f),
                Cart(-8f));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }

        [Test]
        public void It_keeps_closing_the_gap_while_the_cannon_reloads()
        {
            CrossfireBrain brain = Gunner(
                ParticipantSlot.One,
                new Vector2(6f, 0f),
                chefVulnerable: true,
                Cart(0f, canFire: false),
                Cart(-8f));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False, "nothing to fire yet");
            Assert.That(brain.Move.x, Is.GreaterThan(0.9f), "but it should still be lining up");
        }
    }
}
