using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Minigames.SandTrap
{
    public readonly struct SandTrapSeat
    {
        public readonly bool Exists;
        public readonly float TrackPosition;
        public readonly bool Grounded;
        public readonly bool Airborne;
        public readonly bool Pounding;
        public readonly float HighlightRemaining;

        public SandTrapSeat(
            bool exists,
            float trackPosition,
            bool grounded,
            bool airborne,
            bool pounding,
            float highlightRemaining)
        {
            Exists = exists;
            TrackPosition = trackPosition;
            Grounded = grounded;
            Airborne = airborne;
            Pounding = pounding;
            HighlightRemaining = highlightRemaining;
        }
    }

    public readonly struct SandTrapSnapshot
    {
        public readonly SandTrapSeat Rows;
        public readonly SandTrapSeat Columns;
        public readonly Vector2Int ChefTarget;

        public SandTrapSnapshot(SandTrapSeat rows, SandTrapSeat columns, Vector2Int chefTarget)
        {
            Rows = rows;
            Columns = columns;
            ChefTarget = chefTarget;
        }

        public SandTrapSeat SeatFor(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? Rows : Columns;
        }

        public SandTrapSeat PartnerOf(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? Columns : Rows;
        }
    }
}
