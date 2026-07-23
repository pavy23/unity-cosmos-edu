using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// The MR stand-in for the keyboard: a button menu along the bottom of the
    /// world-space frame, reachable with a hand ray. Every button calls straight
    /// into <see cref="DesktopControls"/> — the cycles, the toasts and the four
    /// language variants live there, and duplicating them here would guarantee
    /// the two drift apart.
    /// </summary>
    [DisallowMultipleComponent]
    public class MRControls : MonoBehaviour
    {
        public DesktopControls controls;
        public Transform hole;

        readonly List<(Text label, System.Func<string> text)> localized = new();
        int locVersion = -1;

        static readonly List<GameObject> menuRows = new();

        // 58px measured 2.5° tall on the headset — over the 2° floor for a ray
        // target but under the ~3° hand tracking needs once jitter is counted,
        // and the rows were 2.9° apart, close enough to mis-hit the next one.
        const float ButtonW = 224f, ButtonH = 84f, ButtonGap = 10f;
        const float RowPitch = 96f;
        const float FrameW = 1920f;

        /// <summary>Hide/show the hand menu with the rest of the overlays. The
        /// narrated experiences own the view — and their captions land on the
        /// same bottom strip the menu occupies. Static because DesktopControls
        /// drives immersion and knows nothing about MR (matches LanguageSelect).</summary>
        public static void SetVisible(bool on)
        {
            menuRows.RemoveAll(r => r == null);
            foreach (var row in menuRows) row.SetActive(on);
        }

        void Start()
        {
            if (controls == null) controls = GetComponent<DesktopControls>();
            if (controls == null) return;
            Build();
        }

        void Update()
        {
            if (locVersion == Loc.Version) return;
            locVersion = Loc.Version;
            foreach (var (label, text) in localized)
                if (label != null) label.text = text();
        }

        void Build()
        {
            menuRows.Clear(); // a DontSave menu outlives play mode; never stack sessions
            var canvas = BlackHoleUI.EnsureCanvas(GetComponentInChildren<Camera>() ?? Camera.main);

            // Three rows along the bottom of the frame, grouped the way the
            // desktop help bar groups its key legend: experiences / the hole
            // itself / phenomena. A row must stay inside the 1920-wide frame —
            // nine buttons would run 2096 and hang off both edges.
            var experiences = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("가이드 투어", "Guided tour", "ガイドツアー", "导览"), ToggleTour),
                (() => Loc.T("블랙홀 탄생", "Birth", "ブラックホール誕生", "黑洞诞生"), BeginIntro),
                (() => Loc.T("낙하 체험", "Fall in", "落下体験", "坠入体验"), BeginFallIn),
                (() => Loc.T("블랙홀 병합", "Merger", "ブラックホール合体", "黑洞合并"), BeginMerger),
            };

            var blackHole = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("원반 색상", "Disk colors", "円盤の色", "吸积盘颜色"), () => controls.CycleColor()),
                (() => Loc.T("질량", "Mass", "質量", "质量"), () => controls.CycleMass()),
                (() => Loc.T("스핀", "Spin", "スピン", "自旋"), CycleSpin),
                (() => Loc.T("관측사진", "EHT photo", "観測写真", "观测照片"), () => controls.CycleComparison()),
                (() => Loc.T("설명 난이도", "Level", "難易度", "难度"), () => controls.CycleDifficulty()),
            };

            var phenomena = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("아인슈타인 링", "Einstein ring", "アインシュタイン環", "爱因斯坦环"), () => controls.ToggleEinstein()),
                (() => Loc.T("스파게티화", "Spaghettify", "スパゲッティ化", "面条化"), () => controls.ToggleSpaghetti()),
                (() => Loc.T("제트", "Jets", "ジェット", "喷流"), () => controls.ToggleJets()),
                (() => Loc.T("렌즈", "Lens", "レンズ", "透镜"), () => controls.ToggleLens()),
                (() => Loc.T("광도곡선", "Light curve", "光度曲線", "光变曲线"), () => controls.ToggleLightCurve()),
                (() => Loc.T("수식", "Formulas", "数式", "公式"), ToggleTheory),
            };

            // The other MR exhibits, one hop away — same scene-name pattern
            // as the desktop F9/F11 keys.
            var scenes = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("은하 전시", "Milky Way", "銀河展示", "银河展区"), () => LoadScene("MilkyWayMR")),
                (() => Loc.T("태양계 전시", "Solar system", "太陽系展示", "太阳系展区"), () => LoadScene("SolarSystemMR")),
                (() => Loc.T("성운 전시", "Nebulae", "星雲展示", "星云展区"), () => LoadScene("NebulaMR")),
                (() => Loc.T("처음으로", "Title", "最初へ", "回标题"), () => LoadScene("MRTitle")),
            };

            BuildRow(canvas.transform, "MR Menu Experiences", experiences, 26f + RowPitch * 3f);
            BuildRow(canvas.transform, "MR Menu BlackHole", blackHole, 26f + RowPitch * 2f);
            BuildRow(canvas.transform, "MR Menu Phenomena", phenomena, 26f + RowPitch);
            BuildRow(canvas.transform, "MR Menu Scenes", scenes, 26f);
        }

        void LoadScene(string scene)
        {
            if (controls == null || controls.CinematicBusy) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }

        void BuildRow(Transform parent, string name,
            (System.Func<string> text, UnityEngine.Events.UnityAction act)[] items, float y)
        {
            float w = ButtonW;
            float total = items.Length * w + (items.Length - 1) * ButtonGap;
            if (total > FrameW)
            {
                // Shrink to fit rather than hang buttons off the edge of the frame,
                // where they would be unreadable and out of the hand ray's way.
                w = (FrameW - (items.Length - 1) * ButtonGap) / items.Length;
                total = FrameW;
                Debug.LogWarning("MRControls: '" + name + "' has " + items.Length
                    + " buttons; narrowing them to " + w.ToString("F0") + "px to fit the frame.");
            }
            float x = -total * 0.5f + w * 0.5f;

            foreach (var (text, act) in items)
            {
                var btn = BlackHoleUI.MakeButton(parent, name + " / " + text(), text(),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(w, ButtonH), act);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null) localized.Add((label, text));
                menuRows.Add(btn.gameObject);
                x += w + ButtonGap;
            }
        }

        void ToggleTour()
        {
            var tour = controls.tour;
            if (tour == null) return;
            if (tour.Running) tour.StopTour();
            else if (!controls.CinematicBusy) tour.StartTour();
        }

        void ToggleTheory()
        {
            if (controls.Theory != null) controls.Theory.Toggle();
        }

        void BeginMerger()
        {
            if (controls.Binary != null && !controls.CinematicBusy) controls.Binary.Begin();
        }

        void BeginFallIn()
        {
            if (controls.fallIn != null && !controls.CinematicBusy) controls.fallIn.Begin();
        }

        void BeginIntro()
        {
            if (controls.intro != null && !controls.CinematicBusy) controls.intro.Play();
        }

        void CycleSpin()
        {
            // The merger owns the spin while it runs (it ramps to the Kerr remnant).
            if (controls.Binary != null && controls.Binary.Running) return;
            controls.CycleSpin();
        }
    }
}
