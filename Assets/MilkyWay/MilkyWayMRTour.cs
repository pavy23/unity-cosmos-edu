using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager

namespace MilkyWay
{
    /// <summary>
    /// The guided tour, MR edition. The desktop tour's camera IS its demo —
    /// every step flies to an authored viewpoint. In MR the headset owns the
    /// camera, so the pointing is inverted: the miniature stays put and a
    /// pulsing ring parented to it marks the feature being narrated, while
    /// the same voiced lines (subtitle == voice, shared clips) play and the
    /// controller turns up the feature it is talking about. Steps advance on
    /// narration end; the card also offers prev/next/stop for hand rays.
    /// </summary>
    public class MilkyWayMRTour : MonoBehaviour
    {
        public MilkyWayController controller;
        public MilkyWayMRStage stage;
        public MilkyWayMRControls controls;

        public bool Running { get; private set; }

        int step;
        Coroutine advance;
        float baseHii, baseDust;
        bool labelsWereOn;

        RectTransform card;
        Text cardTitle, cardBody, cardFooter;
        Text prevLabel, nextLabel, stopLabel;

        struct MRStep
        {
            public Vector3 center;   // galaxy-local kpc
            public float radius;     // highlight ring radius, kpc (0 = none)
            public float hiiMul, dustMul;
            public bool gold;        // sun step: gold ring instead of cyan
        }

        // Same seven beats as the desktop tour (shared narration clips
        // mw_tour_0..6); the viewpoints become highlight targets.
        static readonly MRStep[] Steps =
        {
            new MRStep { },                                                          // 0 overview
            new MRStep { center = Vector3.zero, radius = 3.5f },                     // 1 bulge & bar
            new MRStep { center = Vector3.zero, radius = 10f },                      // 2 spiral arms
            new MRStep { center = Vector3.zero, radius = 6.5f, dustMul = 1.4f },     // 3 dust lanes
            new MRStep { center = new Vector3(4.5f, 0f, -3.5f), radius = 2f,
                         hiiMul = 2.2f },                                            // 4 nurseries
            new MRStep { center = new Vector3(8.2f, 0.02f, 0f), radius = 2f,
                         gold = true },                                              // 5 the Sun
            new MRStep { center = Vector3.zero, radius = 14f },                      // 6 halo
        };

        static readonly Color RingCyan = new Color(0.55f, 1.35f, 1.7f, 1f);
        static readonly Color RingGold = new Color(1.7f, 1.25f, 0.55f, 1f);

        void Start()
        {
            var keys = new string[MilkyWayTour.NarrationLines.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = "mw_tour_" + i;
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
            if (controller != null) { baseHii = controller.hiiStrength; baseDust = controller.dustStrength; }
            if (stage != null)
            {
                labelsWereOn = stage.LabelsVisible;
                stage.SetLabelsVisible(false); // tags fight the single highlight for attention
            }
            if (controls != null) controls.SetMenuVisible(false); // card owns the bottom strip
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
            }
            if (controller != null)
            {
                controller.hiiStrength = baseHii;
                controller.dustStrength = baseDust;
                controller.Apply();
            }
            if (controls != null) controls.SetMenuVisible(true);
        }

        public void Next() { if (Running && step < Steps.Length - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        public void OnLanguageChanged() { if (Running) ApplyStep(); }

        void ApplyStep()
        {
            var s = Steps[step];

            if (stage != null)
            {
                if (s.radius > 0f) stage.SetHighlight(s.center, s.radius, s.gold ? RingGold : RingCyan);
                else stage.ClearHighlight();
            }

            if (controller != null)
            {
                controller.hiiStrength = baseHii * (s.hiiMul > 0f ? s.hiiMul : 1f);
                controller.dustStrength = baseDust * (s.dustMul > 0f ? s.dustMul : 1f);
                controller.Apply();
            }

            float len = Application.isPlaying
                ? NarrationManager.Instance.Play("mw_tour_" + step) : 4f;

            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = MilkyWayTour.StepTitle(step);
            cardBody.text = Loc.T(MilkyWayTour.NarrationLines[step],
                                  MilkyWayTour.NarrationLinesEn[step],
                                  MilkyWayTour.NarrationLinesJa[step],
                                  MilkyWayTour.NarrationLinesZh[step]);
            cardFooter.text = (step + 1) + " / " + Steps.Length;
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
            if (step < Steps.Length - 1) { step++; ApplyStep(); }
            else StopTour(); // the last line ends on "explore freely" — hand it over
        }

        // ---------------- card UI (shared factory, world canvas) -------------

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(Camera.main);

            // Grows upward in MR with the text: bottom-pivoted, so the nav
            // buttons stay put on the bottom edge and the extra height comes
            // out of empty room above.
            card = BlackHoleUI.MakePanel(canvas.transform, "MW MR Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f),
                new Vector2(1100f, BlackHoleUI.ReadingY(250f)));

            cardTitle = BlackHoleUI.MakeText(card, "Title", BlackHoleUI.ReadingSize(28), BlackHoleUI.TitleGold,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-18f)), new Vector2(860f, BlackHoleUI.ReadingY(36f)),
                FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", BlackHoleUI.ReadingSize(21), BlackHoleUI.TextPrimary,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, BlackHoleUI.ReadingY(-62f)), new Vector2(1044f, BlackHoleUI.ReadingY(150f)));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardFooter = BlackHoleUI.MakeText(card, "Footer", BlackHoleUI.ReadingSize(16), BlackHoleUI.TextSecondary,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 12f), new Vector2(300f, BlackHoleUI.ReadingY(24f)));

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
