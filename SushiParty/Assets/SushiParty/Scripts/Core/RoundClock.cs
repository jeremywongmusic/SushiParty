namespace SushiParty.Core
{
    [System.Flags]
    public enum RoundEvent
    {
        None = 0,

        BriefingShown = 1 << 0,

        BriefingHidden = 1 << 1,

        CountdownStepped = 1 << 2,

        GoShown = 1 << 3,

        BannerCleared = 1 << 4,

        ClockChanged = 1 << 5,

        ClockWarning = 1 << 6,

        PhaseChanged = 1 << 7,

        PlayBegan = 1 << 8,

        PlayTicked = 1 << 9,

        TimeExpired = 1 << 10,

        Resolved = 1 << 11,

        SettleTicked = 1 << 12,

        ResultsShown = 1 << 13,

        RetryRequested = 1 << 14,
    }

    public readonly struct RoundTick
    {
        public readonly RoundEvent Events;
        public readonly MinigamePhase Phase;
        public readonly float TimeRemaining;
        public readonly float DeltaTime;
        public readonly int CountdownValue;
        public readonly MinigameOutcome Outcome;

        public RoundTick(
            RoundEvent events,
            MinigamePhase phase,
            float timeRemaining,
            float deltaTime,
            int countdownValue,
            MinigameOutcome outcome)
        {
            Events = events;
            Phase = phase;
            TimeRemaining = timeRemaining;
            DeltaTime = deltaTime;
            CountdownValue = countdownValue;
            Outcome = outcome;
        }

        public bool Has(RoundEvent inquiry)
        {
            return (Events & inquiry) == inquiry && inquiry != RoundEvent.None;
        }
    }

    public sealed class RoundClock
    {
        public const float DefaultBriefingAutoAdvance = 8f;
        public const float DefaultCountdownStep = 0.8f;
        public const float DefaultSettleDuration = 2.2f;
        public const float DefaultClockWarningThreshold = 5f;
        private const int CountdownFrom = 3;
        private readonly bool usesRoundStructure;
        private readonly float briefingAutoAdvance;
        private readonly float countdownStep;
        private readonly float settleDuration;
        private readonly float clockWarningThreshold;
        private float phaseTimer;
        private int countdownValue;

        public RoundClock(
            float timeLimit,
            bool usesRoundStructure = true,
            float briefingAutoAdvance = DefaultBriefingAutoAdvance,
            float countdownStep = DefaultCountdownStep,
            float settleDuration = DefaultSettleDuration,
            float clockWarningThreshold = DefaultClockWarningThreshold)
        {
            this.usesRoundStructure = usesRoundStructure;
            this.briefingAutoAdvance = briefingAutoAdvance;
            this.countdownStep = countdownStep;
            this.settleDuration = settleDuration;
            this.clockWarningThreshold = clockWarningThreshold;

            TimeRemaining = timeLimit;
        }

        public MinigamePhase Phase { get; private set; } = MinigamePhase.Idle;

        public float TimeRemaining { get; private set; }

        public MinigameOutcome? Outcome { get; private set; }

        public bool TimeExpired => usesRoundStructure && Phase == MinigamePhase.Playing && TimeRemaining <= 0f;

        public RoundTick Begin()
        {
            if (Phase != MinigamePhase.Idle)
            {
                return Report(RoundEvent.None);
            }

            if (!usesRoundStructure)
            {
                EnterPhase(MinigamePhase.Playing);
                return Report(RoundEvent.PhaseChanged | RoundEvent.PlayBegan);
            }

            EnterPhase(MinigamePhase.Briefing);
            return Report(RoundEvent.BriefingShown | RoundEvent.PhaseChanged);
        }

        public RoundTick Advance(float deltaTime, bool primaryPressed)
        {
            switch (Phase)
            {
                case MinigamePhase.Briefing:
                    return AdvanceBriefing(deltaTime, primaryPressed);
                case MinigamePhase.Countdown:
                    return AdvanceCountdown(deltaTime);
                case MinigamePhase.Playing:
                    return AdvancePlaying(deltaTime);
                case MinigamePhase.Settling:
                    return AdvanceSettling(deltaTime);
                case MinigamePhase.Finished:
                    return AdvanceFinished(primaryPressed);
                default:
                    return Report(RoundEvent.None);
            }
        }

        public RoundTick Resolve(MinigameOutcome result)
        {
            if (Phase == MinigamePhase.Settling || Phase == MinigamePhase.Finished)
            {
                return Report(RoundEvent.None);
            }

            Outcome = result;
            EnterPhase(MinigamePhase.Settling);
            return Report(RoundEvent.Resolved | RoundEvent.PhaseChanged);
        }

        public RoundTick AddTime(float seconds)
        {
            TimeRemaining += seconds;
            return Report(RoundEvent.ClockChanged);
        }

        private RoundTick AdvanceBriefing(float deltaTime, bool primaryPressed)
        {
            phaseTimer += deltaTime;

            if (phaseTimer < briefingAutoAdvance && !primaryPressed)
            {
                return Report(RoundEvent.None);
            }

            countdownValue = CountdownFrom;
            EnterPhase(MinigamePhase.Countdown);
            return Report(
                RoundEvent.BriefingHidden | RoundEvent.CountdownStepped | RoundEvent.PhaseChanged,
                countdownValue: countdownValue);
        }

        private RoundTick AdvanceCountdown(float deltaTime)
        {
            phaseTimer += deltaTime;
            if (phaseTimer < countdownStep)
            {
                return Report(RoundEvent.None);
            }

            phaseTimer = 0f;
            countdownValue--;

            if (countdownValue > 0)
            {
                return Report(RoundEvent.CountdownStepped, countdownValue: countdownValue);
            }

            if (countdownValue == 0)
            {
                return Report(RoundEvent.GoShown);
            }

            EnterPhase(MinigamePhase.Playing);
            return Report(RoundEvent.BannerCleared | RoundEvent.PhaseChanged | RoundEvent.PlayBegan);
        }

        private RoundTick AdvancePlaying(float deltaTime)
        {
            RoundEvent events = RoundEvent.PlayTicked;

            if (usesRoundStructure)
            {
                float previous = TimeRemaining;
                TimeRemaining = System.Math.Max(0f, previous - deltaTime);
                events |= RoundEvent.ClockChanged;

                if (previous > clockWarningThreshold && TimeRemaining <= clockWarningThreshold)
                {
                    events |= RoundEvent.ClockWarning;
                }
            }

            if (TimeExpired)
            {
                events |= RoundEvent.TimeExpired;
            }

            return Report(events, deltaTime);
        }

        private RoundTick AdvanceSettling(float deltaTime)
        {
            phaseTimer += deltaTime;

            if (phaseTimer < settleDuration)
            {
                return Report(RoundEvent.SettleTicked, deltaTime);
            }

            EnterPhase(MinigamePhase.Finished);
            return Report(
                RoundEvent.SettleTicked | RoundEvent.BannerCleared | RoundEvent.ResultsShown | RoundEvent.PhaseChanged,
                deltaTime);
        }

        private RoundTick AdvanceFinished(bool primaryPressed)
        {
            return Report(primaryPressed ? RoundEvent.RetryRequested : RoundEvent.None);
        }

        private void EnterPhase(MinigamePhase next)
        {
            Phase = next;
            phaseTimer = 0f;
        }

        private RoundTick Report(RoundEvent events, float deltaTime = 0f, int countdownValue = 0)
        {
            return new RoundTick(
                events,
                Phase,
                TimeRemaining,
                deltaTime,
                countdownValue,
                Outcome ?? default(MinigameOutcome));
        }
    }
}
