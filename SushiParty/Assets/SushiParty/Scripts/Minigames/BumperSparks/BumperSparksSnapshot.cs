using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Minigames.BumperSparks
{
    public readonly struct BumperVehicle
    {
        public readonly bool Present;
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly bool Stunned;

        public BumperVehicle(Vector2 position, float radius, bool stunned)
        {
            Present = true;
            Position = position;
            Radius = radius;
            Stunned = stunned;
        }
    }

    public readonly struct BumperSparksSnapshot
    {
        public readonly float ArenaRadius;
        public readonly BumperVehicle Chef;
        public readonly BumperVehicle PlayerOne;
        public readonly BumperVehicle PlayerTwo;

        public BumperSparksSnapshot(
            float arenaRadius,
            BumperVehicle chef,
            BumperVehicle playerOne,
            BumperVehicle playerTwo)
        {
            ArenaRadius = arenaRadius;
            Chef = chef;
            PlayerOne = playerOne;
            PlayerTwo = playerTwo;
        }

        public int PlayerCount => 2;

        public BumperVehicle Player(int index)
        {
            if (index == 0)
            {
                return PlayerOne;
            }

            return index == 1 ? PlayerTwo : default;
        }

        public BumperVehicle PlayerAt(ParticipantSlot slot)
        {
            return Player((int)slot);
        }
    }
}
