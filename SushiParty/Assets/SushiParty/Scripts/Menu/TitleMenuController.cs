using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiParty.Menu
{
    public sealed class TitleMenuController : MonoBehaviour
    {
        private const float EntryWidth = 860f;
        private const float EntryHeight = 118f;
        private const float EntryGap = 22f;
        private const float EntriesTop = 400f;
        private const float PlateWidth = 330f;
        private const float SelectedNudge = 26f;
        private const float RepeatDelay = 0.32f;
        private const float RepeatRate = 0.14f;
        private static readonly Color Highlight = new Color(1f, 0.82f, 0.25f);

        private sealed class Entry
        {
            public TitleChoice Choice;
            public RectTransform Root;
            public Image Body;
            public Image Edge;
            public Image Plate;
            public Text Caption;
            public Text Blurb;
            public Vector2 Home;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private TitleSelection selection;
        private Canvas canvas;
        private float navCooldown;
        private float lastDirection;
        private int hovered = -1;
        private MainMenuController grid;

        private MatchSetup Setup => MinigameFlow.Instance != null
            ? MinigameFlow.Instance.Setup
            : fallbackSetup;

        private readonly MatchSetup fallbackSetup = new MatchSetup();

        public bool IsShowing { get; private set; }

        private void Start()
        {
            selection = new TitleSelection();
            grid = GetComponent<MainMenuController>();

            Build();
            Show();

            if (MinigameFlow.Instance != null)
            {
                MinigameFlow.Instance.StartMenuMusic();
            }

            GameAudio.Play(Sfx.TitleShow);
        }

        public void Show()
        {
            IsShowing = true;
            hovered = -1;

            if (canvas != null)
            {
                canvas.gameObject.SetActive(true);
            }

            if (grid != null)
            {
                grid.SetShowing(false);
            }

            Refresh();
        }

        private void HideForGrid()
        {
            IsShowing = false;
            hovered = -1;

            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }

            if (grid != null)
            {
                grid.SetShowing(true);
            }
        }

        private static Color AccentFor(TitleChoice choice)
        {
            switch (choice)
            {
                case TitleChoice.PlayBoard:
                    return new Color(1f, 0.62f, 0.18f);
                case TitleChoice.PlayMinigames:
                    return new Color(0.18f, 0.78f, 0.70f);
                default:
                    return new Color(0.62f, 0.42f, 0.86f);
            }
        }

        private void Build()
        {
            canvas = UiKit.CreateCanvas("TitleCanvas", 20, transform, match: 0f);

            UiKit.Panel(canvas.transform, "Backdrop", UiKit.PartyBlue).Rt().Stretch();

            UiKit.Disc(canvas.transform, "Glow", new Color(0.35f, 0.45f, 0.95f, 0.30f))
                .Rt().Pin(new Vector2(0.5f, 1f), new Vector2(1500f, 900f), new Vector2(0f, 250f));

            BuildWordmark(canvas.transform);

            IReadOnlyList<TitleChoice> all = selection.Entries;
            for (int i = 0; i < all.Count; i++)
            {
                entries.Add(BuildEntry(canvas.transform, all[i], i));
            }

            UiKit.Pill(canvas.transform,
                    "↑↓ or W/S: choose     Enter, Space or A: go",
                    UiKit.DarkInk, UiKit.Ink, 24, new Vector2(680f, 46f))
                .Rt().parent.GetComponent<RectTransform>()
                .Pin(new Vector2(0.5f, 0f), new Vector2(680f, 46f), new Vector2(0f, 44f));
        }

        private void BuildWordmark(Transform root)
        {
            UiKit.Label(root, "SUSHI PARTY", 118, TextAnchor.UpperCenter, UiKit.Ink, FontStyle.Bold)
                .Chunky(6f)
                .Rt().Pin(new Vector2(0.5f, 1f), new Vector2(1400f, 140f), new Vector2(0f, -104f));

            RectTransform strip = UiKit.Rect(root, "Subtitle");
            strip.Pin(new Vector2(0.5f, 1f), new Vector2(760f, 52f), new Vector2(0f, -248f));
            UiKit.RoundedPanel(strip, "Fill", new Color(0f, 0f, 0f, 0.28f)).Rt().Stretch();

            UiKit.Label(strip, "CHEF TAKO MINIGAMES  ·  TWO PLAYERS, CO-OPERATIVE",
                    24, TextAnchor.MiddleCenter, UiKit.InkDim, FontStyle.Bold)
                .Rt().Stretch(8f);
        }

        private Entry BuildEntry(Transform root, TitleChoice choice, int index)
        {
            Vector2 home = new Vector2(0f, -(EntriesTop + index * (EntryHeight + EntryGap)));

            RectTransform card = UiKit.Rect(root, "Entry_" + choice);
            card.Pin(new Vector2(0.5f, 1f), new Vector2(EntryWidth, EntryHeight), home);

            UiKit.Window(card, UiKit.Cream, edge: 5f, shadow: 9f);
            Image edge = card.Find("Edge").GetComponent<Image>();
            Image body = card.Find("Body").GetComponent<Image>();

            Image plate = UiKit.RoundedPanel(card, "Plate", AccentFor(choice));
            plate.Rt().Pin(new Vector2(0f, 0.5f), new Vector2(PlateWidth, EntryHeight - 30f),
                new Vector2(16f, 0f));
            plate.Rt().pivot = new Vector2(0f, 0.5f);
            plate.Rt().anchoredPosition = new Vector2(16f, 0f);

            Text caption = UiKit.Label(plate.Rt(), TitleSelection.Caption(choice), 34,
                TextAnchor.MiddleCenter, UiKit.Ink, FontStyle.Bold);
            caption.Chunky(2.4f).Rt().Stretch(8f);

            Text blurb = UiKit.Label(card, TitleSelection.Blurb(choice), 24,
                TextAnchor.MiddleLeft, UiKit.DarkInkDim);
            blurb.Rt().Pin(new Vector2(0f, 0.5f), new Vector2(EntryWidth - PlateWidth - 70f, 60f),
                new Vector2(PlateWidth + 40f, 0f));
            blurb.Rt().pivot = new Vector2(0f, 0.5f);
            blurb.Rt().anchoredPosition = new Vector2(PlateWidth + 40f, 0f);

            return new Entry
            {
                Choice = choice,
                Root = card,
                Body = body,
                Edge = edge,
                Plate = plate,
                Caption = caption,
                Blurb = blurb,
                Home = home,
            };
        }

        private void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                bool selected = i == selection.SelectedIndex;

                entry.Body.color = selected ? new Color(1f, 0.98f, 0.90f) : UiKit.Cream;
                entry.Edge.color = selected ? Highlight : UiKit.WindowEdge;
                entry.Blurb.color = selected ? UiKit.DarkInk : UiKit.DarkInkDim;

                entry.Root.anchoredPosition = selected
                    ? entry.Home + new Vector2(SelectedNudge, 0f)
                    : entry.Home;
            }
        }

        private void Confirm()
        {
            TitleChoice choice = selection.Selected;

            GameAudio.PlayLabelled(Sfx.TitleConfirm, Sfx.ChoiceParameter, choice.ToString());

            switch (choice)
            {
                case TitleChoice.PlayBoard:
                    if (MinigameFlow.Instance != null)
                    {
                        MinigameFlow.Instance.StartBoardGame(Setup);
                    }

                    break;

                case TitleChoice.PlayMinigames:
                    HideForGrid();
                    break;

                default:
                    SettingsMenuController.Open(transform);
                    break;
            }
        }

        private void Update()
        {
            if (!IsShowing || SettingsMenuController.IsOpen)
            {
                return;
            }

            navCooldown -= Time.unscaledDeltaTime;

            HandleNavigation();
            HandleMouse();
            HandleConfirm();
        }

        private void HandleNavigation()
        {
            float direction = ReadVertical();

            if (direction == 0f)
            {
                navCooldown = 0f;
                lastDirection = 0f;
                return;
            }

            bool fresh = !Mathf.Approximately(direction, lastDirection);
            lastDirection = direction;

            if (!fresh && navCooldown > 0f)
            {
                return;
            }

            navCooldown = fresh ? RepeatDelay : RepeatRate;

            if (selection.Navigate(direction > 0f ? -1 : 1))
            {
                Refresh();
                GameAudio.Play(Sfx.TitleMove);
            }
        }

        private static float ReadVertical()
        {
            float direction = 0f;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) direction += 1f;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) direction -= 1f;
            }

            if (direction == 0f && Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue() + Gamepad.current.dpad.ReadValue();
                if (Mathf.Abs(stick.y) > 0.5f)
                {
                    direction = Mathf.Sign(stick.y);
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

            int over = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Root.ContainsScreenPoint(position))
                {
                    over = i;
                    break;
                }
            }

            if (over != hovered)
            {
                if (hovered >= 0)
                {
                    GameAudio.Play(Sfx.ButtonOut);
                }

                if (over >= 0)
                {
                    GameAudio.Play(Sfx.ButtonOver);

                    if (selection.SelectIndex(over))
                    {
                        Refresh();
                    }
                }

                hovered = over;
            }

            if (over >= 0 && mouse.leftButton.wasPressedThisFrame)
            {
                GameAudio.Play(Sfx.ButtonIn);
                Confirm();
            }
        }

        private void HandleConfirm()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

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
                Confirm();
            }
        }
    }
}
