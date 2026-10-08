using System.Text;
using SushiParty.Board;
using SushiParty.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Presentation
{
    public sealed class BoardHud : MonoBehaviour
    {
        private const float ColumnEdge = 48f;
        private const float CardWidth = 396f;
        private const float CardHeight = 172f;
        private const float PairWidth = 452f;
        private const float PairHeight = 500f;
        private const float PairTop = 44f;
        private const float CardInset = 28f;
        private const float ChefGap = 56f;
        private const float CombinedHeight = 76f;
        private const int MaxChoices = 4;
        private const float ChoiceWidth = 920f;
        private const float ChoiceHeight = 250f;
        private const float ChoiceBottom = 120f;
        private const float ChoiceMargin = 40f;
        private const float ChoiceOptionWidth = 400f;
        private const float ChoiceOptionHeight = 132f;
        private const float ChoiceOptionGap = 24f;
        private const float ChoiceOptionBottom = 32f;
        private const float ChoiceOptionLift = 1.06f;
        private const float ContinueWidth = 620f;
        private const float ContinueHeight = 60f;
        private const float ContinueBottom = 128f;
        private const float ContinuePulseCycle = 1.15f;
        private const float ContinuePulseSwell = 0.035f;
        private const float ContinueBob = 5f;
        private static readonly Color Gold = new Color(0.98f, 0.80f, 0.28f);

        private sealed class TokenCard
        {
            public Image Edge;
            public Image Body;
            public Image Plate;
            public Text Coins;
            public Text Golden;
        }

        private sealed class ChoiceOption
        {
            public RectTransform Root;
            public Image Edge;
            public Image Body;
            public Text Label;
        }

        private readonly TokenCard[] cards = new TokenCard[3];
        private readonly ChoiceOption[] choiceOptions = new ChoiceOption[MaxChoices];
        private readonly StringBuilder builder = new StringBuilder();
        private Text roundText;
        private Text turnText;
        private Text combinedText;
        private RectTransform choicePanel;
        private Text choiceCaption;
        private Color turnAccent = UiKit.PartyBlue;
        private RectTransform bannerHolder;
        private Text bannerText;
        private RectTransform promptPill;
        private Text promptText;
        private RectTransform continueHolder;
        private RectTransform continuePill;
        private Text continueText;
        private float continuePulse;
        private RectTransform resultsPanel;
        private RectTransform resultsCard;
        private Text resultsTitle;
        private Text resultsDetail;
        private Text resultsPrompt;
        private PanelTransition bannerTransition;
        private PanelTransition promptTransition;
        private PanelTransition continueTransition;
        private PanelTransition choiceTransition;
        private PanelTransition resultsTransition;
        private PanelTransition[] transitions;

        public static BoardHud Create(BoardSession session)
        {
            Canvas canvas = UiKit.CreateCanvas("BoardHud", 100);
            BoardHud hud = canvas.gameObject.AddComponent<BoardHud>();
            hud.Build(canvas.transform, session);
            return hud;
        }

        private void Build(Transform root, BoardSession session)
        {
            BuildHeader(root);
            BuildPair(root, session);
            BuildChef(root, session);
            BuildBanner(root);
            BuildPrompt(root);
            BuildContinue(root);
            BuildChoice(root);

            BuildResults(root);

            BuildTransitions();

            SetRound(session.Round, session.TotalRounds);
            RefreshTotals(session);

            BoardToken up = session.TokenFor(session.Turn);
            SetTurn(up.Seat, up.DisplayName, ColorFor(up.Seat));
        }

        private void BuildTransitions()
        {
            bannerTransition = PanelTransition.For(bannerHolder);
            promptTransition = PanelTransition.For(promptPill);
            continueTransition = PanelTransition.For(continueHolder);
            choiceTransition = PanelTransition.For(choicePanel);
            resultsTransition = PanelTransition.For(resultsPanel, resultsCard);

            transitions = new[]
            {
                bannerTransition,
                promptTransition,
                continueTransition,
                choiceTransition,
                resultsTransition,
            };

            for (int i = 0; i < transitions.Length; i++)
            {
                transitions[i].SnapHidden();
            }
        }

        private void Update()
        {
            if (transitions == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            for (int i = 0; i < transitions.Length; i++)
            {
                transitions[i].Tick(deltaTime);
            }

            TickContinuePulse(deltaTime);
        }

        private void TickContinuePulse(float deltaTime)
        {
            if (!continueTransition.Visible)
            {
                return;
            }

            continuePulse += deltaTime;

            float wave = Mathf.Sin(continuePulse * Mathf.PI * 2f / ContinuePulseCycle);

            float swell = 1f + (wave * 0.5f + 0.5f) * ContinuePulseSwell;

            continuePill.localScale = new Vector3(swell, swell, 1f);
            continuePill.anchoredPosition = new Vector2(0f, wave * ContinueBob);
        }

        private void BuildHeader(Transform root)
        {
            roundText = UiKit.Label(root, string.Empty, 44, TextAnchor.UpperLeft, UiKit.Ink, FontStyle.Bold).Chunky(3f);
            roundText.Rt().Pin(new Vector2(0f, 1f), new Vector2(560f, 56f), new Vector2(ColumnEdge, -36f));

            turnText = UiKit.Label(root, string.Empty, 34, TextAnchor.UpperLeft, UiKit.Ink, FontStyle.Bold).Chunky(2.6f);
            turnText.Rt().Pin(new Vector2(0f, 1f), new Vector2(620f, 48f), new Vector2(ColumnEdge, -100f));
        }

        private void BuildPair(Transform root, BoardSession session)
        {
            RectTransform frame = UiKit.Rect(root, "Pair");
            frame.Pin(new Vector2(1f, 1f), new Vector2(PairWidth, PairHeight), new Vector2(-ColumnEdge, -PairTop));

            UiKit.Window(frame, UiKit.PanelBlue);
            UiKit.Banner(frame, "THE PAIR", UiKit.DarkInk, 28, 300f);

            BuildTokenCard(frame, BoardSeat.One, session, new Vector2(0.5f, 1f), new Vector2(0f, -34f));
            BuildTokenCard(frame, BoardSeat.Two, session, new Vector2(0.5f, 1f), new Vector2(0f, -34f - CardHeight - 10f));

            Image strip = UiKit.RoundedPanel(frame, "Combined", Gold);
            strip.Rt().Pin(new Vector2(0.5f, 0f), new Vector2(CardWidth, CombinedHeight), new Vector2(0f, 22f));

            UiKit.Label(strip.Rt(), "COMBINED GOLDEN COINS", 21, TextAnchor.MiddleLeft, UiKit.DarkInk, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 0.5f), new Vector2(290f, 30f), new Vector2(26f, 0f));

            combinedText = UiKit.Label(strip.Rt(), "0", 46, TextAnchor.MiddleRight, UiKit.DarkInk, FontStyle.Bold);
            combinedText.Rt().Pin(new Vector2(1f, 0.5f), new Vector2(120f, 60f), new Vector2(-26f, 0f));
        }

        private void BuildChef(Transform root, BoardSession session)
        {
            float top = PairTop + PairHeight + ChefGap;

            UiKit.Label(root, "RIVAL", 20, TextAnchor.LowerLeft, UiKit.InkDim, FontStyle.Bold)
                .Chunky(1.8f)
                .Rt().Pin(new Vector2(1f, 1f), new Vector2(CardWidth, 28f), new Vector2(-(ColumnEdge + CardInset), -(top - 34f)));

            BuildTokenCard(root, BoardSeat.Chef, session, new Vector2(1f, 1f),
                new Vector2(-(ColumnEdge + CardInset), -top));
        }

        private void BuildTokenCard(Transform parent, BoardSeat seat, BoardSession session, Vector2 anchor, Vector2 offset)
        {
            Color color = ColorFor(seat);
            RectTransform card = UiKit.Rect(parent, $"Card_{seat}");
            card.Pin(anchor, new Vector2(CardWidth, CardHeight), offset);

            UiKit.Window(card, UiKit.Cream, edge: 5f, shadow: 8f);

            Image plate = UiKit.RoundedPanel(card, "NamePlate", color);
            plate.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(CardWidth - 24f, 44f), new Vector2(0f, -12f));

            UiKit.Label(plate.Rt(), session.TokenFor(seat).DisplayName, 26, TextAnchor.MiddleCenter, UiKit.Ink, FontStyle.Bold)
                .Chunky(2.2f)
                .Rt().Stretch(6f);

            UiKit.Disc(card, "Pearl", Gold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(26f, -70f));

            UiKit.Label(card, "GOLDEN COINS", 19, TextAnchor.UpperLeft, UiKit.DarkInkDim, FontStyle.Bold)
                .Rt().Pin(new Vector2(0f, 1f), new Vector2(220f, 24f), new Vector2(54f, -66f));

            Text golden = UiKit.Label(card, "0", 60, TextAnchor.UpperLeft, UiKit.DarkInk, FontStyle.Bold);
            golden.Rt().Pin(new Vector2(0f, 1f), new Vector2(220f, 72f), new Vector2(24f, -90f));

            UiKit.Label(card, "COINS", 19, TextAnchor.UpperRight, UiKit.DarkInkDim, FontStyle.Bold)
                .Rt().Pin(new Vector2(1f, 1f), new Vector2(150f, 24f), new Vector2(-26f, -66f));

            Text coins = UiKit.Label(card, "0", 36, TextAnchor.UpperRight, UiKit.DarkInk, FontStyle.Bold);
            coins.Rt().Pin(new Vector2(1f, 1f), new Vector2(150f, 48f), new Vector2(-26f, -92f));

            cards[(int)seat] = new TokenCard
            {
                Edge = card.Find("Edge").GetComponent<Image>(),
                Body = card.Find("Body").GetComponent<Image>(),
                Plate = plate,
                Coins = coins,
                Golden = golden,
            };
        }

        private void BuildBanner(Transform root)
        {
            bannerHolder = UiKit.Rect(root, "Banner");

            bannerHolder.Pin(new Vector2(0.5f, 0.5f), new Vector2(1200f, 200f), new Vector2(0f, -210f));

            UiKit.Panel(bannerHolder, "Backdrop", new Color(0f, 0f, 0f, 0.45f)).Rt().Stretch();

            bannerText = UiKit.Label(bannerHolder, string.Empty, 96, TextAnchor.MiddleCenter, UiKit.Ink, FontStyle.Bold).Chunky(5f);
            bannerText.Rt().Stretch();

            bannerHolder.gameObject.SetActive(false);
        }

        private void BuildPrompt(Transform root)
        {
            promptText = UiKit.Pill(root, string.Empty, UiKit.PartyBlue, UiKit.Ink, 28, new Vector2(880f, 60f));
            promptPill = promptText.Rt().parent.GetComponent<RectTransform>();
            promptPill.Pin(new Vector2(0.5f, 0f), new Vector2(880f, 60f), new Vector2(0f, 44f));

            promptPill.gameObject.SetActive(false);
        }

        private void BuildContinue(Transform root)
        {
            continueHolder = UiKit.Rect(root, "Continue");
            continueHolder.Pin(new Vector2(0.5f, 0f), new Vector2(ContinueWidth, ContinueHeight), new Vector2(0f, ContinueBottom));

            continueText = UiKit.Pill(continueHolder, string.Empty, UiKit.PartyBlue, UiKit.Ink, 28,
                new Vector2(ContinueWidth, ContinueHeight));

            continuePill = continueText.Rt().parent.GetComponent<RectTransform>();

            continuePill.Pin(new Vector2(0.5f, 0.5f), new Vector2(ContinueWidth, ContinueHeight), Vector2.zero);

            continueHolder.gameObject.SetActive(false);
        }

        private void BuildChoice(Transform root)
        {
            choicePanel = UiKit.Rect(root, "Choice");
            choicePanel.Pin(new Vector2(0.5f, 0f), new Vector2(ChoiceWidth, ChoiceHeight), new Vector2(0f, ChoiceBottom));

            UiKit.Window(choicePanel, UiKit.PanelBlue);
            UiKit.Banner(choicePanel, "WHICH WAY?", UiKit.DarkInk, 28, 340f);

            choiceCaption = UiKit.Label(choicePanel, string.Empty, 24, TextAnchor.UpperCenter, UiKit.InkDim);
            choiceCaption.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(ChoiceWidth - ChoiceMargin * 2f, 36f), new Vector2(0f, -26f));

            for (int i = 0; i < choiceOptions.Length; i++)
            {
                choiceOptions[i] = BuildChoiceOption(choicePanel, i);
            }

            choicePanel.gameObject.SetActive(false);
        }

        private ChoiceOption BuildChoiceOption(Transform parent, int index)
        {
            RectTransform card = UiKit.Rect(parent, $"Way_{index}");
            card.Pin(new Vector2(0.5f, 0f), new Vector2(ChoiceOptionWidth, ChoiceOptionHeight), new Vector2(0f, ChoiceOptionBottom));

            UiKit.Window(card, UiKit.Cream, edge: 5f, shadow: 8f);

            Text label = UiKit.Label(card, string.Empty, 25, TextAnchor.MiddleCenter, UiKit.DarkInk);
            label.Rt().Stretch(16f);

            card.gameObject.SetActive(false);

            return new ChoiceOption
            {
                Root = card,
                Edge = card.Find("Edge").GetComponent<Image>(),
                Body = card.Find("Body").GetComponent<Image>(),
                Label = label,
            };
        }

        private void BuildResults(Transform root)
        {
            resultsPanel = UiKit.Rect(root, "Results").Stretch();
            UiKit.Panel(resultsPanel, "Dim", UiKit.Backdrop).Rt().Stretch();

            RectTransform card = resultsCard = UiKit.Rect(resultsPanel, "Card");
            card.Pin(new Vector2(0.5f, 0.5f), new Vector2(1000f, 500f), Vector2.zero);
            UiKit.Window(card, UiKit.Cream);

            resultsTitle = UiKit.Label(card, string.Empty, 76, TextAnchor.UpperCenter, UiKit.DarkInk, FontStyle.Bold);
            resultsTitle.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(920f, 96f), new Vector2(0f, -44f));

            resultsDetail = UiKit.Label(card, string.Empty, 30, TextAnchor.UpperCenter, UiKit.DarkInkDim);
            resultsDetail.Rt().Pin(new Vector2(0.5f, 1f), new Vector2(900f, 220f), new Vector2(0f, -160f));

            resultsPrompt = UiKit.Pill(card, string.Empty, UiKit.PartyBlue, UiKit.Ink, 26, new Vector2(820f, 56f));
            resultsPrompt.Rt().parent.GetComponent<RectTransform>()
                .Pin(new Vector2(0.5f, 0f), new Vector2(820f, 56f), new Vector2(0f, 34f));

            resultsPanel.gameObject.SetActive(false);
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

        public void SetRound(int round, int totalRounds)
        {
            roundText.text = $"Round {round} / {totalRounds}";
        }

        public void SetTurn(BoardSeat seat, string displayName, Color color)
        {
            turnText.text = $"{displayName}'s turn";
            turnText.color = color;
            turnAccent = color;

            for (int i = 0; i < cards.Length; i++)
            {
                TokenCard card = cards[i];
                if (card == null)
                {
                    continue;
                }

                bool active = i == (int)seat;

                if (active)
                {
                    card.Plate.color = color;
                }

                card.Edge.color = active ? color : UiKit.WindowEdge;
                card.Body.color = active ? Color.Lerp(UiKit.Cream, color, 0.12f) : UiKit.Cream;
            }
        }

        public void RefreshTotals(BoardSession session)
        {
            int combined = 0;

            for (int i = 0; i < cards.Length; i++)
            {
                BoardToken token = session.TokenFor((BoardSeat)i);
                cards[i].Coins.text = token.Coins.ToString();
                cards[i].Golden.text = token.GoldenCoins.ToString();

                if (token.IsPlayer)
                {
                    combined += token.GoldenCoins;
                }
            }

            combinedText.text = combined.ToString();
        }

        public void ShowBanner(string text, Color? color = null)
        {
            bannerTransition.Show();
            bannerText.text = text;
            bannerText.color = color ?? UiKit.Ink;
        }

        public void HideBanner()
        {
            bannerTransition.Hide();
        }

        public void ShowPrompt(string text)
        {
            promptTransition.Show();
            promptText.text = text ?? string.Empty;
        }

        public void HidePrompt()
        {
            promptTransition.Hide();
        }

        public void ShowContinuePrompt(string text)
        {
            continueText.text = text ?? string.Empty;

            if (!continueTransition.Visible)
            {
                continuePulse = 0f;
            }

            continueTransition.Show();
        }

        public void HideContinuePrompt()
        {
            continueTransition.Hide();
        }

        public void ShowChoice(string prompt, string[] options, int highlighted)
        {
            int count = options == null ? 0 : Mathf.Min(options.Length, MaxChoices);
            if (count == 0)
            {
                HideChoice();
                return;
            }

            choiceTransition.Show();
            choiceCaption.text = prompt ?? string.Empty;

            int lit = Mathf.Clamp(highlighted, 0, count - 1);

            float span = ChoiceWidth - ChoiceMargin * 2f;
            float width = Mathf.Min(ChoiceOptionWidth, (span - (count - 1) * ChoiceOptionGap) / count);
            float pitch = width + ChoiceOptionGap;
            float first = -(count - 1) * pitch * 0.5f;

            for (int i = 0; i < choiceOptions.Length; i++)
            {
                ChoiceOption option = choiceOptions[i];
                bool used = i < count;

                option.Root.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                option.Root.sizeDelta = new Vector2(width, ChoiceOptionHeight);
                option.Root.anchoredPosition = new Vector2(first + i * pitch, ChoiceOptionBottom);
                option.Label.text = options[i] ?? string.Empty;

                bool active = i == lit;
                option.Edge.color = active ? turnAccent : UiKit.WindowEdge;
                option.Body.color = active ? Color.Lerp(UiKit.Cream, turnAccent, 0.14f) : UiKit.Cream;
                option.Root.localScale = Vector3.one * (active ? ChoiceOptionLift : 1f);
            }
        }

        public void HideChoice()
        {
            choiceTransition.Hide();
        }

        public void ShowResults(BoardResult result)
        {
            resultsTransition.Show();
            resultsTitle.text = result.PairWon ? "THE PAIR WINS!" : "CHEF TAKO WINS";
            resultsTitle.color = result.PairWon ? UiKit.Success : UiKit.Failure;

            builder.Length = 0;
            builder.AppendLine(result.PairWon
                ? "Golden Coins carried off the board, together."
                : "Chef Tako keeps the Golden Coins.");
            builder.AppendLine();
            builder.AppendLine($"Golden Coins — pair <b>{result.PairGoldenCoins}</b>  ·  Chef Tako <b>{result.ChefGoldenCoins}</b>");
            builder.AppendLine($"Coins — pair <b>{result.PairCoins}</b>  ·  Chef Tako <b>{result.ChefCoins}</b>");

            resultsDetail.text = builder.ToString();
            resultsPrompt.text = "Action button: back to the menu";
        }
    }
}
