using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// One-shot explanation card (right edge of the screen): whenever a
    /// phenomenon demo is toggled on, this tells the user WHAT they are
    /// looking at — a bare key press should never leave them guessing.
    /// Suppressed while the guided tour or a cinematic is narrating.
    /// </summary>
    public static class ExplainCard
    {
        /// <summary>Set every frame by DesktopControls (tour/cinematic/immersive).</summary>
        public static bool Suppress;

        static RectTransform panel;
        static Text text;
        static Runner runner;

        class Runner : MonoBehaviour { }

        public static void Show(string title, string body, float seconds = 10f)
        {
            if (Suppress || !Application.isPlaying) return;
            if (panel == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(Camera.main);
                panel = BlackHoleUI.MakePanel(canvas.transform, "Explain Card",
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, -30f), new Vector2(470f, 150f));
                text = BlackHoleUI.MakeText(panel, "Text", 16, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(430f, 120f));
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            if (runner == null)
            {
                var go = new GameObject("Explain Card Runner") { hideFlags = HideFlags.DontSave };
                runner = go.AddComponent<Runner>();
            }

            text.text = "<color=#FFC46E><b>" + title + "</b></color>\n" + body;
            // Hug the content so short and long explanations both look right.
            float h = Mathf.Max(text.preferredHeight, 40f);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, h);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, h + 34f);
            panel.gameObject.SetActive(true);

            runner.StopAllCoroutines();
            runner.StartCoroutine(HideAfter(seconds));
        }

        public static void Hide()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        static System.Collections.IEnumerator HideAfter(float s)
        {
            yield return new WaitForSeconds(s);
            Hide();
        }
    }
}
