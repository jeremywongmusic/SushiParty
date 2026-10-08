using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Minigames.PairOfAces
{
    public readonly struct GunnerView
    {
        public readonly Vector2 Crosshair;
        public readonly bool CanFire;
        public readonly float IncomingWasabiBombTime;

        public GunnerView(Vector2 crosshair, bool canFire, float incomingWasabiBombTime)
        {
            Crosshair = crosshair;
            CanFire = canFire;
            IncomingWasabiBombTime = incomingWasabiBombTime;
        }
    }

    public readonly struct PairOfAcesSnapshot
    {
        public readonly Vector2 ChefPosition;
        public readonly Vector2 ChefVelocity;
        public readonly bool ChefVulnerable;
        public readonly float RiceBallFlightTime;
        public readonly GunnerView GunnerOne;
        public readonly GunnerView GunnerTwo;

        public PairOfAcesSnapshot(
            Vector2 chefPosition,
            Vector2 chefVelocity,
            bool chefVulnerable,
            float riceBallFlightTime,
            GunnerView gunnerOne,
            GunnerView gunnerTwo)
        {
            ChefPosition = chefPosition;
            ChefVelocity = chefVelocity;
            ChefVulnerable = chefVulnerable;
            RiceBallFlightTime = riceBallFlightTime;
            GunnerOne = gunnerOne;
            GunnerTwo = gunnerTwo;
        }

        public GunnerView GunnerFor(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? GunnerOne : GunnerTwo;
        }
    }
}
