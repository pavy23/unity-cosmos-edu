using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, LanguageSelect

namespace MilkyWay
{
    /// <summary>
    /// Hand-ray menu for the MR solar-system exhibit: tour, time speed, labels,
    /// and the way back to the title. Also owns the tap-a-planet close-up
    /// (<see cref="SolarSystemMRFocus"/>), which is the exhibit's main verb —
    /// the menu is what you use when you are not holding a planet.
    /// </summary>
    public class SolarSystemMRControls : MonoBehaviour
    {
        public SolarSystemMRStage stage;
        public SolarSystemMRTour tour;

        SolarSystemMRFocus focus;

        /// <summary>The tap-a-planet close-up, once Start has built it. The
        /// tour asks so it can refuse to start over a held planet.</summary>
        public SolarSystemMRFocus Focus => focus;

        readonly List<(Text label, System.Func<string> text)> localized = new();
        readonly List<GameObject> menuRows = new();
        int locVersion = -1;

        static readonly float[] Speeds = { 0.25f, 1f, 4f };
        int speedIndex;

        const float ButtonW = 224f, ButtonH = 84f, ButtonGap = 10f;
        const float RowPitch = 96f;

        void Start()
        {
            // Build first — see MilkyWayMRControls: the canvas sweep must run
            // before CreateWidget's staleness guard looks at the old widget.
            Build();
            LanguageSelect.CreateWidget();
            if (BlackHoleUI.WorldRig != null && stage != null)
                BlackHoleUI.WorldRig.target = stage.transform;

            // Added here, not authored in the scene: the bodies it makes
            // tappable are built by SolarSystemRig at Awake, so nothing could
            // have been wired to them ahead of time.
            focus = gameObject.AddComponent<SolarSystemMRFocus>();
            focus.stage = stage;
            focus.controls = this;
            focus.viewer = GetComponentInChildren<Camera>() ?? Camera.main;
        }

        void Update()
        {
            if (locVersion == Loc.Version) return;
            locVersion = Loc.Version;
            RefreshLabels();
            if (tour != null) tour.OnLanguageChanged();
            if (focus != null) focus.RefreshCard();
        }

        void RefreshLabels()
        {
            foreach (var (label, text) in localized)
                if (label != null) label.text = text();
        }

        public void SetMenuVisible(bool on)
        {
            menuRows.RemoveAll(r => r == null);
            foreach (var row in menuRows) row.SetActive(on);
        }

        void Build()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponentInChildren<Camera>() ?? Camera.main);

            var actions = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("행성 투어", "Planet tour", "惑星ツアー", "行星导览"),
                    () => { if (tour != null) tour.Toggle(); }),
                (() => Loc.T("시간 ×", "Speed ×", "時間 ×", "时间 ×")
                       + Speeds[speedIndex].ToString("0.##"),
                    CycleSpeed),
                (() => Loc.T("이름표", "Labels", "名札", "标签"),
                    () => { if (stage != null && (tour == null || !tour.Running))
                                stage.SetLabelsVisible(!stage.LabelsVisible); }),
            };

            // The title screen is the only hop. Exhibit-to-exhibit buttons put
            // three destinations in every menu and buried the way home among
            // them; the hub does that job.
            var exit = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("처음으로", "Title", "最初へ", "回标题"),
                    () => LoadScene("MRTitle")),
            };

            BuildRow(canvas.transform, "Solar MR Menu", actions, 26f + RowPitch);
            BuildRow(canvas.transform, "Solar MR Exit", exit, 26f);
        }

        void CycleSpeed()
        {
            if (stage == null || (tour != null && tour.Running)) return;
            speedIndex = (speedIndex + 1) % Speeds.Length;
            stage.ambientMotionScale = Speeds[speedIndex];
            stage.SetMotionScale(Speeds[speedIndex]);
            RefreshLabels(); // the speed button shows the value it just set
        }

        void LoadScene(string name)
        {
            if (tour != null && tour.Running) tour.StopTour();
            UnityEngine.SceneManagement.SceneManager.LoadScene(name);
        }

        void BuildRow(Transform parent, string name,
            (System.Func<string> text, UnityEngine.Events.UnityAction act)[] items, float y)
        {
            float total = items.Length * ButtonW + (items.Length - 1) * ButtonGap;
            float x = -total * 0.5f + ButtonW * 0.5f;
            foreach (var (text, act) in items)
            {
                var btn = BlackHoleUI.MakeButton(parent, name + " / " + text(), text(),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, y),
                    new Vector2(ButtonW, ButtonH), act);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null) localized.Add((label, text));
                menuRows.Add(btn.gameObject);
                x += ButtonW + ButtonGap;
            }
        }
    }
}
