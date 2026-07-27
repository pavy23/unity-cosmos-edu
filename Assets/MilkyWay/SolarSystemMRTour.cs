using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager

namespace MilkyWay
{
    /// <summary>
    /// The planet tour, MR edition: nine narrated stops (Sun → Neptune) that
    /// ring each body in place instead of flying a camera at it. Shares the
    /// desktop tour's stops, facts and voiced lines (subtitle == voice, same
    /// clips). Orbits slow to the desktop tour's parked pace so the ringed
    /// planet stays where the visitor just looked. The last stop's narration
    /// promises a return "to where we can see the galaxy" — here that promise
    /// is kept literally: the tour ends by walking the visitor over to the MR
    /// galaxy exhibit.
    /// </summary>
    public class SolarSystemMRTour : MonoBehaviour
    {
        public SolarSystemMRStage stage;
        public SolarSystemMRControls controls;

        [Tooltip("Orbit/spin speed while touring (desktop tour uses 0.06).")]
        public float tourMotionScale = 0.06f;

        public bool Running { get; private set; }

        int step;
        Coroutine advance;
        bool labelsWereOn;

        RectTransform card;
        Text cardTitle, cardBody, cardFacts, cardFooter;
        Text prevLabel, nextLabel, stopLabel;

        void Start()
        {
            var keys = new string[SolarSystemTour.StopCount];
            for (int i = 0; i < keys.Length; i++) keys[i] = "mw_sol_" + i;
            NarrationManager.Instance.Preload(keys);
        }

        public void Toggle()
        {
            if (Running) StopTour(); else StartTour();
        }

        public void StartTour()
        {
            if (Running || stage == null) return;
            // A planet is up close and the orrery is faded out — there is
            // nothing for the tour's highlight ring to point at.
            if (controls != null && controls.Focus != null && controls.Focus.Focused) return;
            Running = true;
            step = 0;
            labelsWereOn = stage.LabelsVisible;
            stage.SetLabelsVisible(false); // the highlight ring owns attention; the card names the body
            stage.SetMotionScale(tourMotionScale);
            if (controls != null) controls.SetMenuVisible(false);
            ApplyStep();
        }

        public void StopTour()
        {
            if (!Running) return;
            Running = false;
            if (advance != null) { StopCoroutine(advance); advance = null; }
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            if (stage != null)
            {
                stage.ClearHighlight();
                stage.SetLabelsVisible(labelsWereOn);
                stage.SetMotionScale(stage.ambientMotionScale);
            }
            if (controls != null) controls.SetMenuVisible(true);
        }

        public void Next() { if (Running && step < SolarSystemTour.StopCount - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        public void OnLanguageChanged() { if (Running) ApplyStep(); }

        void ApplyStep()
        {
            stage.SetHighlight(SolarSystemTour.StopBody(step));

            float len = Application.isPlaying
                ? NarrationManager.Instance.Play("mw_sol_" + step) : 4f;

            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = SolarSystemTour.StopTitle(step);
            cardBody.text = Loc.T(SolarSystemTour.NarrationLines[step],
                                  SolarSystemTour.NarrationLinesEn[step],
                                  SolarSystemTour.NarrationLinesJa[step],
                                  SolarSystemTour.NarrationLinesZh[step]);
            cardFacts.text = SolarSystemTour.FactLine(step);
            cardFooter.text = (step + 1) + " / " + SolarSystemTour.StopCount;
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
            if (step < SolarSystemTour.StopCount - 1) { step++; ApplyStep(); }
            else
            {
                // Ends where it started, in this room. The last line used to
                // hand the visitor straight to the galaxy exhibit, but with the
                // title screen as the only hub an exhibit that walks off to
                // another exhibit on its own is a scene change nobody asked for.
                StopTour();
            }
        }

        // ---------------- card UI (shared factory, world canvas) -------------

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);

            // Bottom-pivoted so MR's taller reading text grows the card upward,
            // away from the nav buttons on its bottom edge.
            card = BlackHoleUI.MakePanel(canvas.transform, "Solar MR Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f),
                new Vector2(1100f, BlackHoleUI.ReadingY(274f)));

            cardTitle = BlackHoleUI.MakeText(card, "Title", BlackHoleUI.ReadingSize(28), BlackHoleUI.TitleGold,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-18f)), new Vector2(860f, BlackHoleUI.ReadingY(36f)),
                FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", BlackHoleUI.ReadingSize(21), BlackHoleUI.TextPrimary,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-60f)), new Vector2(1044f, BlackHoleUI.ReadingY(132f)));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Facts and footer stack up from the bottom edge, so their offsets
            // grow together with the text or the two rows close on each other.
            cardFacts = BlackHoleUI.MakeText(card, "Facts", BlackHoleUI.ReadingSize(17), BlackHoleUI.TitleGold,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, BlackHoleUI.ReadingY(40f)), new Vector2(1044f, BlackHoleUI.ReadingY(26f)));

            cardFooter = BlackHoleUI.MakeText(card, "Footer", BlackHoleUI.ReadingSize(16), BlackHoleUI.TextSecondary,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 12f), new Vector2(300f, BlackHoleUI.ReadingY(24f)));

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
