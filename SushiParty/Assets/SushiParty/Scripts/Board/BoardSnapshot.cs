using SushiParty.Core;
using SushiParty.InputLayer;

namespace SushiParty.Board
{
    public readonly struct BoardSnapshot
    {
        private static readonly int[] NoChoices = new int[0];
        // Shared by every snapshot away from a fork, so reading the board allocates nothing.
        public readonly BoardSeat Turn;
        public readonly bool AwaitingRoll;
        public readonly bool AwaitingStop;
        public readonly int Round;
        public readonly int TotalRounds;
        public readonly int SelfSpace;
        public readonly int SelfCoins;
        public readonly int SelfGoldenCoins;
        public readonly int ShrineSpace;
        public readonly int DistanceToShrine;
        public readonly bool AwaitingChoice;
        public readonly int[] Choices;
        public readonly int HighlightedChoice;
        public readonly int[] ChoiceStepsToShrine;

        public BoardSnapshot(
            BoardSeat turn,
            bool awaitingRoll,
            int round,
            int totalRounds,
            int selfSpace,
            int selfCoins,
            int selfGoldenCoins,
            int shrineSpace,
            int distanceToShrine,
            bool awaitingChoice = false,
            int[] choices = null,
            int highlightedChoice = 0,
            int[] choiceStepsToShrine = null,
            bool awaitingStop = false)
        {
            Turn = turn;
            AwaitingRoll = awaitingRoll;
            AwaitingStop = awaitingStop;
            Round = round;
            TotalRounds = totalRounds;
            SelfSpace = selfSpace;
            SelfCoins = selfCoins;
            SelfGoldenCoins = selfGoldenCoins;
            ShrineSpace = shrineSpace;
            DistanceToShrine = distanceToShrine;
            AwaitingChoice = awaitingChoice;

            Choices = choices ?? NoChoices;
            HighlightedChoice = highlightedChoice;
            ChoiceStepsToShrine = choiceStepsToShrine ?? NoChoices;
        }

        public bool CanAffordShrine => SelfCoins >= BoardSession.ShrinePrice;
    }

    public sealed class BoardBrain : CpuBrain<BoardSnapshot>
    {
        private const int ShrineWithinReach = BoardSession.CoinGainAmount + 1;
        private const float StopFloor = 0.40f;
        private const float StopSpread = 1.10f;
        private const float MisreadBase = 0.25f;
        private const float NudgeHold = 0.12f;
        private const float NudgeGap = 0.06f;
        private const int Undecided = -1;
        private readonly BoardSeat seat;
        private float dwell;
        private float stopDwell;
        private float stopDelay;
        private float choiceDwell;
        private float nudgeElapsed;
        private int committed = Undecided;

        public BoardBrain(System.Func<BoardSnapshot> world, BoardSeat seat, CpuSkill skill)
            : base(world, skill)
        {
            this.seat = seat;
        }

        private float RollDelay => 0.35f + ReactionDelay * 2f;
        private float ConfirmDelay => 0.25f + ReactionDelay;
        private float NudgeInterval => NudgeHold + NudgeGap + ReactionDelay;
        private float MisreadChance => (1f - Competence) * (MisreadBase + AimJitter);

        protected override void Think(in BoardSnapshot world, float deltaTime)
        {
            Steer(UnityEngine.Vector2.zero);

            if (world.Turn != seat)
            {
                Forget();
                return;
            }

            if (world.AwaitingChoice)
            {
                dwell = 0f;
                ForgetStop();
                AnswerFork(in world, deltaTime);
                return;
            }

            ForgetFork();

            if (world.AwaitingStop)
            {
                dwell = 0f;
                CatchBlock(deltaTime);
                return;
            }

            ForgetStop();

            if (!world.AwaitingRoll)
            {
                dwell = 0f;
                return;
            }

            dwell += deltaTime;
            if (dwell < RollDelay)
            {
                return;
            }

            dwell = 0f;
            Press(MinigameAction.Primary);
        }

        private void CatchBlock(float deltaTime)
        {
            if (stopDelay <= 0f)
            {
                stopDelay = NextStopDelay();
            }

            stopDwell += deltaTime;
            if (stopDwell < stopDelay)
            {
                return;
            }

            ForgetStop();
            Press(MinigameAction.Primary);
        }

        private float NextStopDelay()
        {
            return StopFloor + ReactionDelay + UnityEngine.Random.Range(0f, StopSpread);
        }

        private void AnswerFork(in BoardSnapshot world, float deltaTime)
        {
            int count = world.Choices.Length;
            if (count == 0)
            {
                ForgetFork();
                return;
            }

            if (committed == Undecided)
            {
                committed = Decide(in world, count);
            }

            int highlight = world.HighlightedChoice;
            if (highlight < 0 || highlight >= count)
            {
                return;
            }

            if (highlight != committed)
            {
                choiceDwell = 0f;
                Nudge(highlight < committed ? 1f : -1f, deltaTime);
                return;
            }

            nudgeElapsed = 0f;
            choiceDwell += deltaTime;
            if (choiceDwell < ConfirmDelay)
            {
                return;
            }

            choiceDwell = 0f;
            Press(MinigameAction.Primary);
        }

        private void Nudge(float direction, float deltaTime)
        {
            nudgeElapsed += deltaTime;
            if (nudgeElapsed >= NudgeInterval)
            {
                nudgeElapsed = 0f;
            }

            if (nudgeElapsed >= NudgeInterval - NudgeHold)
            {
                Steer(new UnityEngine.Vector2(direction, 0f));
            }
        }

        private int Decide(in BoardSnapshot world, int count)
        {
            int preferred = Prefer(in world, count);

            if (count < 2 || UnityEngine.Random.value >= MisreadChance)
            {
                return preferred;
            }

            // Wrong on purpose: the difficulty knob is being wrong at the fork, not slow to answer it.
            int other = UnityEngine.Random.Range(0, count - 1);
            return other >= preferred ? other + 1 : other;
        }

        private static int Prefer(in BoardSnapshot world, int count)
        {
            bool chasingShrine = world.CanAffordShrine
                                 || world.SelfCoins >= BoardSession.ShrinePrice - ShrineWithinReach;

            int best = 0;
            int bestSteps = StepsToShrine(in world, 0);

            for (int i = 1; i < count; i++)
            {
                int steps = StepsToShrine(in world, i);
                bool better = chasingShrine ? Nearer(steps, bestSteps) : Further(steps, bestSteps);

                if (better)
                {
                    best = i;
                    bestSteps = steps;
                }
            }

            return best;
        }

        private static int StepsToShrine(in BoardSnapshot world, int choice)
        {
            int[] steps = world.ChoiceStepsToShrine;
            return choice < steps.Length ? steps[choice] : -1;
        }

        private static bool Nearer(int steps, int best)
        {
            if (steps < 0)
            {
                return false;
            }

            return best < 0 || steps < best;
        }

        private static bool Further(int steps, int best)
        {
            if (best < 0)
            {
                return false;
            }

            return steps < 0 || steps > best;
        }

        private void Forget()
        {
            dwell = 0f;
            ForgetStop();
            ForgetFork();
        }

        private void ForgetStop()
        {
            stopDwell = 0f;
            stopDelay = 0f;
        }

        private void ForgetFork()
        {
            choiceDwell = 0f;
            nudgeElapsed = 0f;
            committed = Undecided;
        }
    }
}
