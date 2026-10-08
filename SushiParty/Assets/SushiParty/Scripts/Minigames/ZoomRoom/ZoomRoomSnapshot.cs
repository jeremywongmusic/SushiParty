using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Minigames.ZoomRoom
{
    public readonly struct ZoomRoomSnapshot
    {
        public readonly MazeGrid Maze;
        public readonly Vector2 ChefPosition;
        public readonly Vector2 PlayerOnePosition;
        public readonly Vector2 PlayerTwoPosition;

        public ZoomRoomSnapshot(
            MazeGrid maze,
            Vector2 chefPosition,
            Vector2 playerOnePosition,
            Vector2 playerTwoPosition)
        {
            Maze = maze;
            ChefPosition = chefPosition;
            PlayerOnePosition = playerOnePosition;
            PlayerTwoPosition = playerTwoPosition;
        }

        public Vector2 PlayerPosition(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? PlayerOnePosition : PlayerTwoPosition;
        }
    }
}
