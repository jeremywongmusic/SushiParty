using SushiParty.Audio;
using SushiParty.InputLayer;
using SushiParty.Menu;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SushiParty.Core
{
    public abstract class MinigameController : MonoBehaviour
    {
        private static readonly HumanParticipantInput[] SpectatorReaders =
        {
            new HumanParticipantInput(InputDeviceChoice.KeyboardLeft),
            new HumanParticipantInput(InputDeviceChoice.KeyboardRight),
            new HumanParticipantInput(InputDeviceChoice.Gamepad1),
            new HumanParticipantInput(InputDeviceChoice.Gamepad2),
        };

        private RoundClock clock;
        private bool initialized;
        private AudioHandle roundMusic;
        private bool roundMusicRunning;

        public abstract MinigameId Id { get; }

        protected MinigameContext Context { get; private set; }
        protected MinigameDefinition Definition { get; private set; }
        protected MinigameHud Hud { get; private set; }

        public MinigamePhase Phase => clock == null ? MinigamePhase.Idle : clock.Phase;
        protected float TimeRemaining => clock == null ? 0f : clock.TimeRemaining;
        protected Participant P1 => Context.One;
        protected Participant P2 => Context.Two;

        public void Initialize(MinigameContext context)
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Context = context;
            Definition = context.Definition;
            clock = new RoundClock(Definition.TimeLimit, UsesRoundStructure);

            InstallCpuBrains();

            Hud = MinigameHud.Create(Definition, Context);
            Hud.SetTimer(TimeRemaining);

            GameAudio.SetGlobalLabel(Global.Minigame, Id.ToString());

            OnPrepare();

            if (!UsesRoundStructure)
            {
                Hud.SetRoundChromeVisible(false);
            }

            Apply(clock.Begin());
        }

        private void Start()
        {
            if (!initialized)
            {
                Initialize(MinigameContext.CreateDirectPlay(Id));
            }
        }

        private void InstallCpuBrains()
        {
            foreach (Participant participant in Context.Participants)
            {
                if (!participant.IsCpu)
                {
                    continue;
                }

                IParticipantInput brain = CreateCpuBrain(participant);
                if (brain == null)
                {
                    Debug.LogWarning(
                        $"{GetType().Name} returned no CPU brain for {participant.DisplayName}; " +
                        "that seat will stand still. Implement CreateCpuBrain.",
                        this);
                    continue;
                }

                participant.Input = brain;
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (PauseMenuController.IsPaused || SettingsMenuController.IsOpen)
            {
                return;
            }

            float dt = Time.deltaTime;
            Context.TickInputs(dt);
            PublishAudioState();

#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                StartCoroutine(ThumbnailCapture.CaptureRoutine(Definition.SceneName));
            }
#endif

            MinigamePhase phase = Phase;

            RoundTick tick = clock.Advance(dt, AdvanceRequested(phase));
            Apply(tick);

            if (tick.Has(RoundEvent.PlayTicked))
            {
                OnPlay(dt);

                if (Phase != MinigamePhase.Playing)
                {
                    return;
                }

                if (clock.TimeExpired)
                {
                    Resolve(OnTimeUp());
                }

                return;
            }

            if (tick.Has(RoundEvent.RetryRequested))
            {
                if (ReturnedToBoard())
                {
                    return;
                }

                Retry();
                return;
            }

        }

        private void Apply(RoundTick tick)
        {
            if (tick.Has(RoundEvent.BriefingHidden))
            {
                Hud.ShowBriefing(false);
            }

            if (tick.Has(RoundEvent.SettleTicked))
            {
                OnSettle(tick.DeltaTime, tick.Outcome);
            }

            if (tick.Has(RoundEvent.BannerCleared))
            {
                Hud.HideBanner();
            }

            if (tick.Has(RoundEvent.CountdownStepped))
            {
                Hud.ShowBanner(tick.CountdownValue.ToString(), Definition.Accent);
                GameAudio.Play(Sfx.CountdownTick, Sfx.StepParameter, tick.CountdownValue);
            }

            if (tick.Has(RoundEvent.GoShown))
            {
                Hud.ShowBanner("GO!", new Color(0.45f, 0.92f, 0.55f));
                GameAudio.Play(Sfx.Go);
            }

            if (tick.Has(RoundEvent.Resolved))
            {
                Hud.ShowBanner(
                    tick.Outcome.Won ? "SUCCESS!" : "TIME'S UP",
                    tick.Outcome.Won ? new Color(0.45f, 0.92f, 0.55f) : new Color(1f, 0.46f, 0.40f));

                GameAudio.Play(tick.Outcome.Won ? Sfx.Win : Sfx.Lose);

                EndRoundMusic();

                if (MinigameFlow.Instance != null)
                {
                    MinigameFlow.Instance.RecordOutcome(Id, tick.Outcome);
                }
            }

            if (tick.Has(RoundEvent.ResultsShown))
            {
                Hud.ShowResults(tick.Outcome, canReturnToMenu: true, returnsToBoard: InBoardGame());
                GameAudio.Play(Sfx.Results);
            }

            if (tick.Has(RoundEvent.ClockChanged))
            {
                Hud.SetTimer(tick.TimeRemaining);
            }

            if (tick.Has(RoundEvent.ClockWarning))
            {
                GameAudio.Play(Sfx.ClockWarning);
            }

            if (tick.Has(RoundEvent.BriefingShown))
            {
                Hud.ShowBriefing(true);
                GameAudio.Play(Sfx.BriefingShow);
                StartRoundMusic();
            }

            if (tick.Has(RoundEvent.PhaseChanged))
            {
                GameAudio.SetGlobalLabel(Global.RoundPhase, tick.Phase.ToString());
            }

            if (tick.Has(RoundEvent.PlayBegan))
            {
                OnBegin();
            }

            if (tick.Has(RoundEvent.Resolved))
            {
                OnConclude(tick.Outcome);
            }
        }

        private void PublishAudioState()
        {
            if (UsesRoundStructure && Definition.TimeLimit > 0f)
            {
                GameAudio.SetGlobal(Global.TimeRemaining, TimeRemaining, 0.05f);
                GameAudio.SetGlobal(
                    Global.RoundProgress,
                    Mathf.Clamp01(1f - TimeRemaining / Definition.TimeLimit));
            }

            MinigameTelemetry telemetry = SampleTelemetry().Clamped();
            GameAudio.SetGlobal(Global.PlayerMotion, telemetry.PlayerMotion, 0.02f);
            GameAudio.SetGlobal(Global.ChefMotion, telemetry.ChefMotion, 0.02f);
            GameAudio.SetGlobal(Global.Objective, telemetry.Objective);
        }

        private void StartRoundMusic()
        {
            if (roundMusicRunning)
            {
                return;
            }

            roundMusicRunning = true;

            GameAudio.Play(Sfx.MusicRoundStart);
            roundMusic = GameAudio.Loop(Sfx.MusicFor(Id));
        }

        private void EndRoundMusic()
        {
            if (!roundMusicRunning)
            {
                return;
            }

            GameAudio.Play(Sfx.MusicRoundStop);
            StopRoundMusic();
        }

        private void StopRoundMusic()
        {
            roundMusicRunning = false;
            GameAudio.Stop(ref roundMusic);
        }

        private void OnDisable()
        {
            StopRoundMusic();
        }

        protected void AddTime(float seconds)
        {
            Apply(clock.AddTime(seconds));
        }

        protected void Win(string detail = null)
        {
            Resolve(MinigameOutcome.Win(TimeRemaining, detail));
        }

        protected void Lose(string detail = null)
        {
            Resolve(MinigameOutcome.Lose(detail));
        }

        private void Resolve(MinigameOutcome result)
        {
            Apply(clock.Resolve(result));
        }

        internal bool Abandon()
        {
            if (Phase != MinigamePhase.Playing)
            {
                return false;
            }

            MinigameOutcome abandoned = MinigameOutcome.Abandon();

            EndRoundMusic();

            OnConclude(abandoned);
            ReturnToMenu();
            return true;
        }

        protected void Retry()
        {
            if (MinigameFlow.Instance != null && !Context.IsDirectPlay)
            {
                MinigameFlow.Instance.Launch(Context.Setup);
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        protected void ReturnToMenu()
        {
            GameAudio.Play(Sfx.MenuBack);

            if (MinigameFlow.Instance != null)
            {
                MinigameFlow.Instance.ReturnToMenu();
                return;
            }

            Debug.Log("[SushiParty] No flow service present (direct play) — staying in this scene.", this);
        }

        private static bool InBoardGame()
        {
            MinigameFlow flow = MinigameFlow.Instance;
            return flow != null && flow.InBoardGame;
        }

        private static bool ReturnedToBoard()
        {
            MinigameFlow flow = MinigameFlow.Instance;
            if (flow == null || !flow.InBoardGame)
            {
                return false;
            }

            GameAudio.Play(Sfx.MenuBack);
            flow.ReturnToBoard();
            return true;
        }

        protected bool AnyHumanPressed(MinigameAction action)
        {
            foreach (Participant participant in Context.Participants)
            {
                if (!participant.IsCpu && participant.Input.WasPressed(action))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AdvanceRequested(MinigamePhase phase)
        {
            if (AnyHumanPressed(MinigameAction.Primary))
            {
                return true;
            }

            if (!SpectatorMayAdvance(phase, Context))
            {
                return false;
            }

            for (int i = 0; i < SpectatorReaders.Length; i++)
            {
                if (SpectatorReaders[i].WasPressed(MinigameAction.Primary))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SpectatorMayAdvance(MinigamePhase phase, MinigameContext context)
        {
            if (phase != MinigamePhase.Finished || context == null)
            {
                return false;
            }

            foreach (Participant participant in context.Participants)
            {
                if (!participant.IsCpu)
                {
                    return false;
                }
            }

            return true;
        }

        protected abstract void OnPrepare();
        protected abstract void OnPlay(float deltaTime);
        protected abstract IParticipantInput CreateCpuBrain(Participant participant);

        protected virtual void OnBegin()
        {
        }

        protected virtual bool UsesRoundStructure => true;

        protected virtual MinigameOutcome OnTimeUp()
        {
            return MinigameOutcome.Lose();
        }

        protected virtual void OnConclude(MinigameOutcome result)
        {
        }

        protected virtual void OnSettle(float deltaTime, MinigameOutcome result)
        {
        }

        protected virtual MinigameTelemetry SampleTelemetry()
        {
            return MinigameTelemetry.None;
        }
    }
}
