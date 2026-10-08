using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SushiParty.Menu
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        private const float WindowWidth = 620f;
        private const float WindowHeight = 470f;
        private const float RepeatDelay = 0.32f;
        private const float RepeatRate = 0.14f;

        private sealed class Entry
        {
            public string Caption;
            public System.Action Chosen;
            public RectTransform Root;
            public Image Plate;
            public Text Label;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Canvas canvas;
        private Text heldSummary;
        private int selectedIndex;
        private float navCooldown;
        private float lastDirection;
        private float timeScaleBeforePause = 1f;

        public static bool IsPaused { get; private set; }

        public bool IsShowing => IsPaused;

        internal static PauseMenuController CreateOn(GameObject host)
        {
            return host.AddComponent<PauseMenuController>();
        }

        public void Show()
        {
            if (IsShowing)
            {
                return;
            }

            IsPaused = true;

            GameAudio.Pause.Pause();

            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;

            Build();
            GameAudio.Play(Sfx.MenuToggle);
        }

        public void Hide()
        {
            if (!IsShowing)
            {
                return;
            }

            IsPaused = false;

            GameAudio.Pause.Resume();
            Time.timeScale = timeScaleBeforePause;

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
            }

            entries.Clear();
        }

        private void Toggle()
        {
            if (IsShowing)
            {
                GameAudio.Play(Sfx.MenuBack);
                Hide();
                return;
            }

            Show();
        }

        private static bool PausableScene()
        {
            return SceneManager.GetActiveScene().name != MinigameFlow.MenuSceneName;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Hide();
        }

        private void Build()
        {
            canvas = UiKit.CreateCanvas("PauseCanvas", 190, transform, match: 0.5f);

            UiKit.Panel(canvas.transform, "Scrim", UiKit.Backdrop).Rt().Stretch();

            RectTransform window = UiKit.Rect(canvas.transform, "Window");
            window.Pin(new Vector2(0.5f, 0.5f), new Vector2(WindowWidth, WindowHeight), Vector2.zero);
            UiKit.Window(window, UiKit.Cream);
            UiKit.Banner(window, "PAUSED", UiKit.PanelBlue, 38, 320f);

            selectedIndex = 0;
            entries.Clear();
            AddEntry(window, "Resume", 0, Hide);
            AddEntry(window, "Audio settings", 1, OpenSettings);
            AddEntry(window, "Quit to menu", 2, QuitToMenu);

            BuildHeldSummary(window);
            Refresh();
        }

        private void AddEntry(RectTransform window, string caption, int index, System.Action chosen)
        {
            RectTransform root = UiKit.Rect(window, $"Entry_{index}");
            root.Pin(new Vector2(0.5f, 1f), new Vector2(WindowWidth - 110f, 66f), new Vector2(0f, -(112f + index * 76f)));

            Image plate = UiKit.RoundedPanel(root, "Plate", Color.clear);
            plate.Rt().Stretch();

            Text label = UiKit.Label(root, caption, 32, TextAnchor.MiddleCenter, UiKit.DarkInk, FontStyle.Bold);
            label.Rt().Stretch();

            entries.Add(new Entry
            {
                Caption = caption,
                Chosen = chosen,
                Root = root,
                Plate = plate,
                Label = label,
            });
        }

        private void BuildHeldSummary(RectTransform window)
        {
            System.Text.StringBuilder held = new System.Text.StringBuilder();
            IReadOnlyList<MixerChannel> channels = GameAudio.Pause.HeldChannels;

            for (int i = 0; i < channels.Count; i++)
            {
                if (held.Length > 0)
                {
                    held.Append(" and ");
                }

                held.Append(channels[i].DisplayName.ToLowerInvariant());
            }

            heldSummary = UiKit.Label(
                window,
                $"Holding the {held} buses.\n{Mixer.Interface.DisplayName} stays live, which is why this menu still clicks.",
                19,
                TextAnchor.MiddleCenter,
                UiKit.DarkInkDim);

            heldSummary.Rt().Pin(new Vector2(0.5f, 0f), new Vector2(WindowWidth - 90f, 60f), new Vector2(0f, 46f));
        }

        private void Refresh()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                bool selected = i == selectedIndex;
                entries[i].Plate.color = selected ? new Color(0.16f, 0.21f, 0.40f, 0.15f) : Color.clear;
                entries[i].Label.color = selected ? UiKit.DarkInk : UiKit.DarkInkDim;
            }
        }

        private void OpenSettings()
        {
            SettingsMenuController.Open(transform);
        }

        private void QuitToMenu()
        {
            if (MinigameFlow.Instance == null || MinigameFlow.Instance.IsTransitioning)
            {
                return;
            }

            Hide();
            GameAudio.Pause.StopEverything();

            MinigameController round = FindFirstObjectByType<MinigameController>();
            if (round != null && round.Abandon())
            {
                return;
            }

            MinigameFlow.Instance.ReturnToMenu();
        }

        private void Update()
        {
            if (HandleOpenKey())
            {
                return;
            }

            if (!IsShowing)
            {
                return;
            }

            if (SettingsMenuController.IsOpen)
            {
                return;
            }

            navCooldown -= Time.unscaledDeltaTime;
            HandleNavigation();
            HandleConfirm();
        }

        private bool HandleOpenKey()
        {
            if (SettingsMenuController.IsOpen
                || SettingsMenuController.ClosedOnFrame == Time.frameCount)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool pressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (!pressed && gamepad != null)
            {
                pressed = gamepad.startButton.wasPressedThisFrame;
            }

            if (!pressed)
            {
                return false;
            }

            if (!IsShowing && !PausableScene())
            {
                return false;
            }

            Toggle();
            return true;
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

            int step = direction > 0f ? -1 : 1;
            int next = ((selectedIndex + step) % entries.Count + entries.Count) % entries.Count;

            if (next == selectedIndex)
            {
                return;
            }

            selectedIndex = next;
            Refresh();
            GameAudio.Play(Sfx.MenuMove);
        }

        private static float ReadVertical()
        {
            Keyboard keyboard = Keyboard.current;
            float direction = 0f;

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
                pressed = gamepad.buttonSouth.wasPressedThisFrame;
            }

            if (!pressed || selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return;
            }

            GameAudio.Play(Sfx.MenuConfirm);
            entries[selectedIndex].Chosen?.Invoke();
        }

        private void OnDestroy()
        {
            if (IsShowing)
            {
                IsPaused = false;
                GameAudio.Pause.Resume();
                Time.timeScale = timeScaleBeforePause;
            }
        }
    }
}
