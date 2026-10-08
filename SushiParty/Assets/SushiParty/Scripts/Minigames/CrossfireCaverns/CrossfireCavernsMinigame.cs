using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.CrossfireCaverns
{
    public sealed class CrossfireCavernsMinigame : MinigameController
    {
        private const int HitsToWin = 3;
        private const float RailHalfLength = 11f;
        private const float RailZ = 8f;
        private const float CartSpeed = 9f;
        private const float BallSpeed = 26f;
        private const float BallRadius = 0.35f;
        private const float FireCooldown = 0.45f;
        private const float ChefRadius = 1.1f;
        private const float ChefStunDuration = 0.9f;
        private const float ChefInvulnerability = 1.2f;
        private const float DisableDuration = 1.3f;
        private const int StalagmiteCount = 5;

        private sealed class Cart
        {
            public Transform View;
            public Participant Owner;
            public float X;
            public float Z;
            public float FireTimer;
            public float DisabledTimer;
            public int Direction;
            public AudioHandle Rumble;
            public CharacterAnimation Animation;
            public bool Disabled => DisabledTimer > 0f;
        }

        private sealed class Ball
        {
            public Transform View;
            public Vector2 Position;
            public int Direction;
            public Cart Shooter;
        }

        private sealed class Stalagmite
        {
            public Transform View;
            public Vector2 Position;
            public float Radius;
            public bool Alive;
        }

        private readonly List<Ball> balls = new List<Ball>();
        private readonly List<Stalagmite> stalagmites = new List<Stalagmite>();
        private readonly Vulnerability chefHits = new Vulnerability();
        private Cart cartOne;
        private Cart cartTwo;
        private Transform arena;
        private Transform chefView;
        private CharacterAnimation chefAnimation;
        private Vector2 chefPosition;
        private Vector2 chefDrift = new Vector2(1f, 0.6f);
        private float chefRethinkTimer;
        private int hits;
        public override MinigameId Id => MinigameId.CrossfireCaverns;

        public CrossfireCavernsSnapshot Snapshot()
        {
            return new CrossfireCavernsSnapshot(
                chefPosition,
                chefHits.CanBeHit,
                Describe(cartOne),
                Describe(cartTwo));
        }

        private CrossfireCartSnapshot Describe(Cart cart)
        {
            if (cart == null)
            {
                return default;
            }

            return new CrossfireCartSnapshot(
                cart.X,
                cart.Disabled,
                !cart.Disabled && cart.FireTimer <= 0f,
                LaneClear(cart.X, cart.Z, chefPosition.y));
        }

        private bool LaneClear(float x, float fromZ, float toZ)
        {
            float low = Mathf.Min(fromZ, toZ);
            float high = Mathf.Max(fromZ, toZ);

            foreach (Stalagmite stalagmite in stalagmites)
            {
                if (!stalagmite.Alive)
                {
                    continue;
                }

                if (Mathf.Abs(stalagmite.Position.x - x) > stalagmite.Radius + BallRadius)
                {
                    continue;
                }

                if (stalagmite.Position.y > low && stalagmite.Position.y < high)
                {
                    return false;
                }
            }

            return true;
        }

        protected override void OnPrepare()
        {
            arena = new GameObject("Arena").transform;
            arena.SetParent(transform, false);

            BuildCavern();
            BuildStalagmites();

            cartOne = BuildCart(P1, -RailZ, direction: 1);
            cartTwo = BuildCart(P2, RailZ, direction: -1);

            chefView = Shapes.Octopus(arena, Palette.TakoPurple, 0.95f, "ChefTako", isChef: true);
            chefAnimation = CharacterAnimation.Attach(chefView);
            chefPosition = Vector2.zero;

            Hud.ConfigurePips(HitsToWin, "direct hits on Chef Tako");
            Hud.SetPips(0);
            Hud.SetStatus("Line up with him and fire — but a miss carries on into your teammate");
        }

        private void BuildCavern()
        {
            Shapes.Cube(
                arena,
                new Vector3(0f, -0.4f, 0f),
                new Vector3(RailHalfLength * 2f + 4f, 0.8f, RailZ * 2f + 4f),
                new Color(0.19f, 0.17f, 0.21f),
                "Floor");

            foreach (int side in new[] { -1, 1 })
            {
                Shapes.Cube(
                    arena,
                    new Vector3(0f, 0.05f, RailZ * side),
                    new Vector3(RailHalfLength * 2f + 2f, 0.1f, 1.2f),
                    new Color(0.30f, 0.28f, 0.33f),
                    $"Rail{side}");
            }
        }

        private void BuildStalagmites()
        {
            for (int i = 0; i < StalagmiteCount; i++)
            {
                float x = Random.Range(-RailHalfLength + 1.5f, RailHalfLength - 1.5f);
                float z = Random.Range(-4.5f, 4.5f);
                float radius = Random.Range(0.7f, 1.05f);

                Transform view = Shapes.Cylinder(
                    arena,
                    new Vector3(x, 1.1f, z),
                    new Vector3(radius * 2f, 1.1f, radius * 2f),
                    new Color(0.42f, 0.38f, 0.44f),
                    $"Stalagmite{i}");

                stalagmites.Add(new Stalagmite
                {
                    View = view,
                    Position = new Vector2(x, z),
                    Radius = radius,
                    Alive = true,
                });
            }
        }

        private Cart BuildCart(Participant participant, float z, int direction)
        {
            Transform view = Shapes.Octopus(arena, participant.Color, 0.8f, $"Cart_{participant.Slot}");

            Shapes.Cube(
                view,
                new Vector3(0f, 0.75f, direction * 0.95f),
                new Vector3(0.35f, 0.35f, 1.3f),
                Color.Lerp(participant.Color, Color.black, 0.4f),
                "Cannon");

            Cart cart = new Cart
            {
                View = view,
                Owner = participant,
                X = direction > 0 ? -3f : 3f,
                Z = z,
                Direction = direction,
                Animation = CharacterAnimation.Attach(view),
            };

            view.position = new Vector3(cart.X, 0f, z);
            return cart;
        }

        protected override void OnBegin()
        {
            cartOne.Rumble = GameAudio.LoopAt(Sfx.CrossfireCartLoop, cartOne.View.position);
            cartTwo.Rumble = GameAudio.LoopAt(Sfx.CrossfireCartLoop, cartTwo.View.position);
        }

        protected override void OnConclude(MinigameOutcome result)
        {
            GameAudio.Stop(ref cartOne.Rumble);
            GameAudio.Stop(ref cartTwo.Rumble);

            if (!result.Won)
            {
                return;
            }

            cartOne.Animation.Cheer();
            cartTwo.Animation.Cheer();
        }

        protected override void OnPlay(float deltaTime)
        {
            TickCart(cartOne, deltaTime);
            TickCart(cartTwo, deltaTime);
            TickChef(deltaTime);
            TickBalls(deltaTime);

            if (hits >= HitsToWin)
            {
                Win($"Three direct hits — his cart went off the rails");
            }
        }

        protected override void OnSettle(float deltaTime, MinigameOutcome result)
        {
            TickBalls(deltaTime);

            cartOne.Animation.SetSpeed(0f);
            cartTwo.Animation.SetSpeed(0f);
            chefAnimation.SetSpeed(0f);
        }

        private void TickCart(Cart cart, float deltaTime)
        {
            bool wasDisabled = cart.Disabled;

            cart.FireTimer = Mathf.Max(0f, cart.FireTimer - deltaTime);
            cart.DisabledTimer = Mathf.Max(0f, cart.DisabledTimer - deltaTime);

            if (wasDisabled && !cart.Disabled)
            {
                GameAudio.PlayAt(Sfx.CrossfireCartRecovered, cart.View.position);
            }

            float travelled = 0f;

            if (!cart.Disabled)
            {
                float move = cart.Owner.Input.Move.x;
                float previous = cart.X;
                cart.X = Mathf.Clamp(cart.X + move * CartSpeed * deltaTime, -RailHalfLength, RailHalfLength);
                travelled = Mathf.Abs(cart.X - previous);

                if (cart.Owner.Input.WasPressed(MinigameAction.Primary))
                {
                    if (cart.FireTimer <= 0f)
                    {
                        Fire(cart);
                    }
                    else
                    {
                        GameAudio.PlayAt(Sfx.CrossfireFireBlocked, cart.View.position);
                    }
                }
            }
            else if (cart.Owner.Input.WasPressed(MinigameAction.Primary))
            {
                GameAudio.PlayAt(Sfx.CrossfireFireBlocked, cart.View.position);
            }

            cart.View.position = new Vector3(cart.X, 0f, cart.Z);

            cart.Animation.SetSpeed(deltaTime > 0f ? travelled / (CartSpeed * deltaTime) : 0f);

            cart.Animation.SetStunned(cart.Disabled);

            GameAudio.SetPosition(cart.Rumble, cart.View.position);
            GameAudio.SetParameter(
                cart.Rumble,
                Sfx.SpeedParameter,
                cart.Disabled ? 0f : Mathf.Abs(cart.Owner.Input.Move.x));

            Transform body = cart.View.Find("Body");
            if (body != null)
            {
                Shapes.Tint(body, cart.Disabled ? Color.Lerp(cart.Owner.Color, Color.white, 0.65f) : cart.Owner.Color,
                    glow: cart.Disabled);
            }
        }

        private void Fire(Cart cart)
        {
            cart.FireTimer = FireCooldown;
            GameAudio.PlayAt(Sfx.CrossfireFire, new Vector3(cart.X, 0.8f, cart.Z));

            Transform view = Shapes.Sphere(arena, Vector3.zero, BallRadius * 2f, Palette.RiceWhite, "RiceBall");
            Shapes.Tint(view, Palette.RiceWhite, glow: true);

            Ball ball = new Ball
            {
                View = view,
                Position = new Vector2(cart.X, cart.Z + cart.Direction * 1.1f),
                Direction = cart.Direction,
                Shooter = cart,
            };

            ball.View.position = new Vector3(ball.Position.x, 0.8f, ball.Position.y);
            balls.Add(ball);
        }

        private void TickBalls(float deltaTime)
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                Ball ball = balls[i];
                ball.Position.y += ball.Direction * BallSpeed * deltaTime;
                ball.View.position = new Vector3(ball.Position.x, 0.8f, ball.Position.y);

                if (ResolveBall(ball))
                {
                    Destroy(ball.View.gameObject);
                    balls.RemoveAt(i);
                    continue;
                }

                if (Mathf.Abs(ball.Position.y) > RailZ + 3f)
                {
                    GameAudio.PlayAt(Sfx.CrossfireBallLost,
                        new Vector3(ball.Position.x, 0.8f, ball.Position.y));

                    Destroy(ball.View.gameObject);
                    balls.RemoveAt(i);
                }
            }
        }

        private bool ResolveBall(Ball ball)
        {
            foreach (Stalagmite stalagmite in stalagmites)
            {
                if (!stalagmite.Alive)
                {
                    continue;
                }

                if (Vector2.Distance(stalagmite.Position, ball.Position) < stalagmite.Radius + BallRadius)
                {
                    stalagmite.Alive = false;
                    GameAudio.PlayAt(Sfx.CrossfireHitRock,
                        new Vector3(stalagmite.Position.x, 0.8f, stalagmite.Position.y));

                    if (stalagmite.View != null)
                    {
                        Destroy(stalagmite.View.gameObject);
                    }

                    return true;
                }
            }

            if (!chefHits.IsStunned
                && Vector2.Distance(chefPosition, ball.Position) < ChefRadius + BallRadius)
            {
                if (chefHits.TryHit(ChefStunDuration, ChefInvulnerability))
                {
                    RegisterHit();
                }

                return true;
            }

            Cart victim = ball.Shooter == cartOne ? cartTwo : cartOne;
            if (Mathf.Abs(ball.Position.y - victim.Z) < 0.9f && Mathf.Abs(ball.Position.x - victim.X) < 1f)
            {
                victim.DisabledTimer = DisableDuration;
                Hud.SetStatus($"{victim.Owner.DisplayName} took friendly fire — mind the crossfire");

                GameAudio.PlayAt(Sfx.CrossfireFriendlyFire, new Vector3(victim.X, 0.8f, victim.Z));
                GameAudio.PlayAt(Sfx.CrossfireCartDisabled, new Vector3(victim.X, 0.8f, victim.Z));
                return true;
            }

            return false;
        }

        private void RegisterHit()
        {
            hits++;
            Hud.SetPips(hits);
            GameAudio.PlayAt(Sfx.CrossfireHitChef, new Vector3(chefPosition.x, 0.8f, chefPosition.y));

            int left = HitsToWin - hits;
            if (left > 0)
            {
                Hud.SetStatus($"Hit! {left} more to go");
            }
        }

        private void TickChef(float deltaTime)
        {
            chefHits.Tick(deltaTime);

            Vector2 previous = chefPosition;

            if (!chefHits.IsStunned)
            {
                Steer(deltaTime);
                chefPosition += chefDrift * (EvadeSpeed() * deltaTime);
                chefPosition.x = Mathf.Clamp(chefPosition.x, -RailHalfLength + 1f, RailHalfLength - 1f);
                chefPosition.y = Mathf.Clamp(chefPosition.y, -4.6f, 4.6f);
            }

            chefView.position = new Vector3(chefPosition.x, 0f, chefPosition.y);

            float travelled = Vector2.Distance(previous, chefPosition);
            chefAnimation.SetSpeed(deltaTime > 0f ? travelled / (EvadeSpeed() * deltaTime) : 0f);

            chefAnimation.SetStunned(chefHits.IsStunned);

            bool blink = chefHits.HiddenWhileImmune(Time.time);
            Transform body = chefView.Find("Body");
            if (body != null)
            {
                body.gameObject.SetActive(!blink);
                Shapes.Tint(body, chefHits.IsStunned ? Color.white : Palette.TakoPurple, chefHits.IsStunned);
            }
        }

        private float EvadeSpeed()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => 5.2f,
                CpuSkill.Sharp => 8.4f,
                _ => 6.8f,
            };
        }

        private void Steer(float deltaTime)
        {
            if (!Elapsed(ref chefRethinkTimer, deltaTime, 0.35f))
            {
                return;
            }

            float threat = 0f;
            foreach (Cart cart in new[] { cartOne, cartTwo })
            {
                if (cart == null || cart.Disabled)
                {
                    continue;
                }

                float offset = chefPosition.x - cart.X;
                float weight = Mathf.Clamp01(1f - Mathf.Abs(offset) / 6f);
                threat += Mathf.Sign(offset == 0f ? Random.Range(-1f, 1f) : offset) * weight;
            }

            Vector2 desired = new Vector2(threat, chefDrift.y);

            if (chefPosition.x > RailHalfLength - 2f) desired.x = -1f;
            if (chefPosition.x < -RailHalfLength + 2f) desired.x = 1f;
            if (chefPosition.y > 4f) desired.y = -Mathf.Abs(desired.y);
            if (chefPosition.y < -4f) desired.y = Mathf.Abs(desired.y);

            if (desired.sqrMagnitude < 0.01f)
            {
                desired = new Vector2(Random.Range(-1f, 1f), chefDrift.y);
            }

            chefDrift = Vector2.Lerp(chefDrift, desired.normalized, 0.6f).normalized;
        }

        private static bool Elapsed(ref float timer, float deltaTime, float interval)
        {
            timer -= deltaTime;
            if (timer > 0f)
            {
                return false;
            }

            timer = interval;
            return true;
        }

        protected override MinigameOutcome OnTimeUp()
        {
            return MinigameOutcome.Lose($"{hits} of {HitsToWin} hits landed");
        }

        protected override MinigameTelemetry SampleTelemetry()
        {
            float carts = 0f;
            foreach (Cart cart in new[] { cartOne, cartTwo })
            {
                if (cart != null && !cart.Disabled)
                {
                    carts = Mathf.Max(carts, Mathf.Abs(cart.Owner.Input.Move.x));
                }
            }

            return new MinigameTelemetry
            {
                PlayerMotion = carts,
                ChefMotion = chefHits.IsStunned ? 0f : 1f,
                Objective = hits / (float)HitsToWin,
            };
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return new CrossfireBrain(Snapshot, participant.Slot, participant.Skill);
        }
    }

    public sealed class CrossfireBrain : CpuBrain<CrossfireCavernsSnapshot>
    {
        private readonly ParticipantSlot slot;
        private readonly ParticipantSlot partnerSlot;
        private float targetX;
        private float retargetTimer;

        public CrossfireBrain(
            System.Func<CrossfireCavernsSnapshot> world,
            ParticipantSlot slot,
            CpuSkill skill)
            : base(world, skill)
        {
            this.slot = slot;
            partnerSlot = slot == ParticipantSlot.One ? ParticipantSlot.Two : ParticipantSlot.One;
        }

        protected override void Think(in CrossfireCavernsSnapshot world, float deltaTime)
        {
            CrossfireCartSnapshot mine = world.Cart(slot);

            if (mine.Disabled)
            {
                Steer(Vector2.zero);
                return;
            }

            if (Elapsed(ref retargetTimer, deltaTime, ReactionDelay))
            {
                float jitter = AimJitter * 3f;
                targetX = world.ChefPosition.x + Random.Range(-jitter, jitter);
            }

            float own = mine.X;
            float delta = targetX - own;
            Steer(new Vector2(Mathf.Abs(delta) < 0.1f ? 0f : Mathf.Sign(delta), 0f));

            if (!mine.CanFire || Mathf.Abs(delta) > 0.6f)
            {
                return;
            }

            if (!world.ChefVulnerable)
            {
                return;
            }

            if (Mathf.Abs(world.Cart(partnerSlot).X - own) < 1.4f)
            {
                return;
            }

            if (!mine.LaneClearToChef)
            {
                return;
            }

            Press(MinigameAction.Primary);
        }
    }
}
