using UnityEngine;

namespace SushiParty.Audio
{
    public static class Global
    {
        public const string Minigame = "Minigame";
        public const string RoundPhase = "RoundPhase";
        public const string RoundProgress = "RoundProgress";
        public const string TimeRemaining = "TimeRemaining";
        public const string PlayerMotion = "PlayerMotion";
        public const string ChefMotion = "ChefMotion";
        public const string Objective = "Objective";
        public const string Paused = "Paused";
        public const string Coins = "Coins";
        public const string CoinSwing = "CoinSwing";
        public const string BoardRound = "BoardRound";
        public const string BoardProgress = "BoardProgress";
        public const string Difficulty = "Difficulty";
        public const string HumanCount = "HumanCount";
        public const float LongestRound = 90f;
        public const float BoardRounds = 10f;
    }

    public struct MinigameTelemetry
    {
        public float PlayerMotion;
        public float ChefMotion;
        public float Objective;
        public static MinigameTelemetry None => default;

        public MinigameTelemetry Clamped()
        {
            return new MinigameTelemetry
            {
                PlayerMotion = Mathf.Clamp01(PlayerMotion),
                ChefMotion = Mathf.Clamp01(ChefMotion),
                Objective = Mathf.Clamp01(Objective),
            };
        }
    }
}
