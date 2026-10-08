using SushiParty.Audio;
using SushiParty.Core;
using SushiParty.InputLayer;
using UnityEngine;

namespace SushiParty.Minigames.BumperSparks
{
    public sealed class BumperPartnerBrain : CpuBrain<BumperSparksSnapshot>
    {
        private readonly ParticipantSlot slot;
        private Vector2 stagingPoint;
        private float repathTimer;

        public BumperPartnerBrain(System.Func<BumperSparksSnapshot> world, ParticipantSlot slot, CpuSkill skill)
            : base(world, skill)
        {
            this.slot = slot;
        }

        protected override void Think(in BumperSparksSnapshot world, float deltaTime)
        {
            BumperVehicle self = world.PlayerAt(slot);
            BumperVehicle chef = world.Chef;

            if (!self.Present || !chef.Present || self.Stunned)
            {
                Steer(Vector2.zero);
                return;
            }

            if (Elapsed(ref repathTimer, deltaTime, ReactionDelay))
            {
                stagingPoint = ComputeStagingPoint(self, chef, world.ArenaRadius);
            }

            Vector2 toStaging = stagingPoint - self.Position;
            bool linedUp = toStaging.magnitude < 1.1f;

            Vector2 desired = linedUp
                ? (chef.Position - self.Position).normalized
                : toStaging.normalized;

            Steer(ApplyRingSafety(self, desired, world.ArenaRadius));
        }

        private Vector2 ComputeStagingPoint(BumperVehicle self, BumperVehicle chef, float arenaRadius)
        {
            Vector2 outward = chef.Position.sqrMagnitude > 0.04f
                ? chef.Position.normalized
                : (chef.Position - self.Position).normalized;

            float standoff = self.Radius + chef.Radius + 0.9f;
            Vector2 target = chef.Position - outward * standoff;

            float jitter = AimJitter * 2.2f;
            target += new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));

            float safeRadius = arenaRadius - self.Radius - 0.6f;
            if (target.magnitude > safeRadius)
            {
                target = target.normalized * safeRadius;
            }

            return target;
        }

        private Vector2 ApplyRingSafety(BumperVehicle self, Vector2 desired, float arenaRadius)
        {
            float distance = self.Position.magnitude;
            float danger = Mathf.InverseLerp(arenaRadius * 0.72f, arenaRadius - self.Radius, distance);
            if (danger <= 0f)
            {
                return desired;
            }

            danger *= Mathf.Lerp(0.55f, 1f, Competence);

            Vector2 inward = -self.Position.normalized;
            return Vector2.ClampMagnitude(Vector2.Lerp(desired, inward, danger), 1f);
        }
    }

    public sealed class ChefSparksBrain : CpuBrain<BumperSparksSnapshot>
    {
        private const int NoTarget = -1;

        private enum Mood
        {
            Evade,
            Charge,
        }

        private Mood mood = Mood.Evade;
        private float moodTimer;
        private float circleSign = 1f;
        private int chargeTarget = NoTarget;

        public ChefSparksBrain(System.Func<BumperSparksSnapshot> world, CpuSkill skill)
            : base(world, skill)
        {
        }

        protected override void Think(in BumperSparksSnapshot world, float deltaTime)
        {
            BumperVehicle self = world.Chef;
            if (!self.Present || self.Stunned)
            {
                Steer(Vector2.zero);
                return;
            }

            if (Elapsed(ref moodTimer, deltaTime, Random.Range(1.1f, 2.0f)))
            {
                ChooseMood(in world, self);
            }

            BumperVehicle target = world.Player(chargeTarget);
            Vector2 desired = mood == Mood.Charge && target.Present
                ? (target.Position - self.Position).normalized
                : ComputeEvade(in world, self);

            Steer(ApplyRingAvoidance(self, desired, world.ArenaRadius));
        }

        private void ChooseMood(in BumperSparksSnapshot world, BumperVehicle self)
        {
            chargeTarget = NoTarget;

            float aggression = Mathf.Lerp(0.08f, 0.42f, Competence);

            int best = NoTarget;
            float bestExposure = 0f;

            for (int i = 0; i < world.PlayerCount; i++)
            {
                BumperVehicle player = world.Player(i);
                if (!player.Present || player.Stunned)
                {
                    continue;
                }

                float exposure = player.Position.magnitude / world.ArenaRadius;
                bool isInsideThem = self.Position.magnitude < player.Position.magnitude;

                if (exposure > 0.58f && isInsideThem && exposure > bestExposure)
                {
                    bestExposure = exposure;
                    best = i;
                }
            }

            if (best != NoTarget && Random.value < aggression)
            {
                mood = Mood.Charge;
                chargeTarget = best;
                return;
            }

            mood = Mood.Evade;
            circleSign = Random.value < 0.5f ? -1f : 1f;
        }

        private Vector2 ComputeEvade(in BumperSparksSnapshot world, BumperVehicle self)
        {
            bool found = false;
            Vector2 nearest = Vector2.zero;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < world.PlayerCount; i++)
            {
                BumperVehicle player = world.Player(i);
                if (!player.Present)
                {
                    continue;
                }

                float distance = Vector2.Distance(player.Position, self.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = player.Position;
                    found = true;
                }
            }

            if (!found)
            {
                return Vector2.zero;
            }

            Vector2 away = (self.Position - nearest).normalized;

            Vector2 radial = self.Position.sqrMagnitude > 0.04f ? self.Position.normalized : Vector2.up;
            Vector2 tangent = new Vector2(-radial.y, radial.x) * circleSign;

            float panic = Mathf.InverseLerp(6f, 1.6f, nearestDistance);
            Vector2 blended = Vector2.Lerp(away, tangent, 0.45f + 0.25f * panic);

            float jitter = AimJitter;
            blended += new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));
            return blended.normalized;
        }

        private Vector2 ApplyRingAvoidance(BumperVehicle self, Vector2 desired, float arenaRadius)
        {
            float distance = self.Position.magnitude;
            float danger = Mathf.InverseLerp(arenaRadius * 0.62f, arenaRadius - self.Radius, distance);
            if (danger <= 0f)
            {
                return desired;
            }

            danger *= Mathf.Lerp(0.5f, 1.15f, Competence);

            Vector2 inward = -self.Position.normalized;
            return Vector2.ClampMagnitude(Vector2.Lerp(desired, inward, Mathf.Clamp01(danger)), 1f);
        }
    }
}
