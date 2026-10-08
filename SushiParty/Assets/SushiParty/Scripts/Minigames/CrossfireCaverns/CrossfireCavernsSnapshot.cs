using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Minigames.CrossfireCaverns
{
    public readonly struct CrossfireCartSnapshot
    {
        public readonly float X;
        public readonly bool Disabled;
        public readonly bool CanFire;
        public readonly bool LaneClearToChef;

        public CrossfireCartSnapshot(float x, bool disabled, bool canFire, bool laneClearToChef)
        {
            X = x;
            Disabled = disabled;
            CanFire = canFire;
            LaneClearToChef = laneClearToChef;
        }
    }

    public readonly struct CrossfireCavernsSnapshot
    {
        public readonly Vector2 ChefPosition;
        public readonly bool ChefVulnerable;
        public readonly CrossfireCartSnapshot One;
        public readonly CrossfireCartSnapshot Two;

        public CrossfireCavernsSnapshot(
            Vector2 chefPosition,
            bool chefVulnerable,
            CrossfireCartSnapshot one,
            CrossfireCartSnapshot two)
        {
            ChefPosition = chefPosition;
            ChefVulnerable = chefVulnerable;
            One = one;
            Two = two;
        }

        public CrossfireCartSnapshot Cart(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? One : Two;
        }
    }
}
