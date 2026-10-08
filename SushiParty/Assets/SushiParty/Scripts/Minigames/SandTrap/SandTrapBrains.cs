using SushiParty.Core;
using SushiParty.InputLayer;
using UnityEngine;

namespace SushiParty.Minigames.SandTrap
{
    public sealed class SandTrapPartnerBrain : CpuBrain<SandTrapSnapshot>
    {
        private readonly ParticipantSlot slot;
        private readonly bool ownsRows;
        private readonly SnapshotPoundActuator actuator;
        private int targetIndex = 2;
        private float retargetTimer;
        private float cadenceTimer;

        public SandTrapPartnerBrain(System.Func<SandTrapSnapshot> world, ParticipantSlot slot, CpuSkill skill)
            : base(world, skill)
        {
            this.slot = slot;
            ownsRows = slot == ParticipantSlot.One;
            actuator = new SnapshotPoundActuator(Mathf.Lerp(0.30f, 0.14f, Competence));
        }

        private float CadenceInterval => Mathf.Lerp(1.9f, 0.9f, Competence);

        protected override void Think(in SandTrapSnapshot world, float deltaTime)
        {
            SandTrapSeat self = world.SeatFor(slot);
            if (!self.Exists)
            {
                Steer(Vector2.zero);
                return;
            }

            UpdateTarget(in world, deltaTime);
            float delta = SandGrid.CoordinateFor(targetIndex) - self.TrackPosition;
            SteerAlongTrack(delta);

            if (actuator.Tick(self.Airborne, deltaTime))
            {
                Press(MinigameAction.Primary);
                return;
            }

            bool aligned = Mathf.Abs(delta) < 0.3f;
            if (actuator.Busy || !aligned || !self.Grounded)
            {
                return;
            }

            if (ShouldPound(in world, deltaTime))
            {
                Press(MinigameAction.Primary);
                actuator.Begin();
            }
        }

        private void UpdateTarget(in SandTrapSnapshot world, float deltaTime)
        {
            if (!Elapsed(ref retargetTimer, deltaTime, ReactionDelay))
            {
                return;
            }

            Vector2Int tile = world.ChefTarget;
            targetIndex = ownsRows ? tile.x : tile.y;
        }

        private void SteerAlongTrack(float delta)
        {
            float direction = Mathf.Abs(delta) < 0.06f ? 0f : Mathf.Sign(delta);

            Steer(ownsRows ? new Vector2(0f, direction) : new Vector2(direction, 0f));
        }

        private bool ShouldPound(in SandTrapSnapshot world, float deltaTime)
        {
            SandTrapSeat partner = world.PartnerOf(slot);

            if (partner.HighlightRemaining > 0.45f)
            {
                return true;
            }

            if (partner.Exists && partner.Pounding)
            {
                return true;
            }

            return Elapsed(ref cadenceTimer, deltaTime, CadenceInterval);
        }

        private sealed class SnapshotPoundActuator
        {
            private const float AbandonAfter = 0.9f;
            private readonly float apexDelay;
            private float airTimer;

            public SnapshotPoundActuator(float apexDelay)
            {
                this.apexDelay = apexDelay;
            }

            public bool Busy { get; private set; }

            public void Begin()
            {
                Busy = true;
                airTimer = 0f;
            }

            public bool Tick(bool airborne, float deltaTime)
            {
                if (!Busy)
                {
                    return false;
                }

                airTimer += deltaTime;

                if (airborne && airTimer >= apexDelay)
                {
                    Busy = false;
                    return true;
                }

                if (airTimer > AbandonAfter)
                {
                    Busy = false;
                }

                return false;
            }
        }
    }
}
