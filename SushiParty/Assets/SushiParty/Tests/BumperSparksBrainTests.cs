using NUnit.Framework;
using SushiParty.Core;
using SushiParty.Minigames.BumperSparks;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class BumperSparksBrainTests
    {
        private const float ArenaRadius = 9f;
        private const float VehicleRadius = 0.85f;
        private const float Step = 0.1f;
        private const float Standoff = VehicleRadius + VehicleRadius + 0.9f;

        private static BumperVehicle Vehicle(float x, float y, bool stunned = false)
        {
            return new BumperVehicle(new Vector2(x, y), VehicleRadius, stunned);
        }

        private static BumperSparksSnapshot World(
            BumperVehicle chef,
            BumperVehicle playerOne,
            BumperVehicle playerTwo)
        {
            return new BumperSparksSnapshot(ArenaRadius, chef, playerOne, playerTwo);
        }

        private static BumperPartnerBrain Partner(
            BumperSparksSnapshot world,
            ParticipantSlot slot = ParticipantSlot.One,
            CpuSkill skill = CpuSkill.Sharp)
        {
            return new BumperPartnerBrain(() => world, slot, skill);
        }

        private static ChefSparksBrain Driver(BumperSparksSnapshot world, CpuSkill skill = CpuSkill.Sharp)
        {
            return new ChefSparksBrain(() => world, skill);
        }

        [Test]
        public void A_partner_stages_on_the_centre_side_of_chef_tako_rather_than_charging_him()
        {
            BumperSparksSnapshot world = World(Vehicle(4f, 0f), Vehicle(0f, 4f), Vehicle(0f, -4f));
            BumperPartnerBrain brain = Partner(world);

            brain.Tick(Step);

            Assert.That(brain.Move.y, Is.LessThan(-0.9f), "it should dive past him toward the middle");
            Assert.That(brain.Move.x, Is.LessThan(0.5f), "a beeline at him would be 0.71 across");
        }

        [Test]
        public void A_lined_up_partner_drives_outward_straight_through_chef_tako()
        {
            BumperSparksSnapshot world = World(
                Vehicle(8f, 0f),
                Vehicle(8f - Standoff, 0f),
                default);

            BumperPartnerBrain brain = Partner(world);
            brain.Tick(Step);

            Assert.That(brain.Move.x, Is.GreaterThan(0.99f), "straight through him, away from centre");
            Assert.That(Mathf.Abs(brain.Move.y), Is.LessThan(0.02f));
        }

        [Test]
        public void A_stunned_partner_lets_go_of_the_wheel()
        {
            BumperSparksSnapshot world = World(
                Vehicle(4f, 0f),
                Vehicle(0f, 4f, stunned: true),
                default);

            BumperPartnerBrain brain = Partner(world);
            brain.Tick(Step);

            Assert.That(brain.Move, Is.EqualTo(Vector2.zero), "the ring has its controls");
        }

        [Test]
        public void A_brain_with_no_vehicle_on_the_platform_stands_still()
        {
            BumperSparksSnapshot empty = World(default, default, default);

            BumperPartnerBrain partner = Partner(empty);
            ChefSparksBrain chef = Driver(empty);

            partner.Tick(Step);
            chef.Tick(Step);

            Assert.That(partner.Move, Is.EqualTo(Vector2.zero));
            Assert.That(chef.Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Each_seat_reads_its_own_vehicle_out_of_the_one_snapshot()
        {
            BumperSparksSnapshot world = World(Vehicle(4f, 0f), Vehicle(0f, 4f), Vehicle(0f, -4f));

            BumperPartnerBrain one = Partner(world, ParticipantSlot.One);
            BumperPartnerBrain two = Partner(world, ParticipantSlot.Two);

            one.Tick(Step);
            two.Tick(Step);

            Assert.That(one.Move.y, Is.LessThan(-0.9f), "seat one comes down from the north");
            Assert.That(two.Move.y, Is.GreaterThan(0.9f), "seat two comes up from the south");
        }

        [Test]
        public void A_relaxed_partner_hugs_the_ring_more_carelessly_than_a_sharp_one()
        {
            BumperSparksSnapshot world = World(Vehicle(0f, 8f), Vehicle(7.9f, 0f), default);

            BumperPartnerBrain sharp = Partner(world, ParticipantSlot.One, CpuSkill.Sharp);
            BumperPartnerBrain relaxed = Partner(world, ParticipantSlot.One, CpuSkill.Relaxed);

            sharp.Tick(Step);
            relaxed.Tick(Step);

            Assert.That(sharp.Move.x, Is.LessThan(-0.85f), "both still head inward");
            Assert.That(relaxed.Move.x, Is.LessThan(-0.85f));
            Assert.That(relaxed.Move.y, Is.GreaterThan(sharp.Move.y),
                "the relaxed one skates further along the ring before turning in");
        }

        [Test]
        public void Chef_tako_pinned_against_the_ring_drives_straight_back_to_the_middle()
        {
            BumperSparksSnapshot world = World(Vehicle(7.9f, 0f), Vehicle(5f, 0f), default);
            ChefSparksBrain brain = Driver(world);

            brain.Tick(Step);

            Assert.That(brain.Move.x, Is.EqualTo(-1f).Within(1e-3f), "dead inward, not out at the sparks");
            Assert.That(brain.Move.y, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Chef_tako_circles_the_platform_instead_of_running_dead_away()
        {
            BumperSparksSnapshot world = World(Vehicle(3f, 0f), Vehicle(0.5f, 0f), default);
            ChefSparksBrain brain = Driver(world);

            brain.Tick(Step);

            Assert.That(brain.Move.x, Is.GreaterThan(0f), "still edging away from the chaser");
            Assert.That(Mathf.Abs(brain.Move.y), Is.GreaterThan(Mathf.Abs(brain.Move.x)),
                "but mostly sideways, around the platform");
        }
    }
}
