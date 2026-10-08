using SushiParty.Core;

namespace SushiParty.Minigames.PedalToThePaddle
{
    public readonly struct PedalRiderState
    {
        public readonly float Angle;
        public readonly bool Ready;

        public PedalRiderState(float angle, bool ready)
        {
            Angle = angle;
            Ready = ready;
        }
    }

    public readonly struct PedalToThePaddleSnapshot
    {
        public readonly PedalRiderState One;
        public readonly PedalRiderState Two;

        public PedalToThePaddleSnapshot(PedalRiderState one, PedalRiderState two)
        {
            One = one;
            Two = two;
        }

        public PedalRiderState Rider(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? One : Two;
        }
    }
}
