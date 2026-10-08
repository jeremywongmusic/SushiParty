using System.Text;
using SushiParty.Core;
using SushiParty.InputLayer;
using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Presentation
{
    public sealed class MinigameHud : MonoBehaviour
    {
        private Text timerText;
        private Text objectiveText;
        private Text statusText;
        private Text bannerText;
        private Image bannerBackdrop;
        private RectTransform pipRow;
        private Image[] pips = new Image[0];
        private RectTransform briefingPanel;
        private RectTransform resultsPanel;
        private Text resultsTitle;
        private Text resultsDetail;
        private Text resultsPrompt;
        private Color accent = Color.white;

        public static MinigameHud Create(MinigameDefinition definition, MinigameContext context)
        {
            Canvas canvas = UiKit.CreateCanvas("MinigameHud", 100);
            MinigameHud hud = canvas.gameObject.AddComponent<MinigameHud>();
            hud.accent = definition.Accent;
            hud.Build(canvas.transform, definition, context);
            return hud;
        }

        private void Build(Transform root, MinigameDefinition definition, MinigameContext context)
        {
            RectTransform topBar = UiKit.Rect(root, "TopBar").Region(new Vector2(0f, 1f), new Vector2(1f, 1f));
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 120f);
            topBar.anchoredPosition = Vector2.zero;

            Text title = UiKit.Label(topBar, definition.DisplayName, 44, TextAnchor.UpperLeft, accent, FontStyle.Bold).Chunky(3f);
            title.Rt().Stretch().offsetMin = new Vector2(48f, 0f);

            timerText = UiKit.Label(topBar, "30", 64, TextAnchor.UpperRight, UiKit.Ink, FontStyle.Bold).Chunky(3.4f);
            timerText.Rt().Stretch().offsetMax = new Vector2(-48f, -4f);

            objectiveText = UiKit.Label(root, definition.Objective, 24, TextAnchor.UpperLeft, UiKit.InkDim).Chunky(1.8f);
            objectiveText.Rt().Region(new Vector2(0f, 1f), new Vector2(0.62f, 1f));
            objectiveText.Rt().pivot = new Vector2(0f, 1f);
            objectiveText.Rt().sizeDelta = new Vector2(0f, 90f);
            objectiveText.Rt().anchoredPosition = new Vector2(48f, -104f);

            pipRow = UiKit.Rect(root, "Pips");
            pipRow.Pin(new Vector2(0.5f, 1f), new Vector2(400f, 40f), new Vector2(0f, -24f));

            statusText = UiKit.Label(root, string.Empty, 28, TextAnchor.LowerCenter, UiKit.InkDim).Chunky(2f);
            statusText.Rt().Pin(new Vector2(0.5f, 0f), new Vector2(1400f, 80f), new Vector2(0f, 32f));

            BuildBanner(root);
            BuildBriefing(root, definition, context);
            BuildResults(root);
        }

        private void BuildBanner(Transform root)
        {
            RectTransform holder = UiKit.Rect(root, "Banner");
            holder.Pin(new Vector2(0.5f, 0.5f), new Vector2(1200f, 220f), Vector2.zero);

            bannerBackdrop = UiKit.Panel(holder, "Backdrop", new Color(0f, 0f, 0f, 0.45f));
            bannerBackdrop.Rt().Stretch();

            bannerText = UiKit.Label(holder, string.Empty, 120, TextAnchor.MiddleCenter, UiKit.Ink, FontStyle.Bold).Chunky(6f);
            bannerText.Rt().Stretch();

            holder.gameObject.SetActive(false);
        }

        private void BuildBriefing(Transform root, MinigameDefinition definition, MinigameContext context)
        {
            briefingPanel = UiKit.Rect(root, "Briefing").Stretch();
            UiKit.Panel(briefingPanel, "Dim", UiKit.Backdrop).Rt().Stretch();

            RectTransform card = UiKit.Rect(briefingPanel, "Card");
            card.Pin(new Vector2(0.5f, 0.5f), new Vector2(1160f, 580f), Vector2.zero);
            UiKit.Window(card, UiKit.Cream);

            UiKit.Banner(card, definition.DisplayName, accent, 46, 820f);

            UiKit.Label(card, definition.Objective, 30, TextAnchor.UpperCenter, UiKit.DarkInk)
                .Rt().Pin(new Vector2(0.5f, 1f), new Vector2(980f, 150f), new Vector2(0f, -60f));

            UiKit.Label(card, BuildSeatSummary(definition, context), 26, TextAnchor.UpperLeft, UiKit.DarkInkDim)
                .Rt().Pin(new Vector2(0.5f, 1f), new Vector2(980f, 240f), new Vector2(0f, -220f));

            UiKit.Pill(card, "Press your action button to start", UiKit.PartyBlue, UiKit.Ink, 26,
                    new Vector2(700f, 54f))
                .Rt().parent.GetComponent<RectTransform>()
                .Pin(new Vector2(0.5f, 0f), new Vector2(700f, 54f), new Vector2(0f, 30f));

            briefingPanel.gameObject.SetActive(false);
        }

        private static string BuildSeatSummary(MinigameDefinition definition, MinigameContext context)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>You are a team.</b> Both of you win together, or Chef Tako takes "
                          + MinigameLibrary.CoinStake + " Coins from each of you.");
            sb.AppendLine();

            foreach (Participant p in context.Participants)
            {
                string role = p.Slot == ParticipantSlot.One ? definition.RoleP1 : definition.RoleP2;
                string controls = p.Input is HumanParticipantInput human
                    ? human.DescribeControls()
                    : $"CPU partner ({p.Skill})";
                sb.AppendLine($"<b>{p.DisplayName}</b> — {role}");
                sb.AppendLine($"    {controls}");
            }

            return sb.ToString();
        }

        private void BuildResults(Transform root)
        {
            resultsPanel = UiKit.Rect(root, "Results").Stretch();
            UiKit.Panel(resultsPanel, "Dim", UiKit.Backdrop).Rt().Stretch();

            RectTransform card = UiKit.Rect(resultsPanel, "Card");
            card.Pin(new Vector2(0.5f, 0.5f), new Vector2(940f, 440f), Vector2.zero);
            UiKit.Window(card, UiKit.Cream);

            resultsTitle = UiKit.Label(card, string.Empty, 76, TextAnchor.UpperCenter, UiKit.DarkInk, FontStyle.Bold);
            resultsTitle.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(860f, 96f), new Vector2(0f, -44f));

            resultsDetail = UiKit.Label(card, string.Empty, 30, TextAnchor.UpperCenter, UiKit.DarkInkDim);
            resultsDetail.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(840f, 170f), new Vector2(0f, -160f));

            resultsPrompt = UiKit.Pill(card, string.Empty, UiKit.PartyBlue, UiKit.Ink, 26, new Vector2(780f, 56f));
            resultsPrompt.Rt().parent.GetComponent<RectTransform>()
                .Pin(new Vector2(0.5f, 0f), new Vector2(780f, 56f), new Vector2(0f, 34f));

            resultsPanel.gameObject.SetActive(false);
        }

        public void ConfigurePips(int count, string label = null)
        {
            foreach (Transform child in pipRow)
            {
                Destroy(child.gameObject);
            }

            pips = new Image[Mathf.Max(0, count)];
            const float size = 34f;
            const float gap = 14f;
            float totalWidth = count * size + Mathf.Max(0, count - 1) * gap;
            float startX = -totalWidth * 0.5f + size * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Image pip = UiKit.Panel(pipRow, $"Pip{i}", new Color(1f, 1f, 1f, 0.22f));
                pip.Rt().Pin(new Vector2(0.5f, 0.5f), new Vector2(size, size), new Vector2(startX + i * (size + gap), 0f));
                pips[i] = pip;
            }

            if (!string.IsNullOrEmpty(label))
            {
                UiKit.Label(pipRow, label, 20, TextAnchor.UpperCenter, UiKit.InkDim)
                    .Rt().Pin(new Vector2(0.5f, 0f), new Vector2(400f, 30f), new Vector2(0f, -6f));
            }
        }

        public void SetPips(int filled)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null)
                {
                    continue;
                }

                pips[i].color = i < filled ? accent : new Color(1f, 1f, 1f, 0.22f);
            }
        }

        public void SetTimer(float secondsRemaining)
        {
            float clamped = Mathf.Max(0f, secondsRemaining);
            timerText.text = clamped.ToString(clamped < 10f ? "0.0" : "0");
            timerText.color = clamped <= 5f ? new Color(1f, 0.42f, 0.36f) : UiKit.Ink;
        }

        public void SetStatus(string text)
        {
            statusText.text = text ?? string.Empty;
        }

        public void SetRoundChromeVisible(bool visible)
        {
            timerText.gameObject.SetActive(visible);
            pipRow.gameObject.SetActive(visible);
        }

        public void ShowBanner(string text, Color? color = null)
        {
            bannerText.transform.parent.gameObject.SetActive(true);
            bannerText.text = text;
            bannerText.color = color ?? UiKit.Ink;
            bannerBackdrop.color = new Color(0f, 0f, 0f, 0.45f);
        }

        public void HideBanner()
        {
            bannerText.transform.parent.gameObject.SetActive(false);
        }

        public void ShowBriefing(bool visible)
        {
            briefingPanel.gameObject.SetActive(visible);
        }

        public void ShowResults(MinigameOutcome outcome, bool canReturnToMenu, bool returnsToBoard)
        {
            resultsPanel.gameObject.SetActive(true);
            resultsTitle.text = outcome.Won ? "TEAM WINS!" : "CHEF TAKO WINS";
            resultsTitle.color = outcome.Won ? UiKit.Success : UiKit.Failure;

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(outcome.Detail))
            {
                sb.AppendLine(outcome.Detail);
            }

            if (outcome.CoinDelta != 0)
            {
                sb.AppendLine(outcome.CoinDelta > 0
                    ? $"+{outcome.CoinDelta} Coins each"
                    : $"{outcome.CoinDelta} Coins each");
            }

            if (outcome.Won && outcome.TimeRemaining > 0f)
            {
                sb.AppendLine($"{outcome.TimeRemaining:0.0}s to spare");
            }

            resultsDetail.text = sb.ToString();
            resultsPrompt.text = ResultsPrompt(canReturnToMenu, returnsToBoard);
        }

        public static string ResultsPrompt(bool canReturnToMenu, bool returnsToBoard)
        {
            if (returnsToBoard)
            {
                return "Action button: back to the board";
            }

            return canReturnToMenu
                ? "Action button: play again      ·      Esc: pause menu"
                : "Action button: play again";
        }

        public void HideResults()
        {
            resultsPanel.gameObject.SetActive(false);
        }
    }
}
