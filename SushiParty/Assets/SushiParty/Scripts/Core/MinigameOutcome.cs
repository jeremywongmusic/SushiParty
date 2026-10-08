namespace SushiParty.Core
{
    public enum MinigameResult
    {
        PlayersWin = 0,

        ChefTakoWins = 1,

        Abandoned = 2,
    }

    public readonly struct MinigameOutcome
    {
        public readonly MinigameResult Result;
        public readonly float TimeRemaining;
        public readonly string Detail;

        public MinigameOutcome(MinigameResult result, float timeRemaining, string detail)
        {
            Result = result;
            TimeRemaining = timeRemaining;
            Detail = detail;
        }

        public bool Won => Result == MinigameResult.PlayersWin;

        public int CoinDelta => Result switch
        {
            MinigameResult.PlayersWin => MinigameLibrary.CoinStake,
            MinigameResult.ChefTakoWins => -MinigameLibrary.CoinStake,
            _ => 0,
        };

        public static MinigameOutcome Win(float timeRemaining, string detail = null)
        {
            return new MinigameOutcome(MinigameResult.PlayersWin, timeRemaining, detail);
        }

        public static MinigameOutcome Lose(string detail = null)
        {
            return new MinigameOutcome(MinigameResult.ChefTakoWins, 0f, detail);
        }

        public static MinigameOutcome Abandon()
        {
            return new MinigameOutcome(MinigameResult.Abandoned, 0f, null);
        }
    }

    public enum MinigamePhase
    {
        Idle = 0,

        Briefing = 1,

        Countdown = 2,

        Playing = 3,

        Settling = 4,

        Finished = 5,
    }
}
