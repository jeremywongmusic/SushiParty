using UnityEngine;

namespace SushiParty.InputLayer
{
    public enum MinigameAction
    {
        Primary = 0,

        Secondary = 1,
    }

    public interface IParticipantInput
    {
        Vector2 Move { get; }

        Vector2 Aim { get; }

        bool IsHeld(MinigameAction action);

        bool WasPressed(MinigameAction action);

        void Tick(float deltaTime);
    }

    public sealed class NullParticipantInput : IParticipantInput
    {
        public static readonly NullParticipantInput Instance = new NullParticipantInput();

        private NullParticipantInput()
        {
        }

        public Vector2 Move => Vector2.zero;
        public Vector2 Aim => Vector2.zero;
        public bool IsHeld(MinigameAction action) => false;
        public bool WasPressed(MinigameAction action) => false;
        public void Tick(float deltaTime) { }
    }
}
