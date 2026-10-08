using SushiParty.Audio;
using System.Collections.Generic;
using System.Text;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiParty.Menu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const int Columns = 3;
        private const float CardWidth = 380f;
        private const float CardHeight = 250f;
        private const float CardGap = 28f;
        private const float GridTop = 180f;
        private const float CardTitleStrip = 62f;
        private const float GridWidth = Columns * CardWidth + (Columns - 1) * CardGap;
        private const float RepeatDelay = 0.32f;
        private const float RepeatRate = 0.12f;

        private sealed class Card
        {
            public MinigameDefinition Definition;
            public RectTransform Root;
            public Image Body;
            public Image Edge;
            public Image TitlePlate;
            public Text Badge;
        }

        private readonly List<Card> cards = new List<Card>();
        private MenuSelection selection;
        private Canvas canvas;
        private TitleMenuController title;
        private bool isShowing = true;
        private float navCooldown;
        private Vector2 lastNavDirection;
        private Text detailTitle;
        private Text detailObjective;
        private Text detailRoles;
        private Text seatSummary;
        private Text lastResultText;
        private MatchSetup Setup => MinigameFlow.Instance != null ? MinigameFlow.Instance.Setup : fallbackSetup;
        private readonly MatchSetup fallbackSetup = new MatchSetup();

        private void Start()
        {
            selection = new MenuSelection(MinigameLibrary.All, Setup, Columns);

            canvas = UiKit.CreateCanvas("MenuCanvas", 10, match: 0f);
            canvas.transform.SetParent(transform, false);

            BuildBackground(canvas.transform);
            BuildHeader(canvas.transform);
            BuildGrid(canvas.transform);
            BuildDetails(canvas.transform);
            BuildFooter(canvas.transform);

            RefreshSelection();
            RefreshSeatSummary();
            RefreshLastResult();

            SetShowing(false);
            title = gameObject.AddComponent<TitleMenuController>();
        }

        public void SetShowing(bool showing)
        {
            isShowing = showing;

            if (canvas != null)
            {
                canvas.gameObject.SetActive(showing);
            }
        }

        private void BuildBackground(Transform root)
        {
            UiKit.Panel(root, "Backdrop", UiKit.PartyBlue).Rt().Stretch();
        }

        private void BuildHeader(Transform root)
        {
            UiKit.Label(root, "SUSHI PARTY", 68, TextAnchor.UpperLeft, UiKit.Ink, FontStyle.Bold)
                .Chunky(4f)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(900f, 84f), new Vector2(64f, -34f));

            UiKit.Label(root, "Chef Tako Minigames  ·  two players, co-operative, local only",
                    26, TextAnchor.UpperLeft, UiKit.InkDim)
                .Chunky(1.8f)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(900f, 40f), new Vector2(66f, -114f));

            lastResultText = UiKit.Label(root, string.Empty, 26, TextAnchor.UpperRight, UiKit.InkDim).Chunky(1.8f);
            lastResultText.Rt().Pin(new Vector2(1f, 1f), new Vector2(760f, 70f), new Vector2(-64f, -48f));
        }

        private void BuildGrid(Transform root)
        {
            RectTransform host = UiKit.Rect(root, "Grid");
            host.Stretch();

            IReadOnlyList<MinigameDefinition> all = selection.Entries;
            for (int i = 0; i < all.Count; i++)
            {
                cards.Add(BuildCard(host, all[i], i));
            }
        }

        private Card BuildCard(Transform root, MinigameDefinition definition, int index)
        {
            int column = index % Columns;
            int row = index / Columns;

            float x = (column - (Columns - 1) * 0.5f) * (CardWidth + CardGap);
            float y = -(GridTop + row * (CardHeight + CardGap));

            RectTransform card = UiKit.Rect(root, $"Card_{definition.Id}");
            card.Pin(new Vector2(0.5f, 1f), new Vector2(CardWidth, CardHeight), new Vector2(x, y));

            UiKit.Window(card, UiKit.Cream, edge: 5f, shadow: 8f);
            Image edge = card.Find("Edge").GetComponent<Image>();
            Image body = card.Find("Body").GetComponent<Image>();

            Image plate = UiKit.RoundedPanel(card, "TitlePlate", definition.Accent);
            plate.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(CardWidth - 26f, 46f), new Vector2(0f, -13f));

            UiKit.Label(plate.Rt(), definition.DisplayName, 26, TextAnchor.MiddleCenter, UiKit.Ink, FontStyle.Bold)
                .Chunky(2.2f)
                .Rt().Stretch(6f);

            RectTransform preview = UiKit.Rect(card, "Preview");
            preview.anchorMin = Vector2.zero;
            preview.anchorMax = Vector2.one;
            preview.offsetMin = new Vector2(14f, 14f);
            preview.offsetMax = new Vector2(-14f, -CardTitleStrip);
            preview.gameObject.AddComponent<RectMask2D>();
            MinigamePreview.Build(preview, definition);

            Text badge = null;
            if (!definition.Implemented)
            {
                badge = UiKit.Label(card, "SPEC ONLY", 20, TextAnchor.LowerRight,
                    new Color(0.97f, 0.78f, 0.30f), FontStyle.Bold).Chunky(2f);
                badge.Rt().Pin(new Vector2(1f, 0f), new Vector2(CardWidth - 40f, 28f), new Vector2(-20f, 20f));
            }

            return new Card
            {
                Definition = definition,
                Root = card,
                Body = body,
                Edge = edge,
                TitlePlate = plate,
                Badge = badge,
            };
        }

        private void BuildDetails(Transform root)
        {
            RectTransform panel = UiKit.Rect(root, "Details");
            panel.Pin(new Vector2(0.5f, 0f), new Vector2(GridWidth, 300f), new Vector2(0f, 64f));

            UiKit.Window(panel, UiKit.Cream);

            detailTitle = UiKit.Label(panel, string.Empty, 40, TextAnchor.UpperLeft, UiKit.DarkInk, FontStyle.Bold);
            detailTitle.Rt().Pin(new Vector2(0f, 1f), new Vector2(1010f, 50f), new Vector2(32f, -22f));

            detailObjective = UiKit.Label(panel, string.Empty, 25, TextAnchor.UpperLeft, UiKit.DarkInk);
            detailObjective.Rt().Pin(new Vector2(0f, 1f), new Vector2(1010f, 110f), new Vector2(32f, -78f));

            detailRoles = UiKit.Label(panel, string.Empty, 23, TextAnchor.UpperLeft, UiKit.DarkInkDim);
            detailRoles.Rt().Pin(new Vector2(0f, 1f), new Vector2(1010f, 130f), new Vector2(32f, -192f));

            UiKit.RoundedPanel(panel, "SeatDivider", new Color(0f, 0f, 0f, 0.12f))
                .Rt().Pin(new Vector2(0f, 0.5f), new Vector2(2f, 250f), new Vector2(1090f, 0f));

            UiKit.Label(panel, "TEAM", 24, TextAnchor.UpperLeft, UiKit.DarkInk, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(500f, 34f), new Vector2(1130f, -26f));

            seatSummary = UiKit.Label(panel, string.Empty, 24, TextAnchor.UpperLeft, UiKit.DarkInkDim);
            seatSummary.Rt().Pin(new Vector2(0f, 1f), new Vector2(500f, 250f), new Vector2(1130f, -66f));
        }

        private void BuildFooter(Transform root)
        {
            UiKit.Pill(root,
                    "Arrows / WASD: choose    Enter or Space: play    B: board game    "
                    + "1: seat 1    2: seat 2    3: partner skill    4: Chef Tako skill    O: audio",
                    UiKit.DarkInk, UiKit.Ink, 23, new Vector2(GridWidth, 48f))
                .Rt().parent.GetComponent<RectTransform>()
                .Pin(new Vector2(0.5f, 0f), new Vector2(GridWidth, 48f), new Vector2(0f, 16f));
        }

        private void Update()
        {
            if (!isShowing || SettingsMenuController.IsOpen)
            {
                return;
            }

            if (HandleBack())
            {
                return;
            }

            HandleSettingsKey();
            HandleNavigation();
            HandleMouse();
            HandleSeatToggles();
            HandleLaunch();
        }

        private bool HandleBack()
        {
            if (SettingsMenuController.ClosedOnFrame == Time.frameCount)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool back = keyboard != null
                        && (keyboard.escapeKey.wasPressedThisFrame
                            || keyboard.backspaceKey.wasPressedThisFrame);

            if (!back && gamepad != null)
            {
                back = gamepad.buttonEast.wasPressedThisFrame;
            }

            if (!back || title == null)
            {
                return false;
            }

            GameAudio.Play(Sfx.MenuBack);
            title.Show();
            return true;
        }

        private void HandleSettingsKey()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool pressed = keyboard != null && keyboard.oKey.wasPressedThisFrame;
            if (!pressed && gamepad != null)
            {
                pressed = gamepad.selectButton.wasPressedThisFrame;
            }

            if (!pressed)
            {
                return;
            }

            GameAudio.Play(Sfx.MenuConfirm);
            SettingsMenuController.Open(transform);
        }

        private void HandleNavigation()
        {
            Vector2 direction = ReadNavigation();

            if (direction == Vector2.zero)
            {
                navCooldown = 0f;
                lastNavDirection = Vector2.zero;
                return;
            }

            bool changedDirection = direction != lastNavDirection;
            navCooldown -= Time.unscaledDeltaTime;

            if (!changedDirection && navCooldown > 0f)
            {
                return;
            }

            lastNavDirection = direction;
            navCooldown = changedDirection ? RepeatDelay : RepeatRate;

            bool moved = selection.Navigate(Step(direction));
            RefreshSelection();

            if (moved)
            {
                GameAudio.Play(Sfx.MenuMove);
            }
        }

        private static Vector2Int Step(Vector2 direction)
        {
            return new Vector2Int(
                direction.x == 0f ? 0 : (int)Mathf.Sign(direction.x),
                direction.y == 0f ? 0 : (int)Mathf.Sign(direction.y));
        }

        private Vector2 ReadNavigation()
        {
            Vector2 direction = Vector2.zero;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction.x -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction.x += 1f;
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) direction.y += 1f;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) direction.y -= 1f;
            }

            if (direction == Vector2.zero && Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue() + Gamepad.current.dpad.ReadValue();
                if (stick.sqrMagnitude > 0.35f)
                {
                    direction = Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                        ? new Vector2(Mathf.Sign(stick.x), 0f)
                        : new Vector2(0f, Mathf.Sign(stick.y));
                }
            }

            return direction;
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            for (int i = 0; i < cards.Count; i++)
            {
                if (!cards[i].Root.ContainsScreenPoint(position))
                {
                    continue;
                }

                if (selection.SelectIndex(i))
                {
                    RefreshSelection();
                    GameAudio.Play(Sfx.MenuMove);
                }

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Launch();
                }

                return;
            }
        }

        private void HandleSeatToggles()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool changed = false;

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                selection.ToggleSeat(ParticipantSlot.One);
                changed = true;
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                selection.ToggleSeat(ParticipantSlot.Two);
                changed = true;
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                selection.CycleSkill(SkillDial.Partner);
                changed = true;
            }

            if (keyboard.digit4Key.wasPressedThisFrame)
            {
                selection.CycleSkill(SkillDial.Chef);
                changed = true;
            }

            if (changed)
            {
                RefreshSeatSummary();
                GameAudio.Play(Sfx.MenuToggle);
            }
        }

        private void HandleLaunch()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool board = keyboard != null && keyboard.bKey.wasPressedThisFrame;
            if (!board && gamepad != null)
            {
                board = gamepad.buttonNorth.wasPressedThisFrame;
            }

            if (board)
            {
                LaunchBoardGame();
                return;
            }

            bool pressed = keyboard != null
                           && (keyboard.enterKey.wasPressedThisFrame
                               || keyboard.numpadEnterKey.wasPressedThisFrame
                               || keyboard.spaceKey.wasPressedThisFrame);

            if (!pressed && gamepad != null)
            {
                pressed = gamepad.buttonSouth.wasPressedThisFrame
                          || gamepad.startButton.wasPressedThisFrame;
            }

            if (pressed)
            {
                Launch();
            }
        }

        private void Launch()
        {
            if (selection.Confirm() == null)
            {
                return;
            }

            Open();
        }

        private void LaunchBoardGame()
        {
            selection.ConfirmBoardGame();
            Open();
        }

        private void Open()
        {
            if (MinigameFlow.Instance == null)
            {
                Debug.LogError("No MinigameFlow service — cannot launch. Start from the Boot scene.");
                return;
            }

            GameAudio.Play(Sfx.MenuConfirm);

            if (selection.Mode == SessionMode.BoardGame)
            {
                MinigameFlow.Instance.StartBoardGame(Setup);
                return;
            }

            MinigameFlow.Instance.Launch(Setup);
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                bool isSelected = i == selection.SelectedIndex;

                card.Edge.color = isSelected ? card.Definition.Accent : UiKit.WindowEdge;
                card.Body.color = isSelected
                    ? Color.Lerp(UiKit.Cream, card.Definition.Accent, 0.12f)
                    : UiKit.Cream;
                card.Root.localScale = Vector3.one * (isSelected ? 1.06f : 1f);

                card.Root.SetSiblingIndex(isSelected ? cards.Count - 1 : i);
            }

            MinigameDefinition definition = selection.Selected;

            detailTitle.text = definition.DisplayName;
            detailTitle.color = definition.Accent;
            detailObjective.text = definition.Objective;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Seat 1 — {definition.RoleP1}");
            sb.AppendLine($"Seat 2 — {definition.RoleP2}");
            sb.AppendLine($"Time limit — {definition.TimeLimit:0}s");
            if (!definition.Implemented)
            {
                sb.AppendLine();
                sb.AppendLine("<b>Not built yet.</b> Opening it shows the full design spec.");
            }

            detailRoles.text = sb.ToString();
        }

        private void RefreshSeatSummary()
        {
            ControllerKind seatOne = selection.SeatKind(ParticipantSlot.One);
            ControllerKind seatTwo = selection.SeatKind(ParticipantSlot.Two);
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"[1]  Seat 1:  <b>{MenuSelection.Describe(seatOne)}</b>");
            sb.AppendLine($"        {DescribeDevice(seatOne, selection.SeatDevice(ParticipantSlot.One))}");
            sb.AppendLine();
            sb.AppendLine($"[2]  Seat 2:  <b>{MenuSelection.Describe(seatTwo)}</b>");
            sb.AppendLine($"        {DescribeDevice(seatTwo, selection.SeatDevice(ParticipantSlot.Two))}");
            sb.AppendLine();
            sb.AppendLine($"[3]  CPU partner skill:  <b>{selection.PartnerSkill}</b>");
            sb.AppendLine($"[4]  Chef Tako skill:  <b>{selection.ChefSkill}</b>");

            seatSummary.text = sb.ToString();
        }

        private void RefreshLastResult()
        {
            MinigameFlow flow = MinigameFlow.Instance;
            if (flow == null || flow.LastOutcome == null || flow.LastPlayed == null)
            {
                lastResultText.text = string.Empty;
                return;
            }

            MinigameOutcome outcome = flow.LastOutcome.Value;
            if (outcome.Result == MinigameResult.Abandoned)
            {
                lastResultText.text = string.Empty;
                return;
            }

            string name = MinigameLibrary.Get(flow.LastPlayed.Value).DisplayName;
            string verdict = outcome.Won ? "Team wins" : "Chef Tako wins";
            string delta = (outcome.CoinDelta > 0 ? "+" : string.Empty) + outcome.CoinDelta;
            lastResultText.text = $"Last round — {name}: {verdict} ({delta} Coins each)";
            lastResultText.color = outcome.Won
                ? new Color(0.45f, 0.92f, 0.55f)
                : new Color(1f, 0.46f, 0.40f);
        }

        private static string DescribeDevice(ControllerKind kind, InputDeviceChoice device)
        {
            if (kind == ControllerKind.Cpu)
            {
                return "co-op partner AI";
            }

            return new HumanParticipantInput(device).DescribeControls();
        }
    }
}
