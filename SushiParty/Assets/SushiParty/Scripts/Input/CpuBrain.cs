using SushiParty.Core;
using UnityEngine;

namespace SushiParty.InputLayer
{
    public abstract class CpuBrain : IParticipantInput
    {
        private readonly bool[] pressedThisFrame = new bool[2];
        private readonly float[] holdRemaining = new float[2];
        private Vector2 move;
        private Vector2 aim;

        protected CpuBrain(CpuSkill skill)
        {
            Skill = skill;
        }

        protected CpuSkill Skill { get; }

        protected float ReactionDelay => Skill switch
        {
            CpuSkill.Relaxed => 0.34f,
            CpuSkill.Sharp => 0.09f,
            _ => 0.19f,
        };

        protected float AimJitter => Skill switch
        {
            CpuSkill.Relaxed => 0.30f,
            CpuSkill.Sharp => 0.05f,
            _ => 0.15f,
        };

        protected float Competence => Skill switch
        {
            CpuSkill.Relaxed => 0.35f,
            CpuSkill.Sharp => 1f,
            _ => 0.7f,
        };

        public Vector2 Move => move;
        public Vector2 Aim => aim;
        public bool IsHeld(MinigameAction action) => holdRemaining[(int)action] > 0f;
        public bool WasPressed(MinigameAction action) => pressedThisFrame[(int)action];

        public void Tick(float deltaTime)
        {
            pressedThisFrame[0] = false;
            pressedThisFrame[1] = false;

            for (int i = 0; i < holdRemaining.Length; i++)
            {
                if (holdRemaining[i] > 0f)
                {
                    holdRemaining[i] = Mathf.Max(0f, holdRemaining[i] - deltaTime);
                }
            }

            Think(deltaTime);
        }

        protected abstract void Think(float deltaTime);

        protected void Steer(Vector2 direction)
        {
            move = Vector2.ClampMagnitude(direction, 1f);
        }

        protected void AimAt(Vector2 screenPoint)
        {
            aim = screenPoint;
        }

        protected void Press(MinigameAction action, float holdSeconds = 0.10f)
        {
            pressedThisFrame[(int)action] = true;
            holdRemaining[(int)action] = Mathf.Max(holdRemaining[(int)action], holdSeconds);
        }

        protected static bool Elapsed(ref float timer, float deltaTime, float interval)
        {
            timer -= deltaTime;
            if (timer > 0f)
            {
                return false;
            }

            timer = interval;
            return true;
        }

        protected Vector3 Blur(Vector3 target, float scale = 1f)
        {
            float amount = AimJitter * scale;
            return target + new Vector3(Random.Range(-amount, amount), 0f, Random.Range(-amount, amount));
        }
    }

    public abstract class CpuBrain<TSnapshot> : CpuBrain
        where TSnapshot : struct
    {
        private readonly System.Func<TSnapshot> world;

        protected CpuBrain(System.Func<TSnapshot> world, CpuSkill skill)
            : base(skill)
        {
            this.world = world ?? throw new System.ArgumentNullException(nameof(world));
        }

        protected sealed override void Think(float deltaTime)
        {
            TSnapshot current = world();
            Think(in current, deltaTime);
        }

        protected abstract void Think(in TSnapshot world, float deltaTime);
    }
}
