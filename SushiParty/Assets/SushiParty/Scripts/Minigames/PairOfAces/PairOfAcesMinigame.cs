using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.PairOfAces
{
    public sealed class PairOfAcesMinigame : MinigameController
    {
        private const int HitsToWin = 3;
        private const float CrosshairSpeed = 13f;
        private const float RiceBallTravelTime = 0.32f;
        private const float HitRadius = 1.35f;
        private const float FireCooldown = 0.55f;
        private const float DodgeWindow = 0.5f;
        private const float CannonDisableDuration = 1.6f;
        private const float ChefInvulnerability = 1.2f;
        private const float WasabiBombTravelTime = 1.15f;
        private const float ChefTopSpeed = 14f;
        private static readonly Rect PlayField = new Rect(-9.5f, 2.5f, 19f, 8.5f);

        private sealed class Gunner
        {
            public Participant Owner;
            public Transform Crosshair;
            public Transform Cannon;
            public Vector2 Aim;
            public Vector2 Muzzle;
            public float FireTimer;
            public float DisabledTimer;
            public float DodgeTimer;
            public bool Disabled => DisabledTimer > 0f;
            public bool Dodging => DodgeTimer > 0f;
        }

        private sealed class RiceBall
        {
            public Transform View;
            public Vector2 From;
            public Vector2 To;
            public float Progress;
        }

        private sealed class WasabiBomb
        {
            public Transform View;
            public Vector2 From;
            public Vector2 To;
            public float Progress;
            public Gunner Target;
        }

        private readonly List<RiceBall> riceBalls = new List<RiceBall>();
        private readonly List<WasabiBomb> wasabiBombs = new List<WasabiBomb>();
        private readonly Vulnerability chefHits = new Vulnerability();
        private Gunner gunnerOne;
        private Gunner gunnerTwo;
        private Transform arena;
        private Transform chefView;
        private CharacterAnimation chefAnimation;
        private Vector2 chefPosition = new Vector2(0f, 8f);
        private Vector2 chefVelocity;
        private float patternTimer;
        private float patternPhase;
        private int pattern;
        private float attackTimer = 3f;
        private int hits;
        public override MinigameId Id => MinigameId.PairOfAces;

        public PairOfAcesSnapshot Snapshot()
        {
            return new PairOfAcesSnapshot(
                chefPosition,
                chefVelocity,
                chefHits.CanBeHit,
                RiceBallTravelTime,
                ViewOf(gunnerOne),
                ViewOf(gunnerTwo));
        }

        private GunnerView ViewOf(Gunner gunner)
        {
            if (gunner == null)
            {
                return new GunnerView(Vector2.zero, false, -1f);
            }

            return new GunnerView(
                gunner.Aim,
                !gunner.Disabled && gunner.FireTimer <= 0f,
                IncomingWasabiBombTimeFor(gunner));
        }

        private float IncomingWasabiBombTimeFor(Gunner gunner)
        {
            float soonest = -1f;
            foreach (WasabiBomb wasabiBomb in wasabiBombs)
            {
                if (wasabiBomb.Target != gunner)
                {
                    continue;
                }

                float remaining = (1f - wasabiBomb.Progress) * WasabiBombTravelTime;
                if (soonest < 0f || remaining < soonest)
                {
                    soonest = remaining;
                }
            }

            return soonest;
        }

        protected override void OnPrepare()
        {
            arena = new GameObject("Arena").transform;
            arena.SetParent(transform, false);

            BuildBackdrop();

            gunnerOne = BuildGunner(P1, -4.5f);
            gunnerTwo = BuildGunner(P2, 4.5f);

            chefView = Shapes.Octopus(arena, Palette.TakoPurple, 0.8f, "ChefTako", isChef: true);
            chefAnimation = CharacterAnimation.Attach(chefView);
            Shapes.Wok(chefView, new Vector3(0f, -0.2f, 0f), 2.4f);
            SyncChefView();

            Hud.ConfigurePips(HitsToWin, "hits on the flying wok");
            Hud.SetPips(0);
            Hud.SetStatus("Lead your shots — and hit dodge when a wasabi bomb is coming at you");
        }

        private void BuildBackdrop()
        {
            Shapes.Cube(
                arena,
                new Vector3(0f, 6f, 6f),
                new Vector3(30f, 18f, 0.4f),
                new Color(0.10f, 0.13f, 0.20f),
                "Sky");

            Shapes.Cube(
                arena,
                new Vector3(0f, -0.6f, 0f),
                new Vector3(30f, 1.2f, 6f),
                new Color(0.20f, 0.22f, 0.27f),
                "Deck");
        }

        private Gunner BuildGunner(Participant participant, float x)
        {
            Transform cannon = Shapes.Cylinder(
                arena,
                new Vector3(x, 0.6f, 0f),
                new Vector3(1.3f, 0.7f, 1.3f),
                participant.Color,
                $"Cannon_{participant.Slot}");

            Transform crosshair = new GameObject($"Crosshair_{participant.Slot}").transform;
            crosshair.SetParent(arena, false);
            Shapes.Cube(crosshair, Vector3.zero, new Vector3(1.5f, 0.14f, 0.14f), participant.Color, "H");
            Shapes.Cube(crosshair, Vector3.zero, new Vector3(0.14f, 1.5f, 0.14f), participant.Color, "V");

            Gunner gunner = new Gunner
            {
                Owner = participant,
                Cannon = cannon,
                Crosshair = crosshair,
                Muzzle = new Vector2(x, 1.4f),
                Aim = new Vector2(x * 0.5f, 7f),
            };

            crosshair.position = new Vector3(gunner.Aim.x, gunner.Aim.y, 0f);
            return gunner;
        }

        protected override void OnPlay(float deltaTime)
        {
            TickGunner(gunnerOne, deltaTime);
            TickGunner(gunnerTwo, deltaTime);
            TickChef(deltaTime);
            TickRiceBalls(deltaTime);
            TickWasabiBombs(deltaTime, live: true);

            if (hits >= HitsToWin)
            {
                Win("Three hits — down goes the flying wok");
            }
        }

        protected override void OnSettle(float deltaTime, MinigameOutcome result)
        {
            TickRiceBalls(deltaTime);
            TickWasabiBombs(deltaTime, live: false);

            if (result.Won)
            {
                chefPosition += new Vector2(2.5f, 5f) * deltaTime;
                chefView.Rotate(0f, 0f, 320f * deltaTime, Space.Self);
                SyncChefView();
            }
        }

        protected override void OnConclude(MinigameOutcome result)
        {
            chefAnimation.SetSpeed(0f);
        }

        private void TickGunner(Gunner gunner, float deltaTime)
        {
            gunner.FireTimer = Mathf.Max(0f, gunner.FireTimer - deltaTime);
            gunner.DisabledTimer = Mathf.Max(0f, gunner.DisabledTimer - deltaTime);
            gunner.DodgeTimer = Mathf.Max(0f, gunner.DodgeTimer - deltaTime);

            IParticipantInput input = gunner.Owner.Input;

            if (input.WasPressed(MinigameAction.Secondary))
            {
                gunner.DodgeTimer = DodgeWindow;

                GameAudio.PlayAt(Sfx.AcesDodge, gunner.Cannon.position);
            }

            if (!gunner.Disabled)
            {
                Vector2 move = input.Move;
                gunner.Aim += move * (CrosshairSpeed * deltaTime);
                gunner.Aim.x = Mathf.Clamp(gunner.Aim.x, PlayField.xMin, PlayField.xMax);
                gunner.Aim.y = Mathf.Clamp(gunner.Aim.y, PlayField.yMin, PlayField.yMax);

                if (input.WasPressed(MinigameAction.Primary))
                {
                    if (gunner.FireTimer <= 0f)
                    {
                        FireRiceBall(gunner);
                    }
                    else
                    {
                        GameAudio.PlayAt(Sfx.AcesFireBlocked, gunner.Cannon.position);
                    }
                }
            }

            gunner.Crosshair.position = new Vector3(gunner.Aim.x, gunner.Aim.y, 0f);
            gunner.Crosshair.gameObject.SetActive(!gunner.Disabled);

            Color barrel = gunner.Disabled
                ? Color.Lerp(gunner.Owner.Color, Color.gray, 0.7f)
                : gunner.Dodging
                    ? Color.Lerp(gunner.Owner.Color, Color.white, 0.6f)
                    : gunner.Owner.Color;
            Shapes.Tint(gunner.Cannon, barrel, gunner.Dodging);
        }

        private void FireRiceBall(Gunner gunner)
        {
            gunner.FireTimer = FireCooldown;
            GameAudio.PlayAt(Sfx.AcesFire, new Vector3(gunner.Muzzle.x, gunner.Muzzle.y, 0f));

            Transform view = Shapes.Sphere(arena, Vector3.zero, 0.55f, Palette.RiceWhite, "RiceBall");
            Shapes.Tint(view, Palette.RiceWhite, glow: true);

            riceBalls.Add(new RiceBall
            {
                View = view,
                From = gunner.Muzzle,
                To = gunner.Aim,
                Progress = 0f,
            });
        }

        private void TickRiceBalls(float deltaTime)
        {
            for (int i = riceBalls.Count - 1; i >= 0; i--)
            {
                RiceBall riceBall = riceBalls[i];
                riceBall.Progress += deltaTime / RiceBallTravelTime;

                Vector2 position = Vector2.Lerp(riceBall.From, riceBall.To, riceBall.Progress);
                riceBall.View.position = new Vector3(position.x, position.y, 0f);

                if (riceBall.Progress < 1f)
                {
                    continue;
                }

                if (Vector2.Distance(riceBall.To, chefPosition) < HitRadius
                    && chefHits.TryHit(stunSeconds: 0f, immuneSeconds: ChefInvulnerability))
                {
                    RegisterHit();
                    GameAudio.PlayAt(Sfx.AcesHit, new Vector3(chefPosition.x, chefPosition.y, 0f));
                }
                else
                {
                    GameAudio.PlayAt(Sfx.AcesMiss, new Vector3(riceBall.To.x, riceBall.To.y, 0f));
                }

                Destroy(riceBall.View.gameObject);
                riceBalls.RemoveAt(i);
            }
        }

        private void RegisterHit()
        {
            hits++;
            Hud.SetPips(hits);

            int left = HitsToWin - hits;
            if (left > 0)
            {
                Hud.SetStatus($"Hit! {left} more to bring him down");
            }
        }

        private void TickChef(float deltaTime)
        {
            chefHits.Tick(deltaTime);

            patternPhase += deltaTime * PatternSpeed();
            patternTimer -= deltaTime;
            if (patternTimer <= 0f)
            {
                patternTimer = Random.Range(3f, 5f);
                pattern = (pattern + 1 + Random.Range(0, 2)) % 3;
            }

            Vector2 previous = chefPosition;
            chefPosition = EvaluatePattern();
            chefVelocity = deltaTime > 0f ? (chefPosition - previous) / deltaTime : Vector2.zero;

            chefAnimation.SetSpeed(chefVelocity.magnitude / ChefTopSpeed);

            SyncChefView();

            attackTimer -= deltaTime;
            if (attackTimer <= 0f)
            {
                LaunchWasabiBomb();
                attackTimer = Random.Range(2.4f, 4f) * (Context.ChefSkill == CpuSkill.Sharp ? 0.75f : 1f);
            }
        }

        private float PatternSpeed()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => 1.1f,
                CpuSkill.Sharp => 2.1f,
                _ => 1.6f,
            };
        }

        private Vector2 EvaluatePattern()
        {
            float centreY = PlayField.yMin + PlayField.height * 0.55f;

            switch (pattern)
            {
                case 0: // Circle
                    return new Vector2(
                        Mathf.Cos(patternPhase) * 6.5f,
                        centreY + Mathf.Sin(patternPhase) * 2.6f);

                case 1: // Zigzag
                    return new Vector2(
                        Mathf.PingPong(patternPhase * 3.4f, 15f) - 7.5f,
                        centreY + Mathf.Sin(patternPhase * 2.6f) * 1.6f);

                default: // Swoop
                    return new Vector2(
                        Mathf.Sin(patternPhase * 0.8f) * 8f,
                        centreY + Mathf.Cos(patternPhase * 1.7f) * 3.2f);
            }
        }

        private void SyncChefView()
        {
            chefView.position = new Vector3(chefPosition.x, chefPosition.y, 0f);

            bool blink = chefHits.HiddenWhileImmune(Time.time);
            Transform body = chefView.Find("Body");
            if (body != null)
            {
                body.gameObject.SetActive(!blink);
            }
        }

        private void LaunchWasabiBomb()
        {
            Gunner target = Random.value < 0.5f ? gunnerOne : gunnerTwo;
            if (target.Disabled)
            {
                target = target == gunnerOne ? gunnerTwo : gunnerOne;
            }

            if (target.Disabled)
            {
                return;
            }

            Transform view = Shapes.Sphere(arena, Vector3.zero, 0.9f, Palette.WasabiGreen, "WasabiBomb");
            Shapes.Tint(view, Palette.WasabiGreen, glow: true);

            wasabiBombs.Add(new WasabiBomb
            {
                View = view,
                From = chefPosition,
                To = target.Muzzle,
                Progress = 0f,
                Target = target,
            });

            Hud.SetStatus($"Wasabi bomb incoming at {target.Owner.DisplayName} — dodge!");
            GameAudio.PlayAt(Sfx.AcesWasabiLaunch, new Vector3(chefPosition.x, chefPosition.y, 0f));
        }

        private void TickWasabiBombs(float deltaTime, bool live)
        {
            for (int i = wasabiBombs.Count - 1; i >= 0; i--)
            {
                WasabiBomb wasabiBomb = wasabiBombs[i];
                wasabiBomb.Progress += deltaTime / WasabiBombTravelTime;

                Vector2 position = Vector2.Lerp(wasabiBomb.From, wasabiBomb.To, wasabiBomb.Progress);
                wasabiBomb.View.position = new Vector3(position.x, position.y, 0f);

                float scale = Mathf.Lerp(0.6f, 1.4f, wasabiBomb.Progress);
                wasabiBomb.View.localScale = Vector3.one * (0.9f * scale);

                if (wasabiBomb.Progress < 1f)
                {
                    continue;
                }

                if (live && !wasabiBomb.Target.Dodging)
                {
                    wasabiBomb.Target.DisabledTimer = CannonDisableDuration;
                    Hud.SetStatus($"{wasabiBomb.Target.Owner.DisplayName}'s cannon is jammed");
                    GameAudio.PlayAt(Sfx.AcesWasabiImpact, new Vector3(position.x, position.y, 0f));
                    GameAudio.Play(Sfx.AcesCannonJammed);
                }
                else if (live)
                {
                    Hud.SetStatus($"{wasabiBomb.Target.Owner.DisplayName} dodged it");

                    GameAudio.PlayAt(Sfx.AcesDodgeClean, new Vector3(position.x, position.y, 0f));
                }

                Destroy(wasabiBomb.View.gameObject);
                wasabiBombs.RemoveAt(i);
            }
        }

        protected override MinigameOutcome OnTimeUp()
        {
            return MinigameOutcome.Lose($"{hits} of {HitsToWin} hits landed");
        }

        protected override MinigameTelemetry SampleTelemetry()
        {
            float crosshairs = Mathf.Max(
                Vector2.ClampMagnitude(P1.Input.Move, 1f).magnitude,
                Vector2.ClampMagnitude(P2.Input.Move, 1f).magnitude);

            return new MinigameTelemetry
            {
                PlayerMotion = crosshairs,
                ChefMotion = chefVelocity.magnitude / ChefTopSpeed,
                Objective = hits / (float)HitsToWin,
            };
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return new PairOfAcesBrain(Snapshot, participant.Slot, participant.Skill);
        }
    }

    public sealed class PairOfAcesBrain : CpuBrain<PairOfAcesSnapshot>
    {
        private readonly ParticipantSlot slot;
        private Vector2 target;
        private float retargetTimer;

        public PairOfAcesBrain(System.Func<PairOfAcesSnapshot> world, ParticipantSlot slot, CpuSkill skill)
            : base(world, skill)
        {
            this.slot = slot;
        }

        protected override void Think(in PairOfAcesSnapshot world, float deltaTime)
        {
            GunnerView self = world.GunnerFor(slot);

            float incoming = self.IncomingWasabiBombTime;
            if (incoming >= 0f && incoming < Mathf.Lerp(0.20f, 0.34f, Competence))
            {
                Press(MinigameAction.Secondary);
            }

            if (Elapsed(ref retargetTimer, deltaTime, ReactionDelay * 0.5f))
            {
                Vector2 lead = world.ChefPosition + world.ChefVelocity * world.RiceBallFlightTime;
                float jitter = AimJitter * 3.5f;
                target = lead + new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));
            }

            Vector2 delta = target - self.Crosshair;
            Steer(delta.sqrMagnitude < 0.04f ? Vector2.zero : delta.normalized);

            if (delta.magnitude < 0.7f && self.CanFire && world.ChefVulnerable)
            {
                Press(MinigameAction.Primary);
            }
        }
    }
}
