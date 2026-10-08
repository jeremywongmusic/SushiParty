namespace SushiParty.Board
{
    public sealed class ContinueGate
    {
        private readonly float minimumHold;
        private readonly float unattendedHold;
        private bool holding;

        public ContinueGate(float minimumHold, float unattendedHold)
        {
            this.minimumHold = minimumHold;
            this.unattendedHold = unattendedHold;
        }

        public float Held { get; private set; }

        public bool AwaitingPress => holding && Held >= minimumHold;

        public void Open()
        {
            holding = true;
            Held = 0f;
        }

        public bool Tick(float deltaTime, bool pressed, bool anyoneWatching)
        {
            if (!holding)
            {
                return false;
            }

            Held += deltaTime;

            if (Held < minimumHold)
            {
                return false;
            }

            if (pressed)
            {
                return Release();
            }

            if (!anyoneWatching && Held >= unattendedHold)
            {
                return Release();
            }

            return false;
        }

        private bool Release()
        {
            holding = false;
            Held = 0f;
            return true;
        }
    }
}
