using NUnit.Framework;
using SushiParty.Core;
using SushiParty.InputLayer;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class CpuBrainSnapshotTests
    {
        private readonly struct FakeSnapshot
        {
            public readonly Vector2 Self;
            public readonly Vector2 Chef;
            public readonly bool ChefVulnerable;

            public FakeSnapshot(Vector2 self, Vector2 chef, bool chefVulnerable)
            {
                Self = self;
                Chef = chef;
                ChefVulnerable = chefVulnerable;
            }
        }

        private sealed class FakeBrain : CpuBrain<FakeSnapshot>
        {
            public FakeBrain(System.Func<FakeSnapshot> world, CpuSkill skill)
                : base(world, skill)
            {
            }

            protected override void Think(in FakeSnapshot world, float deltaTime)
            {
                Steer(world.Chef - world.Self);

                if (world.ChefVulnerable)
                {
                    Press(MinigameAction.Primary);
                }
            }
        }

        private static FakeBrain BrainOver(FakeSnapshot snapshot)
        {
            return new FakeBrain(() => snapshot, CpuSkill.Standard);
        }

        [Test]
        public void Brain_steers_toward_chef()
        {
            FakeBrain brain = BrainOver(new FakeSnapshot(Vector2.zero, new Vector2(0f, 4f), false));

            brain.Tick(0.1f);

            Assert.That(brain.Move.y, Is.GreaterThan(0.9f), "should drive forward at him");
            Assert.That(Mathf.Abs(brain.Move.x), Is.LessThan(0.01f));
        }

        [Test]
        public void Steering_is_clamped_to_unit_length()
        {
            FakeBrain brain = BrainOver(new FakeSnapshot(Vector2.zero, new Vector2(0f, 400f), false));

            brain.Tick(0.1f);

            Assert.That(brain.Move.magnitude, Is.LessThanOrEqualTo(1.0001f));
        }

        [Test]
        public void Brain_holds_fire_while_chef_is_invulnerable()
        {
            FakeBrain brain = BrainOver(new FakeSnapshot(Vector2.zero, Vector2.one, chefVulnerable: false));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.False);
        }

        [Test]
        public void Brain_fires_when_chef_is_vulnerable()
        {
            FakeBrain brain = BrainOver(new FakeSnapshot(Vector2.zero, Vector2.one, chefVulnerable: true));

            brain.Tick(0.1f);

            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True);
        }

        [Test]
        public void Press_edge_lasts_exactly_one_frame_but_hold_outlives_it()
        {
            FakeBrain brain = BrainOver(new FakeSnapshot(Vector2.zero, Vector2.one, chefVulnerable: true));

            brain.Tick(0.016f);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "pressed on the first frame");
            Assert.That(brain.IsHeld(MinigameAction.Primary), Is.True, "and held");

            brain.Tick(0.5f);
            Assert.That(brain.WasPressed(MinigameAction.Primary), Is.True, "re-pressed next frame");
        }

        [Test]
        public void Secondary_is_untouched_by_a_primary_press()
        {
            FakeBrain brain = BrainOver(new FakeSnapshot(Vector2.zero, Vector2.one, chefVulnerable: true));

            brain.Tick(0.016f);

            Assert.That(brain.WasPressed(MinigameAction.Secondary), Is.False);
            Assert.That(brain.IsHeld(MinigameAction.Secondary), Is.False);
        }

        [Test]
        public void A_brain_reads_the_world_fresh_every_tick()
        {
            FakeSnapshot live = new FakeSnapshot(Vector2.zero, new Vector2(0f, 4f), false);
            FakeBrain brain = new FakeBrain(() => live, CpuSkill.Standard);

            brain.Tick(0.1f);
            Assert.That(brain.Move.y, Is.GreaterThan(0.9f));

            live = new FakeSnapshot(Vector2.zero, new Vector2(0f, -4f), false);
            brain.Tick(0.1f);
            Assert.That(brain.Move.y, Is.LessThan(-0.9f), "should follow him after he moves");
        }

        [Test]
        public void A_brain_without_a_world_is_rejected_at_construction()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new FakeBrain(null, CpuSkill.Standard));
        }

        [Test]
        public void A_snapshot_brain_is_still_just_a_participant_input()
        {
            IParticipantInput input = BrainOver(new FakeSnapshot(Vector2.zero, Vector2.one, true));

            input.Tick(0.1f);

            Assert.That(input.WasPressed(MinigameAction.Primary), Is.True,
                "gameplay must not be able to tell this seat from a human one");
        }
    }
}
