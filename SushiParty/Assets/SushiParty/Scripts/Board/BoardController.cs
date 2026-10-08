using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Menu;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Board
{
    public sealed class BoardController : MonoBehaviour
    {
        private const float RoundIntroHold = 0.9f;
        private const float ResolveHold = 1.1f;
        private const float RoundEndHold = 0.9f;
        private const float UnattendedHold = 2.6f;
        private const float RollHold = 0.18f;
        private const float HeadBangHeight = 0.72f;
        private const float HeadBangDuration = 0.24f;
        private const float HeadBangContact = 0.5f;
        private const float CatchHeight = 0.44f;
        private const float CatchDuration = 0.20f;
        private const float StepDuration = 0.32f;
        private const float StepHopHeight = 0.55f;
        private const float ChoiceRepeatDelay = 0.32f;
        private const float ChoiceRepeatRate = 0.14f;
        private const float ChoiceDeadzone = 0.4f;
        private const string RoundIntroHint = "Press to start the round";
        private const string ResolveHint = "Press to continue";
        private const string RoundEndHint = "Press for the minigame";
        private static readonly Color CoinOrange = new Color(0.98f, 0.55f, 0.22f);
        private static readonly Color ShrineGold = new Color(0.98f, 0.80f, 0.28f);
        private static readonly Color OceanCyan = new Color(0.25f, 0.72f, 0.82f);

        private enum BoardPhase
        {
            RoundIntro,
            AwaitingRoll,
            HeadBang,
            Rolling,
            Catch,
            Moving,
            Choosing,
            Resolving,
            RoundEnd,
            Finished,
        }

        private enum Reaction
        {
            None,
            Delight,
            Dismay,
            Dizzy,
            Stunned,
        }

        private readonly IDie die = new RandomDie();

        private static readonly HumanParticipantInput[] SpectatorReaders =
        {
            new HumanParticipantInput(InputDeviceChoice.KeyboardLeft),
            new HumanParticipantInput(InputDeviceChoice.KeyboardRight),
            new HumanParticipantInput(InputDeviceChoice.Gamepad1),
            new HumanParticipantInput(InputDeviceChoice.Gamepad2),
        };

        private readonly ContinueGate introGate = new ContinueGate(RoundIntroHold, UnattendedHold);
        private readonly ContinueGate resolveGate = new ContinueGate(ResolveHold, UnattendedHold);
        private readonly ContinueGate roundEndGate = new ContinueGate(RoundEndHold, UnattendedHold);
        private readonly ContinueGate rollGate = new ContinueGate(RollHold, UnattendedHold);
        private BoardSession session;

        private readonly BoardAudioReporter standaloneAudioReporter =
            new BoardAudioReporter(new GameAudioGlobals());

        private BoardAudioReporter AudioReporter => MinigameFlow.Instance != null
            ? MinigameFlow.Instance.BoardAudio
            : standaloneAudioReporter;
        private BoardView view;
        private BoardHud hud;
        private BoardCameraRig cameraRig;
        private BoardDiceBlock diceBlock;
        private Participant[] participants;
        private IParticipantInput chefInput;
        private BoardPhase phase;
        private float phaseTimer;
        private bool initialized;
        private bool handedOff;
        private TurnPlan plan;
        private bool sittingOut;
        private Transform hopper;
        private Vector3 hopHome;
        private bool banged;
        private int caughtFace;
        private Transform walker;
        private Vector3 stepFrom;
        private Vector3 stepTo;
        private float stepTimer;
        private int step;
        private int walkerSpace;
        private bool walkerAirborne;
        private int choiceFrom;
        private int[] choiceSpaces;
        private int[] choiceShrineSteps;
        private int[] choiceLoops;
        private string[] choiceLabels;
        private string choicePrompt;
        private int choiceIndex;
        private int choiceDirection;
        private float choiceCooldown;
        private readonly int[] shrineSteps = new int[3];
        private readonly int[] shrineStepsFrom = { -1, -1, -1 };
        private readonly int[] shrineStepsTo = { -1, -1, -1 };

        private BoardSeat spotlight;
        private bool spotlit;
        private Reaction reaction;
        private BoardSeat reactionSeat;

        public void Adopt(BoardSession existing)
        {
            if (initialized || session != null || existing == null)
            {
                return;
            }

            session = existing;
        }

        private void Start()
        {
            if (session == null && MinigameFlow.Instance != null)
            {
                Adopt(MinigameFlow.Instance.BoardSession);
            }

            if (session == null)
            {
                session = new BoardSession(BoardLayout.CreateDefault(), die);
            }

            MatchSetup setup = MinigameFlow.Instance != null ? MinigameFlow.Instance.Setup : new MatchSetup();

            if (MinigameFlow.Instance != null)
            {
                MinigameFlow.Instance.AdoptBoardSession(session, setup);
            }
            participants = setup.CreateParticipants();
            InstallCpuBrains();

            chefInput = new BoardBrain(() => Snapshot(BoardSeat.Chef), BoardSeat.Chef, setup.ChefSkill);

            view = BoardView.Create(transform, session.Layout);

            cameraRig = BoardCameraRig.Create(Camera.main);

            diceBlock = BoardDiceBlock.Create(transform);

            hud = BoardHud.Create(session);
            SyncTokens();
            SyncStunned();

            initialized = true;
            EnterRoundIntro();
        }

        private void InstallCpuBrains()
        {
            for (int i = 0; i < participants.Length; i++)
            {
                Participant participant = participants[i];
                if (!participant.IsCpu)
                {
                    continue;
                }

                BoardSeat seat = participant.Slot == ParticipantSlot.One ? BoardSeat.One : BoardSeat.Two;
                participant.Input = new BoardBrain(() => Snapshot(seat), seat, participant.Skill);
            }
        }

        private void SyncTokens()
        {
            IReadOnlyList<BoardToken> tokens = session.Tokens;
            for (int i = 0; i < tokens.Count; i++)
            {
                view.PlaceToken(tokens[i].Seat, tokens[i].Space);
            }
        }

        private void SyncStunned()
        {
            IReadOnlyList<BoardToken> tokens = session.Tokens;
            for (int i = 0; i < tokens.Count; i++)
            {
                view.AnimationFor(tokens[i].Seat).SetStunned(tokens[i].SkipTurns > 0);
            }
        }

        private void Update()
        {
            if (!initialized || handedOff)
            {
                return;
            }

            if (PauseMenuController.IsPaused || SettingsMenuController.IsOpen)
            {
                return;
            }

            float dt = Time.deltaTime;
            TickInputs(dt);

            AudioReporter.Publish(session, MinigameFlow.Instance != null ? MinigameFlow.Instance.Setup : null);

            phaseTimer += dt;

            switch (phase)
            {
                case BoardPhase.RoundIntro:
                    UpdateRoundIntro(dt);
                    break;

                case BoardPhase.AwaitingRoll:
                    UpdateAwaitingRoll();
                    break;

                case BoardPhase.HeadBang:
                    UpdateHeadBang();
                    break;

                case BoardPhase.Rolling:
                    UpdateRolling(dt);
                    break;

                case BoardPhase.Catch:
                    UpdateCatch();
                    break;

                case BoardPhase.Moving:
                    UpdateMoving(dt);
                    break;

                case BoardPhase.Choosing:
                    UpdateChoosing(dt);
                    break;

                case BoardPhase.Resolving:
                    UpdateResolving(dt);
                    break;

                case BoardPhase.RoundEnd:
                    UpdateRoundEnd(dt);
                    break;

                case BoardPhase.Finished:
                    UpdateFinished();
                    break;
            }

            diceBlock.Spin(dt);
        }

        private void TickInputs(float deltaTime)
        {
            for (int i = 0; i < participants.Length; i++)
            {
                participants[i].Input.Tick(deltaTime);
            }

            chefInput.Tick(deltaTime);
        }

        private IParticipantInput InputFor(BoardSeat seat)
        {
            return seat == BoardSeat.Chef ? chefInput : participants[(int)seat].Input;
        }

        private bool StillHolding(ContinueGate gate, float deltaTime, string hint)
        {
            bool asking = gate.AwaitingPress;

            if (gate.Tick(deltaTime, ContinuePressed(), AnyoneWatching()))
            {
                hud.HideContinuePrompt();
                return false;
            }

            if (gate.AwaitingPress && !asking)
            {
                hud.ShowContinuePrompt(hint);
            }

            return true;
        }

        private bool ContinuePressed()
        {
            bool watched = false;

            for (int i = 0; i < participants.Length; i++)
            {
                Participant participant = participants[i];
                if (participant.IsCpu)
                {
                    continue;
                }

                watched = true;
                if (participant.Input.WasPressed(MinigameAction.Primary))
                {
                    return true;
                }
            }

            if (watched)
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

        private bool AnyoneWatching()
        {
            for (int i = 0; i < participants.Length; i++)
            {
                if (!participants[i].IsCpu)
                {
                    return true;
                }
            }

            return false;
        }

        private BoardSnapshot Snapshot(BoardSeat seat)
        {
            BoardToken token = session.TokenFor(seat);
            int shrine = session.ShrineSpace;

            bool choosing = phase == BoardPhase.Choosing && choiceSpaces != null;

            return new BoardSnapshot(
                session.Turn,
                phase == BoardPhase.AwaitingRoll,
                session.Round,
                session.TotalRounds,
                token.Space,
                token.Coins,
                token.GoldenCoins,
                shrine,
                shrine < 0 ? 0 : StepsToShrine(seat, token.Space, shrine),
                choosing,
                choosing ? choiceSpaces : null,
                choiceIndex,
                choosing ? choiceShrineSteps : null,

                awaitingStop: phase == BoardPhase.Rolling);
        }

        private int StepsToShrine(BoardSeat seat, int from, int shrine)
        {
            int index = (int)seat;

            if (shrineStepsFrom[index] != from || shrineStepsTo[index] != shrine)
            {
                shrineStepsFrom[index] = from;
                shrineStepsTo[index] = shrine;
                shrineSteps[index] = session.Layout.StepsBetween(from, shrine);
            }

            return shrineSteps[index];
        }

        private void EnterRoundIntro()
        {
            ReleaseTurn();

            hud.SetRound(session.Round, session.TotalRounds);
            hud.RefreshTotals(session);
            hud.HidePrompt();
            hud.ShowBanner($"ROUND {session.Round}", UiKit.Ink);

            introGate.Open();
            EnterPhase(BoardPhase.RoundIntro);
        }

        private void UpdateRoundIntro(float deltaTime)
        {
            if (StillHolding(introGate, deltaTime, RoundIntroHint))
            {
                return;
            }

            hud.HideBanner();
            BeginNextTurn();
        }

        private void BeginNextTurn()
        {
            SyncStunned();

            if (session.Finished)
            {
                EnterFinished();
                return;
            }

            if (session.AwaitingMinigame)
            {
                EnterRoundEnd();
                return;
            }

            EnterAwaitingRoll();
        }

        private void EnterAwaitingRoll()
        {
            BoardSeat seat = session.Turn;
            BoardToken token = session.TokenFor(seat);
            Transform standing = view.TokenFor(seat);

            hud.SetTurn(seat, token.DisplayName, ColorFor(seat));
            hud.HideBanner();

            sittingOut = token.SkipTurns > 0;

            hud.ShowPrompt($"{token.DisplayName} — press to roll");

            Spotlight(seat);

            cameraRig.Cut(BoardShot.Turn, standing);

            if (sittingOut)
            {
                diceBlock.Dismiss();
            }
            else
            {
                diceBlock.Present(standing);
            }

            GameAudio.Play(Sfx.BoardTurnPass);

            EnterPhase(BoardPhase.AwaitingRoll);
        }

        private void UpdateAwaitingRoll()
        {
            if (!InputFor(session.Turn).WasPressed(MinigameAction.Primary))
            {
                return;
            }

            hud.HidePrompt();

            if (sittingOut)
            {
                plan = session.BeginTurn();
                EnterResolving();
                return;
            }

            EnterHeadBang();
        }

        private void EnterHeadBang()
        {
            hopper = view.TokenFor(session.Turn);
            hopHome = hopper.position;
            banged = false;

            CharacterAnimation animation = view.AnimationFor(session.Turn);

            animation.SetSpeed(0f);

            animation.Roll();

            EnterPhase(BoardPhase.HeadBang);
        }

        private void UpdateHeadBang()
        {
            float t = Mathf.Clamp01(phaseTimer / HeadBangDuration);
            hopper.position = Arc(hopHome, hopHome, t, HeadBangHeight);

            if (!banged && t >= HeadBangContact)
            {
                banged = true;

                diceBlock.Launch();
            }

            if (t < 1f)
            {
                return;
            }

            EnterRolling();
        }

        private void EnterRolling()
        {
            hud.ShowPrompt($"{session.TokenFor(session.Turn).DisplayName} — press to stop it");

            rollGate.Open();
            EnterPhase(BoardPhase.Rolling);
        }

        private void UpdateRolling(float deltaTime)
        {
            bool caught = InputFor(session.Turn).WasPressed(MinigameAction.Primary);

            if (!rollGate.Tick(deltaTime, caught, AnyoneWatching()))
            {
                return;
            }

            EnterCatch();
        }

        private void EnterCatch()
        {
            caughtFace = diceBlock.Stop();
            hud.HidePrompt();

            hopper = view.TokenFor(session.Turn);
            hopHome = hopper.position;

            CharacterAnimation animation = view.AnimationFor(session.Turn);
            animation.SetSpeed(0f);

            animation.Roll();

            EnterPhase(BoardPhase.Catch);
        }

        private void UpdateCatch()
        {
            float t = Mathf.Clamp01(phaseTimer / CatchDuration);
            hopper.position = Arc(hopHome, hopHome, t, CatchHeight);

            if (t < 1f)
            {
                return;
            }

            BeginWalk();
        }

        private void BeginWalk()
        {
            plan = session.BeginTurn(caughtFace);

            if (plan.Skipped)
            {
                EnterResolving();
                return;
            }

            EnterMoving();
        }

        private static Vector3 Arc(Vector3 from, Vector3 to, float t, float height)
        {
            Vector3 position = Vector3.Lerp(from, to, t);
            position.y += Mathf.Sin(t * Mathf.PI) * height;
            return position;
        }

        private void EnterMoving()
        {
            ClearSpotlight();

            walker = view.TokenFor(plan.Seat);
            walkerSpace = plan.From;
            step = 0;

            EnterPhase(BoardPhase.Moving);
            ContinueWalk();
        }

        private void ContinueWalk()
        {
            if (session.StepsRemaining <= 0)
            {
                FinishWalk();
                return;
            }

            if (session.AwaitingChoice)
            {
                EnterChoosing();
                return;
            }

            EnterPhase(BoardPhase.Moving);

            cameraRig.Cut(BoardShot.Map);

            BeginStep();
        }

        private void BeginStep()
        {
            step++;
            stepTimer = 0f;

            stepFrom = step == 1 ? walker.position : stepTo;

            session.StepOnce();
            walkerSpace = session.TokenFor(plan.Seat).Space;
            stepTo = view.WorldPositionOf(walkerSpace);

            Vector3 travel = stepTo - stepFrom;
            travel.y = 0f;
            if (travel.sqrMagnitude > 0.0001f)
            {
                walker.rotation = Quaternion.LookRotation(travel, Vector3.up);
            }

            GameAudio.Play(Sfx.BoardTokenStep, Sfx.StepParameter, step);
            PushOffWalker();
        }

        private void PushOffWalker()
        {
            CharacterAnimation animation = view.AnimationFor(plan.Seat);

            animation.SetSpeed(1f);

            if (walkerAirborne)
            {
                return;
            }

            walkerAirborne = true;
            animation.SetGrounded(false);
            animation.Jump();
        }

        private void SettleWalker()
        {
            if (!walkerAirborne)
            {
                return;
            }

            walkerAirborne = false;

            CharacterAnimation animation = view.AnimationFor(plan.Seat);
            animation.SetSpeed(0f);
            animation.SetGrounded(true);
            animation.Land();
        }

        private void UpdateMoving(float deltaTime)
        {
            stepTimer += deltaTime;
            float t = Mathf.Clamp01(stepTimer / StepDuration);

            walker.position = Arc(stepFrom, stepTo, t, StepHopHeight);

            if (t < 1f)
            {
                return;
            }

            ContinueWalk();
        }

        private void FinishWalk()
        {
            SettleWalker();
            view.PlaceToken(plan.Seat, walkerSpace);
            EnterResolving();
        }

        private void EnterChoosing()
        {
            SettleWalker();

            cameraRig.Cut(BoardShot.Fork, walker);

            choiceFrom = walkerSpace;
            DescribeChoices(choiceFrom, session.Choices);

            choiceIndex = 0;
            choiceDirection = 0;
            choiceCooldown = 0f;

            int left = session.StepsRemaining;
            choicePrompt =
                $"{session.TokenFor(plan.Seat).DisplayName} — {left} step{(left == 1 ? string.Empty : "s")} left."
                + "   Left and right to look, press to take it.";

            RefreshChoice();
            EnterPhase(BoardPhase.Choosing);
        }

        private void UpdateChoosing(float deltaTime)
        {
            IParticipantInput input = InputFor(plan.Seat);

            Shift(Direction(input.Move.x), deltaTime);

            if (input.WasPressed(MinigameAction.Primary))
            {
                ConfirmChoice();
            }
        }

        private static int Direction(float x)
        {
            if (x > ChoiceDeadzone)
            {
                return 1;
            }

            return x < -ChoiceDeadzone ? -1 : 0;
        }

        private void Shift(int direction, float deltaTime)
        {
            if (direction == 0)
            {
                choiceDirection = 0;
                choiceCooldown = 0f;
                return;
            }

            bool turned = direction != choiceDirection;
            choiceCooldown -= deltaTime;

            if (!turned && choiceCooldown > 0f)
            {
                return;
            }

            choiceDirection = direction;
            choiceCooldown = turned ? ChoiceRepeatDelay : ChoiceRepeatRate;

            int next = Mathf.Clamp(choiceIndex + direction, 0, choiceSpaces.Length - 1);
            if (next == choiceIndex)
            {
                return;
            }

            choiceIndex = next;
            RefreshChoice();

            GameAudio.Play(Sfx.MenuMove);
        }

        private void ConfirmChoice()
        {
            session.ChooseExit(choiceSpaces[choiceIndex]);

            EndChoice();
            GameAudio.Play(Sfx.MenuConfirm);

            ContinueWalk();
        }

        private void RefreshChoice()
        {
            for (int i = 0; i < choiceSpaces.Length; i++)
            {
                view.HighlightExit(choiceFrom, choiceSpaces[i], i == choiceIndex);
            }

            hud.ShowChoice(choicePrompt, choiceLabels, choiceIndex);
        }

        private void EndChoice()
        {
            view.ClearExitHighlights();
            hud.HideChoice();

            choiceSpaces = null;
            choiceShrineSteps = null;
            choiceLoops = null;
            choiceLabels = null;
            choicePrompt = null;
            choiceIndex = 0;
            choiceDirection = 0;
            choiceCooldown = 0f;
        }

        private void DescribeChoices(int at, IReadOnlyList<int> options)
        {
            int count = options.Count;

            choiceSpaces = new int[count];
            choiceShrineSteps = new int[count];
            choiceLoops = new int[count];
            choiceLabels = new string[count];

            BoardLayout layout = session.Layout;
            int shrine = session.ShrineSpace;

            int longest = int.MinValue;
            int shortest = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int to = options[i];
                choiceSpaces[i] = to;
                choiceShrineSteps[i] = shrine < 0 ? -1 : layout.StepsBetween(to, shrine);

                int loop = layout.StepsBetween(to, at);
                choiceLoops[i] = loop < 0 ? int.MaxValue : loop;

                longest = Mathf.Max(longest, choiceLoops[i]);
                shortest = Mathf.Min(shortest, choiceLoops[i]);
            }

            for (int i = 0; i < count; i++)
            {
                choiceLabels[i] = $"<b>{Headline(choiceLoops[i], longest, shortest)}</b>\n{Detail(i, layout, shrine)}";
            }
        }

        private static string Headline(int loop, int longest, int shortest)
        {
            if (longest == shortest)
            {
                return "EITHER WAY";
            }

            if (loop >= longest)
            {
                return "THE LONG WAY";
            }

            return loop <= shortest ? "THE SHORTCUT" : "THE MIDDLE WAY";
        }

        private string Detail(int choice, BoardLayout layout, int shrine)
        {
            string next = DescribeNext(layout.KindAt(choiceSpaces[choice]));

            if (shrine < 0)
            {
                return next;
            }

            int steps = choiceShrineSteps[choice];

            return steps < 0 ? $"no shrine this way  ·  {next}" : $"shrine in {steps + 1}  ·  {next}";
        }

        private static string DescribeNext(SpaceKind kind)
        {
            switch (kind)
            {
                case SpaceKind.CoinGain:
                    return "coins next";

                case SpaceKind.CoinLoss:
                    return "Chef Tako's cut next";

                case SpaceKind.Shrine:
                    return "the shrine next";

                case SpaceKind.Wasabi:
                    return "wasabi next";

                case SpaceKind.Swap:
                    return "the current next";

                default:
                    return "the start next";
            }
        }

        private void EnterResolving()
        {
            EndChoice();

            TurnReport report = session.CompleteTurn();

            view.PlaceToken(report.Seat, session.TokenFor(report.Seat).Space);

            if (report.Swapped)
            {
                view.PlaceToken(report.SwappedWith, session.TokenFor(report.SwappedWith).Space);
            }

            if (report.ClaimedGoldenCoin)
            {
                view.RefreshSpaces(session.Layout);
            }

            Spotlight(report.Seat);
            Announce(report);
            hud.RefreshTotals(session);
            React(report);

            resolveGate.Open();
            EnterPhase(BoardPhase.Resolving);
        }

        private void React(in TurnReport report)
        {
            Reaction next = ReactionFor(report);
            CharacterAnimation animation = view.AnimationFor(report.Seat);

            if (next == Reaction.None || !animation.IsAnimating)
            {
                reaction = Reaction.None;
                return;
            }

            reaction = next;
            reactionSeat = report.Seat;

            animation.SetSpeed(0f);

            switch (next)
            {
                case Reaction.Delight:
                    animation.Delight();
                    break;

                case Reaction.Dismay:
                    animation.Dismay();
                    break;

                case Reaction.Dizzy:
                    animation.Dizzy();

                    view.AnimationFor(report.SwappedWith).Dizzy();
                    break;

                case Reaction.Stunned:
                    animation.SetStunned(true);
                    break;
            }

            cameraRig.Cut(BoardShot.Reaction, view.TokenFor(report.Seat));
        }

        private static Reaction ReactionFor(in TurnReport report)
        {
            if (report.Skipped)
            {
                return Reaction.None;
            }

            if (report.ClaimedGoldenCoin)
            {
                return Reaction.Delight;
            }

            if (report.Landed == SpaceKind.Shrine)
            {
                return Reaction.Dismay;
            }

            if (report.CoinDelta > 0)
            {
                return Reaction.Delight;
            }

            if (report.CoinDelta < 0)
            {
                return Reaction.Dismay;
            }

            if (report.Swapped)
            {
                return Reaction.Dizzy;
            }

            return report.LostNextTurn ? Reaction.Stunned : Reaction.None;
        }

        private void ReleaseTurn()
        {
            if (diceBlock != null)
            {
                diceBlock.Dismiss();
            }

            if (cameraRig != null)
            {
                cameraRig.Release();
            }
        }

        private void EndTurn()
        {
            if (reaction == Reaction.Stunned && view != null)
            {
                view.AnimationFor(reactionSeat).SetStunned(false);
            }

            reaction = Reaction.None;

            ReleaseTurn();
        }

        private void Announce(in TurnReport report)
        {
            if (report.Skipped)
            {
                hud.ShowBanner($"{session.TokenFor(report.Seat).DisplayName} SITS OUT", Palette.WasabiGreen);
                GameAudio.Play(Sfx.BoardTurnSkipped);
                return;
            }

            switch (report.Landed)
            {
                case SpaceKind.CoinGain:
                    hud.ShowBanner($"+{report.CoinDelta} COINS", CoinOrange);

                    GameAudio.Play(Sfx.BoardSpaceCoinGain, Sfx.AmountParameter, report.CoinDelta);
                    break;

                case SpaceKind.CoinLoss:
                    hud.ShowBanner(
                        report.CoinDelta < 0 ? $"{report.CoinDelta} COINS" : "NOTHING LEFT TO TAKE",
                        Palette.TakoPurple);
                    GameAudio.Play(Sfx.BoardSpaceCoinLoss, Sfx.AmountParameter, Mathf.Abs(report.CoinDelta));
                    break;

                case SpaceKind.Shrine:
                    AnnounceShrine(report);
                    break;

                case SpaceKind.Wasabi:
                    hud.ShowBanner("WASABI! LOSE A TURN", Palette.WasabiGreen);
                    GameAudio.Play(Sfx.BoardSpaceWasabi);
                    break;

                case SpaceKind.Swap:
                    hud.ShowBanner(
                        report.Swapped
                            ? $"THE CURRENT — SWAPPED WITH {session.TokenFor(report.SwappedWith).DisplayName}"
                            : "THE CURRENT PASSES",
                        OceanCyan);
                    GameAudio.Play(Sfx.BoardSpaceSwap);
                    break;

                default:
                    hud.ShowBanner("BACK AT THE START", UiKit.Ink);

                    GameAudio.PlayLabelled(Sfx.BoardSpaceStart, Sfx.KindParameter, report.Landed.ToString());
                    break;
            }
        }

        private void AnnounceShrine(in TurnReport report)
        {
            if (!report.ClaimedGoldenCoin)
            {
                hud.ShowBanner($"THE SHRINE WANTS {BoardSession.ShrinePrice} COINS", ShrineGold);
                GameAudio.Play(Sfx.BoardShrineDenied);
                return;
            }

            hud.ShowBanner("GOLDEN COIN!", ShrineGold);
            GameAudio.Play(Sfx.BoardShrineClaim);

            GameAudio.Play(Sfx.BoardShrineMove);
        }

        private void UpdateResolving(float deltaTime)
        {
            if (StillHolding(resolveGate, deltaTime, ResolveHint))
            {
                if (resolveGate.AwaitingPress)
                {
                    ReleaseTurn();
                }

                return;
            }

            hud.HideBanner();
            ClearSpotlight();

            EndTurn();

            BeginNextTurn();
        }

        private void EnterRoundEnd()
        {
            ClearSpotlight();

            ReleaseTurn();

            hud.HidePrompt();
            hud.ShowBanner("MINIGAME TIME!", ShrineGold);
            GameAudio.Play(Sfx.BoardRoundEnd);

            roundEndGate.Open();
            EnterPhase(BoardPhase.RoundEnd);
        }

        private void UpdateRoundEnd(float deltaTime)
        {
            if (StillHolding(roundEndGate, deltaTime, RoundEndHint))
            {
                return;
            }

            HandOffToMinigame();
        }

        private void HandOffToMinigame()
        {
            EndTurn();

            MinigameId id = PickMinigame();

            if (MinigameFlow.Instance != null && MinigameFlow.Instance.LaunchMinigameFromBoard(id))
            {
                handedOff = true;
                return;
            }

            Debug.Log(
                "[SushiParty] No flow service took the handoff (direct play) — settling this round " +
                $"on the board's own die instead of playing {MinigameLibrary.Get(id).DisplayName}.",
                this);

            SettleWithoutAMinigame();
        }

        private static MinigameId PickMinigame()
        {
            IReadOnlyList<MinigameDefinition> all = MinigameLibrary.All;

            int eligible = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Implemented)
                {
                    eligible++;
                }
            }

            int wanted = Random.Range(0, eligible);
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].Implemented)
                {
                    continue;
                }

                if (wanted-- == 0)
                {
                    return all[i].Id;
                }
            }

            return MinigameId.BumperSparks;
        }

        private void SettleWithoutAMinigame()
        {
            bool pairWon = die.Roll() * 2 > die.Faces;

            session.ApplyMinigameOutcome(pairWon);
            hud.RefreshTotals(session);
            GameAudio.Play(pairWon ? Sfx.Win : Sfx.Lose);

            if (session.Finished)
            {
                EnterFinished();
                return;
            }

            EnterRoundIntro();
        }

        private void UpdateFinished()
        {
            if (!ContinuePressed())
            {
                return;
            }

            GameAudio.Play(Sfx.MenuBack);

            if (MinigameFlow.Instance != null)
            {
                MinigameFlow.Instance.ReturnToMenu();
            }
        }

        private void EnterFinished()
        {
            ClearSpotlight();

            EndTurn();

            hud.HidePrompt();

            hud.HideContinuePrompt();
            hud.HideBanner();

            BoardResult result = session.Result();
            hud.ShowResults(result);
            GameAudio.Play(result.PairWon ? Sfx.BoardSessionWin : Sfx.BoardSessionLose);

            if (result.PairWon)
            {
                Celebrate(BoardSeat.One);
                Celebrate(BoardSeat.Two);
            }

            EnterPhase(BoardPhase.Finished);
        }

        private void Celebrate(BoardSeat seat)
        {
            CharacterAnimation animation = view.AnimationFor(seat);

            animation.SetStunned(false);
            animation.SetSpeed(0f);
            animation.Cheer();
        }

        private void OnDestroy()
        {
            EndTurn();
        }

        private void EnterPhase(BoardPhase next)
        {
            phase = next;
            phaseTimer = 0f;
        }

        private void Spotlight(BoardSeat seat)
        {
            ClearSpotlight();

            view.SetTokenHighlight(seat, true);
            spotlight = seat;
            spotlit = true;
        }

        private void ClearSpotlight()
        {
            if (!spotlit)
            {
                return;
            }

            view.SetTokenHighlight(spotlight, false);
            spotlit = false;
        }

        private static Color ColorFor(BoardSeat seat)
        {
            switch (seat)
            {
                case BoardSeat.One:
                    return MatchSetup.SlotOneColor;

                case BoardSeat.Two:
                    return MatchSetup.SlotTwoColor;

                default:
                    return Palette.TakoPurple;
            }
        }
    }
}
