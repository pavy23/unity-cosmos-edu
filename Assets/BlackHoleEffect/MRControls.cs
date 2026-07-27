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

            // Two content rows plus the way out. Every row is also an arc the
            // visitor has to sweep their head across — MRWorldCanvas bends the
            // layout at true arc length, so a button added here costs real
            // degrees of neck. Six is the widest row that still lands inside
            // the 70 deg comfort budget at the 1.6 m viewing distance.
            //
            // What is deliberately NOT here, and why (desktop keeps all of it):
            //   낙하 체험    — drives the camera into the hole. Forced translation
            //                 with a stationary head is the textbook VR-sickness
            //                 trigger, and in a passthrough room it is worse: the
            //                 real floor stays put while the view moves.
            //   블랙홀 병합  — swaps the room for a starfield (MRSpaceWindow), so
            //                 an MR exhibit silently becomes a VR one mid-session.
            //   질량        — CycleMass grows the horizon until the viewer is
            //                 inside it. There is no safe upper cycle in a
            //                 room-scale exhibit; the hole is one arm away.
            //   광도곡선/수식 — a fine-lined 2D plot and a formula sheet, authored
            //                 at desktop density. At MR text sizes they are not
            //                 readable, and the panels eat the view while open.
            var exhibit = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("가이드 투어", "Guided tour", "ガイドツアー", "导览"), ToggleTour),
                (() => Loc.T("블랙홀 탄생", "Birth", "ブラックホール誕生", "黑洞诞生"), BeginIntro),
                (() => Loc.T("원반 색상", "Disk colors", "円盤の色", "吸积盘颜色"), () => controls.CycleColor()),
                (() => Loc.T("스핀", "Spin", "スピン", "自旋"), CycleSpin),
                (() => Loc.T("관측사진", "EHT photo", "観測写真", "观测照片"), () => controls.CycleComparison()),
                (() => Loc.T("설명 난이도", "Level", "難易度", "难度"), () => controls.CycleDifficulty()),
            };

            //   아인슈타인 링/렌즈 — both need a bright source BEHIND the hole for
            //                 the split images to be read against. In passthrough
            //                 the background is the visitor's room, so there is
            //                 nothing to lens and the effect is invisible.
            //   손바닥 블랙홀  — a second raymarched hole at hand distance, on the
            //                 scene already sitting at the Quest march budget.
            var phenomena = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("스파게티화", "Spaghettify", "スパゲッティ化", "面条化"), () => controls.ToggleSpaghetti()),
                (() => Loc.T("제트", "Jets", "ジェット", "喷流"), () => controls.ToggleJets()),
            };

            // One way out, and it is the title. Hopping straight between exhibits
            // put three destinations in every menu and made the way home the
            // fourth thing to find; the title screen is the hub now.
            var exit = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("처음으로", "Title", "最初へ", "回标题"), () => LoadScene("MRTitle")),
            };

            BuildRow(canvas.transform, "MR Menu Exhibit", exhibit, 26f + RowPitch * 2f);
            BuildRow(canvas.transform, "MR Menu Phenomena", phenomena, 26f + RowPitch);
            BuildRow(canvas.transform, "MR Menu Exit", exit, 26f);
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
