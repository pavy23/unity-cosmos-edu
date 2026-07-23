using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, LanguageSelect

namespace MilkyWay
{
    /// <summary>
    /// Hand-ray menu for the MR solar-system exhibit: tour, scale truth,
    /// time speed, labels, and hops to the other two MR exhibits.
    /// </summary>
    public class SolarSystemMRControls : MonoBehaviour
    {
        public SolarSystemMRStage stage;
        public SolarSystemMRTour tour;

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
            if (stage != null) stage.TruthEnded += () => { SetMenuVisible(true); RefreshLabels(); };
        }

        void Update()
        {
            if (locVersion == Loc.Version) return;
            locVersion = Loc.Version;
            RefreshLabels();
            if (tour != null) tour.OnLanguageChanged();
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
                (() => Loc.T("진짜 크기", "True scale", "本当の縮尺", "真实比例"),
                    BeginTruth),
                (() => Loc.T("시간 ×", "Speed ×", "時間 ×", "时间 ×")
                       + Speeds[speedIndex].ToString("0.##"),
                    CycleSpeed),
                (() => Loc.T("이름표", "Labels", "名札", "标签"),
                    () => { if (stage != null && (tour == null || !tour.Running) && !stage.TruthRunning)
                                stage.SetLabelsVisible(!stage.LabelsVisible); }),
            };

            var scenes = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("블랙홀 전시", "Black hole", "ブラックホール展示", "黑洞展区"),
                    () => LoadScene("BlackHoleMR")),
                (() => Loc.T("은하 전시", "Milky Way", "銀河展示", "银河展区"),
                    () => LoadScene("MilkyWayMR")),
                (() => Loc.T("성운 전시", "Nebulae", "星雲展示", "星云展区"),
                    () => LoadScene("NebulaMR")),
                (() => Loc.T("처음으로", "Title", "最初へ", "回标题"),
                    () => LoadScene("MRTitle")),
            };

            BuildRow(canvas.transform, "Solar MR Menu", actions, 26f + RowPitch);
            BuildRow(canvas.transform, "Solar MR Scenes", scenes, 26f);
        }

        void BeginTruth()
        {
            if (stage == null || (tour != null && tour.Running)) return;
            if (stage.TruthRunning) { stage.AbortTruth(); return; }
            SetMenuVisible(false); // captions land on the strip the menu occupies
            stage.ToggleTruth();
        }

        void CycleSpeed()
        {
            if (stage == null || (tour != null && tour.Running) || stage.TruthRunning) return;
            speedIndex = (speedIndex + 1) % Speeds.Length;
            stage.ambientMotionScale = Speeds[speedIndex];
            stage.SetMotionScale(Speeds[speedIndex]);
            RefreshLabels(); // the speed button shows the value it just set
        }

        void LoadScene(string name)
        {
            if (tour != null && tour.Running) tour.StopTour();
            if (stage != null && stage.TruthRunning) stage.AbortTruth();
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
