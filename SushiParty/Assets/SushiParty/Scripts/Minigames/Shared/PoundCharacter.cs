using SushiParty.Audio;
using System;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.Shared
{
    public enum TrackAxis
    {
        X,
        Z,
    }

    public sealed class PoundCharacter
    {
        private const float Gravity = 30f;
        private const float JumpSpeed = 9.2f;
        private const float PoundSpeed = 24f;
        private const float WalkSpeed = 7.5f;
        private const float AirControl = 0.4f;
        private const float RecoverDuration = 0.22f;
        private const float StrideLength = 2.2f;
        private const int TentacleCount = 6;

        public enum State
        {
            Grounded,
            Airborne,
            Pounding,
            Recovering,
        }

        private readonly Transform view;
        private readonly Transform body;
        private readonly CharacterAnimation animation;
        private readonly TrackAxis axis;
        private readonly Vector3 origin;
        private readonly float minTrack;
        private readonly float maxTrack;
        private readonly Vector3 bodyBaseScale;
        private float trackPosition;
        private float height;
        private float verticalSpeed;
        private float recoverTimer;
        private float strideRemaining;
        private int strideFoot;
        public event Action<float> Pounded;

        public PoundCharacter(
            Transform view,
            IParticipantInput input,
            TrackAxis axis,
            Vector3 origin,
            float minTrack,
            float maxTrack,
            float startTrack)
        {
            this.view = view;
            this.axis = axis;
            this.origin = origin;
            this.minTrack = minTrack;
            this.maxTrack = maxTrack;

            Input = input;
            trackPosition = Mathf.Clamp(startTrack, minTrack, maxTrack);
            body = view.Find("Body");
            bodyBaseScale = body != null ? body.localScale : Vector3.one;

            animation = CharacterAnimation.Attach(view);

            Sync();
        }

        public IParticipantInput Input { get; }

        public State Current { get; private set; } = State.Grounded;

        public float TrackPosition => trackPosition;
        public bool IsPounding => Current == State.Pounding;

        public void Tick(float deltaTime, bool controlsEnabled)
        {
            if (!controlsEnabled)
            {
                animation.SetSpeed(0f);
                ApplyGravity(deltaTime);
                Sync();
                return;
            }

            HandleMovement(deltaTime);
            HandleJumpAndPound();
            ApplyGravity(deltaTime);
            Sync();
        }

        private void HandleMovement(float deltaTime)
        {
            if (Current == State.Recovering)
            {
                animation.SetSpeed(0f);
                return;
            }

            Vector2 move = Input.Move;
            float amount = axis == TrackAxis.X ? move.x : move.y;

            float speed = WalkSpeed * (Current == State.Grounded ? 1f : AirControl);
            float previous = trackPosition;
            trackPosition = Mathf.Clamp(trackPosition + amount * speed * deltaTime, minTrack, maxTrack);

            float travelled = Mathf.Abs(trackPosition - previous);
            animation.SetSpeed(deltaTime > 0f ? travelled / (WalkSpeed * deltaTime) : 0f);

            if (Current == State.Grounded)
            {
                TickStride(travelled);
            }
        }

        private void TickStride(float distance)
        {
            strideRemaining -= distance;
            if (strideRemaining > 0f)
            {
                return;
            }

            strideRemaining = StrideLength;
            strideFoot = (strideFoot + 1) % TentacleCount;
            GameAudio.PlayAt(Sfx.CharacterFootstep, view.position, Sfx.StepParameter, strideFoot);
        }

        private void HandleJumpAndPound()
        {
            if (!Input.WasPressed(MinigameAction.Primary))
            {
                return;
            }

            switch (Current)
            {
                case State.Grounded:
                    verticalSpeed = JumpSpeed;
                    Current = State.Airborne;
                    GameAudio.PlayAt(Sfx.CharacterJump, view.position);
                    animation.SetGrounded(false);
                    animation.Jump();
                    break;

                case State.Airborne:
                    verticalSpeed = -PoundSpeed;
                    Current = State.Pounding;

                    GameAudio.PlayAt(Sfx.CharacterPoundDive, view.position);
                    animation.Pound();
                    break;
            }
        }

        private void ApplyGravity(float deltaTime)
        {
            if (Current == State.Recovering)
            {
                recoverTimer -= deltaTime;
                if (recoverTimer <= 0f)
                {
                    Current = State.Grounded;
                    GameAudio.PlayAt(Sfx.CharacterPoundRecover, view.position);
                }

                return;
            }

            if (Current == State.Grounded)
            {
                return;
            }

            verticalSpeed -= Gravity * deltaTime;
            height += verticalSpeed * deltaTime;

            if (height > 0f)
            {
                return;
            }

            height = 0f;
            verticalSpeed = 0f;

            animation.SetGrounded(true);
            animation.Land();

            if (Current == State.Pounding)
            {
                Pounded?.Invoke(trackPosition);
                Current = State.Recovering;
                recoverTimer = RecoverDuration;
                return;
            }

            Current = State.Grounded;
            GameAudio.PlayAt(Sfx.CharacterLand, view.position);
        }

        private void Sync()
        {
            Vector3 position = origin;
            if (axis == TrackAxis.X)
            {
                position.x += trackPosition;
            }
            else
            {
                position.z += trackPosition;
            }

            position.y += height;
            view.position = position;

            if (body == null)
            {
                return;
            }

            float squash = Current == State.Pounding ? 0.55f : 1f;
            float stretch = Current == State.Airborne && verticalSpeed > 0f ? 1.25f : 1f;
            body.localScale = new Vector3(
                bodyBaseScale.x,
                bodyBaseScale.y * squash * stretch,
                bodyBaseScale.z);
        }
    }
}
