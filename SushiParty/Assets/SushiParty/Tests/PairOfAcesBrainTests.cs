using NUnit.Framework;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Minigames.PairOfAces;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class PairOfAcesBrainTests
    {
        private const float FlightTime = 0.32f;
        private const float NoBomb = -1f;
        private static readonly Vector2 ChefAloft = new Vector2(0f, 7f);

        private static GunnerView Cannon(Vector2 crosshair, bool canFire = true, float incoming = NoBomb)
        {
            return new GunnerView(crosshair, canFire, incoming);
        }

        private static PairOfAcesSnapshot World(
            Vector2 chef,
            Vector2 chefVelocity,
            GunnerView one,
            GunnerView two,
            bool chefVulnerable = true)
        {
            return new PairOfAcesSnapshot(chef, chefVelocity, chefVulnerable, FlightTime, one, two);
        }

        private static PairOfAcesBrain BrainOver(
            PairOfAcesSnapshot world,
            ParticipantSlot slot = ParticipantSlot.One,
            CpuSkill skill = CpuSkill.Sharp)
        {
            return new PairOfAcesBrain(() => world, slot, skill);
        }

        [Test]
        public void Steers_ahead_of_the_flying_wok_rather_than_at_it()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                new Vector2(10f, 0f),
                Cannon(ChefAloft),
                Cannon(new Vector2(4.5f, 7f))));

            brain.Tick(0.1f);

            Assert.That(brain.Move.x, Is.GreaterThan(0.95f), "should chase the lead point, not the wok");
            Assert.That(Mathf.Abs(brain.Move.y), Is.LessThan(0.1f));
        }

        [Test]
        public void Fires_once_the_crosshair_is_sitting_on_the_lead_point()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft),
                Cannon(new Vector2(4.5f, 7f))));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True);
        }

        [Test]
        public void Holds_fire_while_chef_tako_is_still_flashing()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft),
                Cannon(new Vector2(4.5f, 7f)),
                chefVulnerable: false));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }

        [Test]
        public void Holds_fire_while_its_own_cannon_is_jammed()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft, canFire: false),
                Cannon(new Vector2(4.5f, 7f))));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
            Assert.That(brain.Move.magnitude, Is.LessThanOrEqualTo(1.0001f), "but it keeps tracking");
        }

        [Test]
        public void Dodges_a_wasabi_bomb_that_is_about_to_land()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft, incoming: 0.1f),
                Cannon(new Vector2(4.5f, 7f))));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Secondary), Is.True);
        }

        [Test]
        public void Leaves_a_bomb_alone_while_it_is_still_a_long_way_out()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft, incoming: 0.9f),
                Cannon(new Vector2(4.5f, 7f))));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Secondary), Is.False);
        }

        [Test]
        public void Only_the_seat_the_bomb_is_aimed_at_dodges_it()
        {
            PairOfAcesSnapshot world = World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft),
                Cannon(new Vector2(4.5f, 7f), incoming: 0.05f));

            PairOfAcesBrain seatOne = BrainOver(world, ParticipantSlot.One);
            PairOfAcesBrain seatTwo = BrainOver(world, ParticipantSlot.Two);

            seatOne.Tick(0.1f);
            seatTwo.Tick(0.1f);

            Assert.That(seatOne.WasPressed(MinigameAction.Secondary), Is.False,
                "a bomb aimed at the partner is not seat one's problem");
            Assert.That(seatTwo.WasPressed(MinigameAction.Secondary), Is.True);
        }

        [Test]
        public void Dodging_does_not_cost_it_the_shot_on_the_same_frame()
        {
            PairOfAcesBrain brain = BrainOver(World(
                ChefAloft,
                Vector2.zero,
                Cannon(ChefAloft, incoming: 0.1f),
                Cannon(new Vector2(4.5f, 7f))));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Secondary), Is.True);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True);
        }
    }
}
