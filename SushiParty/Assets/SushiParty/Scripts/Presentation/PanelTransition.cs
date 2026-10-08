using UnityEngine;

namespace SushiParty.Presentation
{
    public sealed class PanelTransition
    {
        private const float AppearDuration = 0.28f;
        private const float DismissDuration = 0.13f;
        private const float SwellFraction = 0.6f;
        private const float HiddenScale = 0.82f;
        private const float PeakScale = 1.07f;
        private const float DismissScale = 0.9f;

        private enum State
        {
            Hidden,
            Appearing,
            Shown,
            Dismissing,
        }

        private readonly RectTransform panel;
        private readonly RectTransform scaled;
        private readonly CanvasGroup group;
        private State state;
        private float timer;
        private float fromScale;
        private float fromAlpha;

        private PanelTransition(RectTransform panel, RectTransform scaled)
        {
            this.panel = panel;
            this.scaled = scaled;

            group = panel.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = panel.gameObject.AddComponent<CanvasGroup>();
            }

            state = panel.gameObject.activeSelf ? State.Shown : State.Hidden;
        }

        public static PanelTransition For(RectTransform panel)
        {
            return new PanelTransition(panel, panel);
        }

        public static PanelTransition For(RectTransform panel, RectTransform scaled)
        {
            return new PanelTransition(panel, scaled);
        }

        public bool Visible => state != State.Hidden;
        public bool Settled => state == State.Shown || state == State.Hidden;

        public void Show()
        {
            if (state == State.Appearing || state == State.Shown)
            {
                return;
            }

            fromScale = state == State.Hidden ? HiddenScale : scaled.localScale.x;
            fromAlpha = state == State.Hidden ? 0f : group.alpha;
            timer = 0f;
            state = State.Appearing;

            panel.gameObject.SetActive(true);
            Apply(fromScale, fromAlpha);
        }

        public void Hide()
        {
            if (state == State.Hidden || state == State.Dismissing)
            {
                return;
            }

            fromScale = scaled.localScale.x;
            fromAlpha = group.alpha;
            timer = 0f;
            state = State.Dismissing;
        }

        public void SnapHidden()
        {
            state = State.Hidden;
            timer = 0f;
            Apply(HiddenScale, 0f);
            panel.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (state == State.Hidden || state == State.Shown)
            {
                return;
            }

            timer += deltaTime;

            if (state == State.Appearing)
            {
                float t = Mathf.Clamp01(timer / AppearDuration);
                Apply(AppearScale(t), Mathf.Lerp(fromAlpha, 1f, Mathf.Clamp01(t / SwellFraction)));

                if (t >= 1f)
                {
                    state = State.Shown;
                    Apply(1f, 1f);
                }

                return;
            }

            float d = Mathf.Clamp01(timer / DismissDuration);

            float eased = d * d;
            Apply(Mathf.Lerp(fromScale, DismissScale, eased), Mathf.Lerp(fromAlpha, 0f, eased));

            if (d >= 1f)
            {
                SnapHidden();
            }
        }

        private float AppearScale(float t)
        {
            if (t < SwellFraction)
            {
                float u = t / SwellFraction;

                return Mathf.Lerp(fromScale, PeakScale, 1f - (1f - u) * (1f - u));
            }

            float v = (t - SwellFraction) / (1f - SwellFraction);
            return Mathf.Lerp(PeakScale, 1f, v * v * (3f - 2f * v));
        }

        private void Apply(float scale, float alpha)
        {
            scaled.localScale = new Vector3(scale, scale, 1f);
            group.alpha = alpha;
        }
    }
}
