namespace SushiParty.Minigames.Shared
{
    public sealed class PoundActuator
    {
        private const float AbandonAfter = 0.9f;
        private readonly float apexDelay;
        private float airTimer;

        public PoundActuator(float apexDelay)
        {
            this.apexDelay = apexDelay;
        }

        public bool Busy { get; private set; }

        public void Begin()
        {
            Busy = true;
            airTimer = 0f;
        }

        public bool Tick(PoundCharacter character, float deltaTime)
        {
            if (!Busy)
            {
                return false;
            }

            airTimer += deltaTime;

            if (character != null
                && character.Current == PoundCharacter.State.Airborne
                && airTimer >= apexDelay)
            {
                Busy = false;
                return true;
            }

            if (airTimer > AbandonAfter)
            {
                Busy = false;
            }

            return false;
        }
    }
}
