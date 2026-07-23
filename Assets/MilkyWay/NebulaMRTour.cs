using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager

namespace MilkyWay
{
    /// <summary>
    /// "The Life of a Star", MR edition. The desktop tour glides a camera
    /// between specimens; here the miniature stays within reach and each step
    /// simply swaps which specimen it shows, while the same voiced lines play
    /// (shared neb_life_N clips, subtitle == voice). Steps advance when the
    /// narration ends; the card offers prev/next/stop for hand rays.
    /// </summary>
    public class NebulaMRTour : MonoBehaviour
    {
        public NebulaMRStage stage;
        public NebulaMRControls controls;

        public bool Running { get; private set; }

        int step;
        Coroutine advance;

        RectTransform card;
        Text cardTitle, cardBody, cardFooter;
        Text prevLabel, nextLabel, stopLabel;

        void Start()
        {
            var keys = new string[NebulaTour.StepCount];
            for (int i = 0; i < keys.Length; i++) keys[i] = "neb_life_" + i;
            NarrationManager.Instance.Preload(keys);
        }

        public void Toggle()
        {
            if (Running) StopTour(); else StartTour();
        }

        public void StartTour()
        {
            if (Running) return;
            Running = true;
            step = 0;
            if (controls != null) controls.SetMenuVisible(false); // card owns the strip
            ApplyStep();
        }

        public void StopTour()
        {
            if (!Running) return;
            Running = false;
            if (advance != null) { StopCoroutine(advance); advance = null; }
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            if (controls != null) controls.SetMenuVisible(true);
            // The stage stays on the specimen we ended at — free browsing
            // resumes right where the story left the visitor.
        }

        public void Next() { if (Running && step < NebulaTour.StepCount - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        public void OnLanguageChanged() { if (Running) ApplyStep(); }

        void ApplyStep()
        {
            if (stage != null) stage.Show(NebulaTour.StepSpecimen(step));

            float len = Application.isPlaying
                ? NarrationManager.Instance.Play("neb_life_" + step) : 4f;

            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = NebulaTour.StepTitle(step);
            cardBody.text = Loc.T(NebulaTour.NarrationLines[step],
                                  NebulaTour.NarrationLinesEn[step],
                                  NebulaTour.NarrationLinesJa[step],
                                  NebulaTour.NarrationLinesZh[step]);
            cardFooter.text = (step + 1) + " / " + NebulaTour.StepCount;
            if (prevLabel != null) prevLabel.text = Loc.T("이전", "Prev", "前へ", "上一步");
            if (nextLabel != null) nextLabel.text = Loc.T("다음", "Next", "次へ", "下一步");
            if (stopLabel != null) stopLabel.text = Loc.T("종료", "End", "終了", "结束");

            if (advance != null) StopCoroutine(advance);
            advance = StartCoroutine(AdvanceAfter(len + 2.2f));
        }

        IEnumerator AdvanceAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            advance = null;
            if (!Running) yield break;
            if (step < NebulaTour.StepCount - 1) { step++; ApplyStep(); }
            else StopTour(); // the cycle closes — hand the room back
        }

        // ---------------- card UI (shared factory, world canvas) -------------

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);

            card = BlackHoleUI.MakePanel(canvas.transform, "Nebula MR Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1100f, 250f));

            cardTitle = BlackHoleUI.MakeText(card, "Title", 28, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(860f, 36f), FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", 21, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -62f), new Vector2(1044f, 150f));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardFooter = BlackHoleUI.MakeText(card, "Footer", 16, BlackHoleUI.TextSecondary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 12f), new Vector2(300f, 24f));

            // Hand-ray transport: ≥3° targets (84 px on the 2.6 m frame).
            prevLabel = BlackHoleUI.MakeButton(card, "Tour Prev", "",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-320f, 14f), new Vector2(140f, 84f), Prev)
                .GetComponentInChildren<Text>();
            nextLabel = BlackHoleUI.MakeButton(card, "Tour Next", "",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-170f, 14f), new Vector2(140f, 84f), Next)
                .GetComponentInChildren<Text>();
            stopLabel = BlackHoleUI.MakeButton(card, "Tour Stop", "",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(140f, 84f), StopTour)
                .GetComponentInChildren<Text>();
        }

        void OnDisable()
        {
            if (Running) StopTour();
        }
    }
}
