using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Presentation
{
    public sealed class ScreenFader : MonoBehaviour
    {
        private Image curtain;

        public static ScreenFader Create(Transform parent)
        {
            Canvas canvas = UiKit.CreateCanvas("ScreenFader", 1000, parent);
            ScreenFader fader = canvas.gameObject.AddComponent<ScreenFader>();

            fader.curtain = UiKit.Panel(canvas.transform, "Curtain", new Color(0.03f, 0.03f, 0.05f, 0f));
            fader.curtain.Rt().Stretch();
            fader.curtain.raycastTarget = false;

            return fader;
        }

        public bool IsOpaque => curtain != null && curtain.color.a >= 0.999f;

        public IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (curtain == null)
            {
                yield break;
            }

            Color color = curtain.color;
            float startAlpha = color.a;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                color.a = Mathf.Lerp(startAlpha, targetAlpha, t * t * (3f - 2f * t));
                curtain.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            curtain.color = color;
        }

        public void SetAlpha(float alpha)
        {
            if (curtain == null)
            {
                return;
            }

            Color color = curtain.color;
            color.a = Mathf.Clamp01(alpha);
            curtain.color = color;
        }
    }
}
