using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SushiParty.Menu
{
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private const float WindowWidth = 1180f;
        private const float WindowHeight = 800f;
        private const float RowHeight = 78f;
        private const float RowGap = 8f;
        private const float RowsTop = 118f;
        private const float TrackWidth = 380f;
        private const float RepeatDelay = 0.32f;
        private const float RepeatRate = 0.09f;
        private const float DiagnosticsInterval = 0.25f;

        private sealed class Row
        {
            public MixerChannel Channel;
            public RectTransform Root;
            public Image Plate;
            public Text Name;
            public Text Summary;
            public Image TrackFill;
            public Text Readout;
            public Text Final;
            public Text Mute;
        }

        private readonly List<Row> rows = new List<Row>();
        private MixerSettings settings;
        private Text diagnostics;
        private float navCooldown;
        private float diagnosticsCooldown;
        private bool holdingChannelGroups;
        private bool suspendedPause;
        private AudioHandle audition;
        private MixerChannel auditionedChannel;
        private Vector2 lastDirection;
        private System.Action onClosed;

        public static bool IsOpen { get; private set; }

        public static int ClosedOnFrame { get; private set; } = -1;

        public static SettingsMenuController Open(Transform parent, System.Action onClosed = null)
        {
            if (IsOpen)
            {
                return null;
            }

            GameObject host = new GameObject("SettingsMenu");
            host.transform.SetParent(parent, false);

            SettingsMenuController controller = host.AddComponent<SettingsMenuController>();
            controller.onClosed = onClosed;
            return controller;
        }

        public void Close()
        {
            ClosedOnFrame = Time.frameCount;
            onClosed?.Invoke();

            Destroy(gameObject);
        }

        private void Awake()
        {
            IsOpen = true;
            settings = GameAudio.Settings;

            suspendedPause = GameAudio.Pause.Suspend();
        }

        private void Start()
        {
            Canvas canvas = UiKit.CreateCanvas("SettingsCanvas", 200, transform, match: 0.5f);

            UiKit.Panel(canvas.transform, "Scrim", UiKit.Backdrop).Rt().Stretch();

            RectTransform window = UiKit.Rect(canvas.transform, "Window");
            window.Pin(new Vector2(0.5f, 0.5f), new Vector2(WindowWidth, WindowHeight), Vector2.zero);
            UiKit.Window(window, UiKit.Cream);
            UiKit.Banner(window, "AUDIO", UiKit.PanelBlue, 40, 400f);

            BuildRows(window);
            BuildDiagnostics(window);
            BuildFooter(window);

            HoldChannelGroups();

            Refresh();
        }

        private void OnDestroy()
        {
            // settings is assigned in Awake, so this is only null if the object died before that ran.
            settings?.Save();

            StopAudition();
            ReleaseChannelGroups();

            if (suspendedPause)
            {
                suspendedPause = false;
                GameAudio.Pause.Restore();
            }

            IsOpen = false;
        }

        private void BuildRows(RectTransform window)
        {
            IReadOnlyList<MixerChannel> channels = settings.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                rows.Add(BuildRow(window, channels[i], i));
            }
        }

        private Row BuildRow(RectTransform window, MixerChannel channel, int index)
        {
            float y = -(RowsTop + index * (RowHeight + RowGap));

            RectTransform root = UiKit.Rect(window, $"Row_{channel.DisplayName}");
            root.Pin(new Vector2(0.5f, 1f), new Vector2(WindowWidth - 96f, RowHeight), new Vector2(0f, y));

            Image plate = UiKit.RoundedPanel(root, "Plate", Color.clear);
            plate.Rt().Stretch();

            Text name = UiKit.Label(root, channel.DisplayName, 30, TextAnchor.MiddleLeft, UiKit.DarkInk, FontStyle.Bold);
            name.Rt().Pin(new Vector2(0f, 1f), new Vector2(320f, 34f), new Vector2(22f, -12f));

            Text summary = UiKit.Label(root, channel.Summary, 19, TextAnchor.MiddleLeft, UiKit.DarkInkDim);
            summary.Rt().Pin(new Vector2(0f, 1f), new Vector2(320f, 26f), new Vector2(22f, -46f));

            RectTransform track = UiKit.Rect(root, "Track");
            track.Pin(new Vector2(0f, 0.5f), new Vector2(TrackWidth, 18f), new Vector2(360f, 0f));
            track.pivot = new Vector2(0f, 0.5f);
            UiKit.RoundedPanel(track, "TrackBed", new Color(0f, 0f, 0f, 0.14f)).Rt().Stretch();

            RectTransform fillRoot = UiKit.Rect(track, "Fill");
            fillRoot.anchorMin = new Vector2(0f, 0f);
            fillRoot.anchorMax = new Vector2(0f, 1f);
            fillRoot.pivot = new Vector2(0f, 0.5f);
            fillRoot.offsetMin = new Vector2(0f, 0f);
            fillRoot.offsetMax = new Vector2(0f, 0f);

            Image fill = UiKit.RoundedPanel(fillRoot, "FillBar", UiKit.PanelBlue);
            fill.Rt().Stretch();

            Text readout = UiKit.Label(root, string.Empty, 25, TextAnchor.MiddleRight, UiKit.DarkInk, FontStyle.Bold);
            readout.Rt().Pin(new Vector2(1f, 1f), new Vector2(250f, 32f), new Vector2(-96f, -14f));

            Text final = UiKit.Label(root, string.Empty, 18, TextAnchor.MiddleRight, UiKit.DarkInkDim);
            final.Rt().Pin(new Vector2(1f, 1f), new Vector2(250f, 24f), new Vector2(-96f, -46f));

            Text mute = UiKit.Label(root, string.Empty, 22, TextAnchor.MiddleRight, UiKit.DarkInkDim, FontStyle.Bold);
            mute.Rt().Pin(new Vector2(1f, 0.5f), new Vector2(80f, 30f), new Vector2(-14f, 0f));

            return new Row
            {
                Channel = channel,
                Root = root,
                Plate = plate,
                Name = name,
                Summary = summary,
                TrackFill = fill,
                Readout = readout,
                Final = final,
                Mute = mute,
            };
        }

        private void BuildDiagnostics(RectTransform window)
        {
            RectTransform strip = UiKit.Rect(window, "Diagnostics");
            strip.Pin(new Vector2(0.5f, 0f), new Vector2(WindowWidth - 96f, 122f), new Vector2(0f, 108f));

            UiKit.RoundedPanel(strip, "Bed", new Color(0f, 0f, 0f, 0.08f)).Rt().Stretch();

            UiKit.Label(strip, "WHAT THE MIXER REPORTS", 17, TextAnchor.UpperLeft, UiKit.DarkInkDim, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(500f, 22f), new Vector2(20f, -12f));

            diagnostics = UiKit.Label(window, string.Empty, 19, TextAnchor.UpperLeft, UiKit.DarkInk);
            diagnostics.Rt().Pin(new Vector2(0.5f, 0f), new Vector2(WindowWidth - 140f, 80f), new Vector2(0f, 122f));
        }

        private void BuildFooter(RectTransform window)
        {
            UiKit.Label(
                    window,
                    "↑↓ choose   ←→ adjust   [M] mute   [R] reset   [Esc] back",
                    22,
                    TextAnchor.MiddleCenter,
                    UiKit.DarkInkDim)
                .Rt().Pin(new Vector2(0.5f, 0f), new Vector2(WindowWidth - 96f, 32f), new Vector2(0f, 52f));
        }

        private void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool selected = i == settings.SelectedIndex;
                bool muted = settings.IsMuted(row.Channel);
                float volume = settings.VolumeOf(row.Channel);

                row.Plate.color = selected
                    ? new Color(0.16f, 0.21f, 0.40f, 0.13f)
                    : Color.clear;

                row.Name.color = muted ? UiKit.DarkInkDim : UiKit.DarkInk;

                RectTransform fillRoot = (RectTransform)row.TrackFill.transform.parent;
                fillRoot.sizeDelta = new Vector2(TrackWidth * volume, 0f);
                row.TrackFill.color = muted
                    ? new Color(0.55f, 0.57f, 0.64f, 0.9f)
                    : (selected ? UiKit.PanelBlue : new Color(0.35f, 0.40f, 0.58f, 0.92f));

                row.Readout.text = $"{Mathf.RoundToInt(volume * 100f)}%   {Mixer.Decibels(volume)}";

                float final = settings.FinalVolumeOf(row.Channel);
                row.Final.text = $"plays at {Mathf.RoundToInt(final * 100f)}%";
                row.Final.color = Mathf.Abs(final - volume) > 0.005f
                    ? new Color(0.68f, 0.36f, 0.10f)
                    : UiKit.DarkInkDim;

                bool heldByPause = GameAudio.Mixer.IsPaused(row.Channel);

                if (!row.Channel.IsBus)
                {
                    row.Mute.text = "—";
                    row.Mute.color = UiKit.DarkInkDim;
                }
                else if (muted)
                {
                    row.Mute.text = "MUTED";
                    row.Mute.color = UiKit.Failure;
                }
                else if (heldByPause)
                {
                    row.Mute.text = "HELD";
                    row.Mute.color = new Color(0.68f, 0.36f, 0.10f);
                }
                else
                {
                    row.Mute.text = "♪";
                    row.Mute.color = UiKit.DarkInkDim;
                }
            }

            RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            MixerChannel channel = settings.Selected;
            MixerReading reading = GameAudio.Mixer.Read(channel);

            if (!reading.Resolved)
            {
                diagnostics.text =
                    $"<b>{channel.Path}</b> is not in any loaded bank, so this slider moves nothing.\n" +
                    "Build the Studio project and import its banks to bring the row to life.";
                diagnostics.color = UiKit.Failure;
                return;
            }

            diagnostics.color = UiKit.DarkInk;

            if (!channel.IsBus)
            {
                diagnostics.text =
                    $"<b>{reading.Path}</b>   ·   {reading.Guid}\n" +
                    $"volume {reading.Volume:0.00}   ·   final {reading.FinalVolume:0.00}\n" +
                    "A VCA is a gain and nothing else — no mute, no pause, no channel group, no port.";
                return;
            }

            string group = reading.ChannelGroupLive ? "live" : "none (nothing playing)";
            string port = reading.PortIndexSupported ? reading.PortIndex.ToString() : "not supported on this platform";

            diagnostics.text =
                $"<b>{reading.Path}</b>   ·   {reading.Guid}\n" +
                $"volume {reading.Volume:0.00}   ·   final {reading.FinalVolume:0.00}   ·   " +
                $"{(reading.Muted ? "muted" : "audible")}   ·   {(reading.Paused ? "paused" : "running")}\n" +
                $"channel group {group}   ·   CPU {reading.CpuExclusive}/{reading.CpuInclusive} µs   ·   " +
                $"memory {reading.MemoryInclusive / 1024f:0.0} KB ({reading.MemorySampleData / 1024f:0.0} KB samples)   ·   " +
                $"port {port}\n" +
                DescribeAudition();
        }

        private void Update()
        {
            navCooldown -= Time.unscaledDeltaTime;
            diagnosticsCooldown -= Time.unscaledDeltaTime;

            HandleNavigation();
            HandleKeys();

            ReleaseFinishedAudition();

            if (diagnosticsCooldown <= 0f)
            {
                diagnosticsCooldown = DiagnosticsInterval;
                RefreshDiagnostics();
            }
        }

        private void HandleNavigation()
        {
            Vector2 direction = ReadNavigation();

            if (direction == Vector2.zero)
            {
                navCooldown = 0f;
                lastDirection = Vector2.zero;
                return;
            }

            bool fresh = direction != lastDirection;
            lastDirection = direction;

            if (!fresh && navCooldown > 0f)
            {
                return;
            }

            navCooldown = fresh ? RepeatDelay : RepeatRate;

            if (direction.y != 0f)
            {
                if (settings.Navigate(direction.y > 0f ? -1 : 1))
                {
                    Refresh();
                    GameAudio.Play(Sfx.MenuMove);
                }

                return;
            }

            if (settings.Nudge(direction.x > 0f ? 1 : -1))
            {
                Refresh();

                Audition(settings.Selected);
            }
        }

        private static Vector2 ReadNavigation()
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

            if (direction.x != 0f && direction.y != 0f)
            {
                direction.x = 0f;
            }

            return direction;
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool mute = keyboard != null && keyboard.mKey.wasPressedThisFrame;
            if (!mute && gamepad != null)
            {
                mute = gamepad.buttonWest.wasPressedThisFrame;
            }

            if (mute)
            {
                GameAudio.Play(settings.ToggleMute() ? Sfx.MenuToggle : Sfx.MenuBack);
                Refresh();
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                if (settings.Reset())
                {
                    GameAudio.Play(Sfx.MenuToggle);
                }

                Refresh();
            }

            bool back = keyboard != null
                        && (keyboard.escapeKey.wasPressedThisFrame
                            || keyboard.backspaceKey.wasPressedThisFrame);

            if (!back && gamepad != null)
            {
                back = gamepad.buttonEast.wasPressedThisFrame;
            }

            if (back)
            {
                GameAudio.Play(Sfx.MenuBack);
                Close();
            }
        }

        private void Audition(MixerChannel channel)
        {
            StopAudition();

            auditionedChannel = channel;
            audition = GameAudio.Loop(Mixer.AuditionFor(channel));
        }

        private void StopAudition()
        {
            if (audition.IsValid)
            {
                GameAudio.Stop(ref audition);
            }
        }

        private void ReleaseFinishedAudition()
        {
            if (audition.IsValid && GameAudio.StateOf(audition) == AudioPlayback.Stopped)
            {
                GameAudio.Stop(ref audition);
            }
        }

        private string DescribeAudition()
        {
            if (!audition.IsValid)
            {
                return "nothing auditioned yet — nudge a slider";
            }

            AudioPlayback state = GameAudio.StateOf(audition);
            string where = auditionedChannel.IsValid ? auditionedChannel.DisplayName : "?";

            return state == AudioPlayback.Stopped
                ? $"last audition on {where} has finished"
                : $"auditioning {where} — {state.ToString().ToLowerInvariant()}";
        }

        private void HoldChannelGroups()
        {
            holdingChannelGroups = true;

            for (int i = 0; i < Mixer.Buses.Count; i++)
            {
                GameAudio.Mixer.HoldChannelGroup(Mixer.Buses[i], true);
            }
        }

        private void ReleaseChannelGroups()
        {
            if (!holdingChannelGroups)
            {
                return;
            }

            holdingChannelGroups = false;

            for (int i = 0; i < Mixer.Buses.Count; i++)
            {
                GameAudio.Mixer.HoldChannelGroup(Mixer.Buses[i], false);
            }
        }
    }
}
