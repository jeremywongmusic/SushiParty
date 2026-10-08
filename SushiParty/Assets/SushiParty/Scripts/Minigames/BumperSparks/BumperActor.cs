using SushiParty.Audio;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.BumperSparks
{
    public sealed class BumperActor
    {
        private const float Restitution = 1.35f;
        private readonly Transform view;
        private readonly Transform body;
        private readonly Vector3 bodyBaseScale;
        private readonly Color baseColor;
        private readonly Vulnerability hits = new Vulnerability();
        private readonly CharacterAnimation animation;

        public BumperActor(Transform view, IParticipantInput input, Color color, bool isChef)
        {
            this.view = view;
            Input = input;
            baseColor = color;
            IsChef = isChef;
            body = view.Find("Body");
            bodyBaseScale = body != null ? body.localScale : Vector3.one;

            animation = CharacterAnimation.Attach(view);
        }

        public IParticipantInput Input { get; }
        public bool IsChef { get; }

        public Vector2 Position;
        public Vector2 Velocity;
        public AudioHandle Engine;
        public float Radius = 0.85f;
        public float Acceleration = 34f;
        public float MaxSpeed = 9f;
        public bool IsStunned => hits.IsStunned;
        public Vector3 WorldPosition => new Vector3(Position.x, 0f, Position.y);

        public void Place(Vector2 position)
        {
            Position = position;
            Velocity = Vector2.zero;
            SyncView();
        }

        public void Integrate(float deltaTime, bool controlsEnabled)
        {
            hits.Tick(deltaTime);

            if (controlsEnabled && !IsStunned)
            {
                Vector2 desired = Vector2.ClampMagnitude(Input.Move, 1f);
                Velocity += desired * (Acceleration * deltaTime);
            }

            Velocity *= Mathf.Exp(-3.2f * deltaTime);
            Velocity = Vector2.ClampMagnitude(Velocity, MaxSpeed);
            Position += Velocity * deltaTime;
        }

        public static bool Resolve(BumperActor a, BumperActor b)
        {
            Vector2 delta = b.Position - a.Position;
            float distance = delta.magnitude;
            float minimum = a.Radius + b.Radius;

            if (distance >= minimum || distance < 0.0001f)
            {
                return false;
            }

            Vector2 normal = delta / distance;
            float overlap = minimum - distance;

            a.Position -= normal * (overlap * 0.5f);
            b.Position += normal * (overlap * 0.5f);

            float closingSpeed = Vector2.Dot(b.Velocity - a.Velocity, normal);
            if (closingSpeed < 0f)
            {
                float impulse = -(1f + Restitution) * closingSpeed * 0.5f;
                a.Velocity -= normal * impulse;
                b.Velocity += normal * impulse;

                Vector2 contact = a.Position + normal * a.Radius;
                GameAudio.PlayAt(
                    Sfx.BumperImpact,
                    new Vector3(contact.x, 0f, contact.y),
                    Sfx.ForceParameter,
                    Mathf.InverseLerp(0f, 18f, -closingSpeed));
            }

            return true;
        }

        public bool ClampToArena(float arenaRadius, float bounceRetention)
        {
            float limit = arenaRadius - Radius;
            float distance = Position.magnitude;

            if (distance <= limit || distance < 0.0001f)
            {
                return false;
            }

            Vector2 normal = Position / distance;
            Position = normal * limit;
            Velocity = (Velocity - 2f * Vector2.Dot(Velocity, normal) * normal) * bounceRetention;
            return true;
        }

        public void Stun(float seconds)
        {
            hits.Stun(seconds);
        }

        public bool TryHit(float stunSeconds, float immuneSeconds)
        {
            return hits.TryHit(stunSeconds, immuneSeconds);
        }

        public void Cheer()
        {
            animation.Cheer();
        }

        public void SyncView()
        {
            if (view == null)
            {
                return;
            }

            view.position = WorldPosition;

            if (Velocity.sqrMagnitude > 0.05f)
            {
                view.rotation = Quaternion.LookRotation(new Vector3(Velocity.x, 0f, Velocity.y), Vector3.up);
            }

            animation.SetSpeed(MaxSpeed > 0f ? Velocity.magnitude / MaxSpeed : 0f);

            animation.SetStunned(IsStunned);

            if (body == null)
            {
                return;
            }

            float squash = IsStunned ? 0.6f : 1f;
            body.localScale = new Vector3(bodyBaseScale.x, bodyBaseScale.y * squash, bodyBaseScale.z);

            bool hidden = hits.HiddenWhileImmune(Time.time);
            Shapes.Tint(
                body,
                IsStunned ? Color.Lerp(baseColor, Color.white, 0.6f) : baseColor,
                glow: IsStunned);
            body.gameObject.SetActive(!hidden);
        }
    }
}
