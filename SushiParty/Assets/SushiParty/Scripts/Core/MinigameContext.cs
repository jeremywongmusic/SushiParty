namespace SushiParty.Core
{
    public sealed class MinigameContext
    {
        public MinigameDefinition Definition { get; }
        public MatchSetup Setup { get; }
        public Participant[] Participants { get; }

        public bool IsDirectPlay { get; }

        public Participant One => Participants[0];
        public Participant Two => Participants[1];
        public CpuSkill ChefSkill => Setup.ChefSkill;

        public MinigameContext(MinigameDefinition definition, MatchSetup setup, bool isDirectPlay)
        {
            Definition = definition;
            Setup = setup;
            Participants = setup.CreateParticipants();
            IsDirectPlay = isDirectPlay;
        }

        public void TickInputs(float deltaTime)
        {
            for (int i = 0; i < Participants.Length; i++)
            {
                Participants[i].Input.Tick(deltaTime);
            }
        }

        public static MinigameContext CreateDirectPlay(MinigameId id)
        {
            return new MinigameContext(MinigameLibrary.Get(id), MatchSetup.Debug(id), isDirectPlay: true);
        }
    }
}
