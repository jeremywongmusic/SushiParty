using SushiParty.Audio;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.PedalToThePaddle
{
    public sealed class PedalToThePaddleMinigame : MinigameController
    {
        private const int PaddleCount = 12;
        private const float WheelRadius = 3.6f;
        private const float WheelCentreY = 4.2f;
        private const float WalkSpeed = 95f;
        private const float StompPower = 62f;
        private const float Drag = 0.85f;
        private const float MaxAngularSpeed = 130f;
        private const float FallAngle = 86f;
        private const float AirTime = 0.42f;
        private const float BubbleDuration = 2.4f;
        private const float RespawnAngle = -45f;
        private const float GoalDistance = 100f;
        private const float ProgressPerDegree = 0.04f;
        private const int ProgressPips = 10;

        private sealed class Rider
        {
            public Transform View;
            public CharacterAnimation Animation;
            public Participant Owner;
            public float Angle = RespawnAngle;
            public float AirTimer;
            public float BubbleTimer;
            public bool Airborne => AirTimer > 0f;
            public bool Bubbled => BubbleTimer > 0f;
        }

        private readonly Rider[] riders = new Rider[2];
        private readonly Transform[] paddles = new Transform[PaddleCount];
        private Transform arena;
        private Transform wheel;
        private Transform chefRaft;
        private float wheelAngle;
        private float angularSpeed;
        private float progress;
        private float chefProgress;
        private AudioHandle wheelLoop;
        private int lastPipsShown = -1;
        public override MinigameId Id => MinigameId.PedalToThePaddle;

        public PedalToThePaddleSnapshot Snapshot()
        {
            return new PedalToThePaddleSnapshot(Sample(riders[0]), Sample(riders[1]));
        }

        private static PedalRiderState Sample(Rider rider)
        {
            return rider == null
                ? new PedalRiderState(0f, false)
                : new PedalRiderState(rider.Angle, !rider.Airborne && !rider.Bubbled);
        }

        protected override void OnPrepare()
        {
            arena = new GameObject("Arena").transform;
            arena.SetParent(transform, false);

            BuildScenery();
            BuildWheel();

            riders[0] = CreateRider(P1, -55f);
            riders[1] = CreateRider(P2, -25f);

            Hud.ConfigurePips(ProgressPips, "distance to the goal");
            Hud.SetPips(0);
            Hud.SetStatus("Stomp on the front of the wheel to drive it — and walk back before it drops you in");
            SyncViews();
        }

        private void BuildScenery()
        {
            Shapes.Cube(
                arena,
                new Vector3(0f, -1.2f, 0f),
                new Vector3(80f, 2.4f, 20f),
                new Color(0.15f, 0.32f, 0.45f),
                "Water");

            Shapes.Cube(
                arena,
                new Vector3(0f, 1.2f, 0f),
                new Vector3(9f, 1.2f, 5f),
                new Color(0.42f, 0.32f, 0.24f),
                "Hull");

            chefRaft = new GameObject("ChefRaft").transform;
            chefRaft.SetParent(arena, false);
            Shapes.Cube(chefRaft, new Vector3(0f, 1.2f, 0f), new Vector3(7f, 1.2f, 4f),
                new Color(0.34f, 0.26f, 0.30f), "Raft");
            Transform jr = Shapes.Octopus(
                chefRaft, Palette.TakoPurple, 0.55f, "ChefTako", isChef: true, animated: true);
            jr.localPosition = new Vector3(0f, 1.8f, 0f);
        }

        private void BuildWheel()
        {
            wheel = new GameObject("PaddleWheel").transform;
            wheel.SetParent(arena, false);
            wheel.localPosition = new Vector3(0f, WheelCentreY, 0f);

            Shapes.Cylinder(
                wheel,
                Vector3.zero,
                new Vector3(1.1f, 1.4f, 1.1f),
                new Color(0.48f, 0.45f, 0.52f),
                "Hub").localRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int i = 0; i < PaddleCount; i++)
            {
                paddles[i] = Shapes.Cube(
                    wheel,
                    Vector3.zero,
                    new Vector3(2.2f, 0.3f, 3.4f),
                    i % 2 == 0 ? new Color(0.80f, 0.62f, 0.38f) : new Color(0.68f, 0.52f, 0.32f),
                    $"Paddle{i}");
            }
        }

        private Rider CreateRider(Participant participant, float angle)
        {
            Transform view = Shapes.Octopus(arena, participant.Color, 0.5f, $"Rider_{participant.Slot}");
            return new Rider
            {
                View = view,
                Animation = CharacterAnimation.Attach(view),
                Owner = participant,
                Angle = angle,
            };
        }

        private static Vector3 PointOnWheel(float angleDegrees, float radius)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, WheelCentreY + Mathf.Cos(radians) * radius, 0f);
        }

        protected override void OnPlay(float deltaTime)
        {
            TickRider(riders[0], deltaTime);
            TickRider(riders[1], deltaTime);
            TickWheel(deltaTime);
            TickChef(deltaTime);
            SyncViews();
            RefreshHud();

            if (progress >= GoalDistance)
            {
                GameAudio.Play(Sfx.PedalGoal);
                Win($"Beat him to the goal by {GoalDistance - chefProgress:0} lengths");
                return;
            }

            if (chefProgress >= GoalDistance)
            {
                Lose("His platform reached the goal first");
            }
        }

        protected override void OnSettle(float deltaTime, MinigameOutcome result)
        {
            TickWheel(deltaTime);
            SyncViews();
        }

        private void TickRider(Rider rider, float deltaTime)
        {
            if (rider.Bubbled)
            {
                rider.BubbleTimer -= deltaTime;
                if (rider.BubbleTimer <= 0f)
                {
                    rider.Angle = RespawnAngle;

                    rider.Animation.SetStunned(false);
                    GameAudio.Play(Sfx.PedalBubblePop);
                }

                return;
            }

            IParticipantInput input = rider.Owner.Input;

            if (rider.Airborne)
            {
                float previousAir = rider.AirTimer;
                rider.AirTimer -= deltaTime;
                if (previousAir > AirTime * 0.5f && rider.AirTimer <= AirTime * 0.5f)
                {
                    rider.Animation.Pound();
                }

                if (rider.AirTimer <= 0f)
                {
                    rider.Animation.SetGrounded(true);
                    rider.Animation.Land();
                    Stomp(rider);
                }

                return;
            }

            rider.Animation.SetSpeed(Mathf.Abs(input.Move.x));

            rider.Angle += (angularSpeed + input.Move.x * WalkSpeed) * deltaTime;

            if (input.WasPressed(MinigameAction.Primary))
            {
                rider.AirTimer = AirTime;

                rider.Animation.SetGrounded(false);
                rider.Animation.Jump();
                return;
            }

            if (Mathf.Abs(rider.Angle) > FallAngle)
            {
                rider.BubbleTimer = BubbleDuration;

                rider.Animation.SetSpeed(0f);
                rider.Animation.SetStunned(true);

                Hud.SetStatus($"{rider.Owner.DisplayName} went in the water — back in {BubbleDuration:0}s");
                GameAudio.PlayAt(Sfx.PedalSplash, rider.View.position);
            }
        }

        private void Stomp(Rider rider)
        {
            float leverage = Mathf.Sin(rider.Angle * Mathf.Deg2Rad);
            if (leverage <= 0.05f)
            {
                Hud.SetStatus("That stomp landed behind the top — no push");
                GameAudio.Play(Sfx.PedalStompWasted);
                return;
            }

            angularSpeed = Mathf.Min(MaxAngularSpeed, angularSpeed + StompPower * leverage);

            GameAudio.Play(Sfx.PedalStomp, Sfx.ForceParameter, Mathf.Clamp01(leverage));
        }

        private const float StallSpeed = 2f;
        private bool wheelStalled;

        private void TickWheel(float deltaTime)
        {
            angularSpeed = Mathf.Max(0f, angularSpeed - Drag * angularSpeed * deltaTime);
            wheelAngle += angularSpeed * deltaTime;
            progress += angularSpeed * ProgressPerDegree * deltaTime;

            GameAudio.SetParameter(
                wheelLoop,
                Sfx.SpeedParameter,
                Mathf.Clamp01(angularSpeed / MaxAngularSpeed));

            bool stalled = angularSpeed <= StallSpeed;

            if (stalled && !wheelStalled)
            {
                GameAudio.Play(Sfx.PedalWheelStall);
            }

            wheelStalled = stalled;
        }

        protected override void OnBegin()
        {
            wheelLoop = GameAudio.Loop(Sfx.PedalWheelLoop);
        }

        protected override void OnConclude(MinigameOutcome result)
        {
            GameAudio.Stop(ref wheelLoop);

            foreach (Rider rider in riders)
            {
                rider.Animation.SetSpeed(0f);

                if (result.Won)
                {
                    rider.Animation.Cheer();
                }
            }
        }

        private void TickChef(float deltaTime)
        {
            chefProgress += RivalSpeed() * deltaTime;
        }

        private float RivalSpeed()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => 1.55f,
                CpuSkill.Sharp => 2.35f,
                _ => 1.95f,
            };
        }

        private void SyncViews()
        {
            if (chefRaft != null)
            {
                float lead = Mathf.Clamp((chefProgress - progress) * 0.6f, -22f, 22f);
                chefRaft.localPosition = new Vector3(lead, 0f, 9f);
            }

            for (int i = 0; i < PaddleCount; i++)
            {
                float angle = wheelAngle + i * (360f / PaddleCount);
                paddles[i].position = arena.position + PointOnWheel(angle, WheelRadius);
                paddles[i].localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            foreach (Rider rider in riders)
            {
                if (rider.Bubbled)
                {
                    rider.View.position = new Vector3(-5.5f, 0.6f + Mathf.Sin(Time.time * 3f) * 0.2f, 0f);
                    rider.View.localScale = Vector3.one * 0.8f;
                    continue;
                }

                float lift = rider.Airborne
                    ? Mathf.Sin((1f - rider.AirTimer / AirTime) * Mathf.PI) * 1.6f
                    : 0f;

                rider.View.position = arena.position + PointOnWheel(rider.Angle, WheelRadius + 0.55f + lift);
                rider.View.localScale = Vector3.one;
            }
        }

        private void RefreshHud()
        {
            int pips = Mathf.Clamp(Mathf.FloorToInt(progress / GoalDistance * ProgressPips), 0, ProgressPips);
            if (pips != lastPipsShown)
            {
                lastPipsShown = pips;
                Hud.SetPips(pips);
            }

            float lead = progress - chefProgress;
            Hud.SetStatus(lead >= 0f
                ? $"Ahead by {lead:0} — keep stomping the front of the wheel"
                : $"Behind by {-lead:0} — you both need to be on it");
        }

        protected override MinigameOutcome OnTimeUp()
        {
            return progress > chefProgress
                ? MinigameOutcome.Win(0f, "Furthest along when the clock ran out")
                : MinigameOutcome.Lose($"{progress:0} of {GoalDistance:0} lengths covered");
        }

        protected override MinigameTelemetry SampleTelemetry()
        {
            return new MinigameTelemetry
            {
                PlayerMotion = angularSpeed / MaxAngularSpeed,
                ChefMotion = RivalSpeed() / 2.4f,
                Objective = progress / GoalDistance,
            };
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return new PedalToThePaddleBrain(Snapshot, participant.Slot, participant.Skill);
        }
    }

    public sealed class PedalToThePaddleBrain : CpuBrain<PedalToThePaddleSnapshot>
    {
        private const float SweetSpot = 42f;
        private const float RetreatAngle = 66f;
        private readonly ParticipantSlot slot;
        private float stompTimer;

        public PedalToThePaddleBrain(
            System.Func<PedalToThePaddleSnapshot> world,
            ParticipantSlot slot,
            CpuSkill skill)
            : base(world, skill)
        {
            this.slot = slot;
        }

        private float StompInterval => Mathf.Lerp(0.85f, 0.45f, Competence);

        protected override void Think(in PedalToThePaddleSnapshot world, float deltaTime)
        {
            PedalRiderState self = world.Rider(slot);

            if (!self.Ready)
            {
                Steer(Vector2.zero);
                return;
            }

            float angle = self.Angle;

            if (angle > RetreatAngle)
            {
                Steer(new Vector2(-1f, 0f));
                return;
            }

            float target = SweetSpot * Mathf.Lerp(0.75f, 1f, Competence);
            float delta = target - angle;
            Steer(new Vector2(Mathf.Abs(delta) < 4f ? 0f : Mathf.Sign(delta) * 0.6f, 0f));

            if (angle > 12f && Elapsed(ref stompTimer, deltaTime, StompInterval))
            {
                Press(MinigameAction.Primary);
            }
        }
    }
}
