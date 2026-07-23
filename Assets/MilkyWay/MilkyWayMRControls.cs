using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, LanguageSelect

namespace MilkyWay
{
    /// <summary>
    /// Hand-ray menu for the MR galaxy exhibit — the same bottom-strip button
    /// rows as the black-hole MR menu, driving the miniature instead of a
    /// keyboard. Scene-change buttons hop between the three MR exhibits.
    /// </summary>
    public class MilkyWayMRControls : MonoBehaviour
    {
        public MilkyWayMRStage stage;
        public MilkyWayMRTour tour;

        readonly List<(Text label, System.Func<string> text)> localized = new();
        readonly List<GameObject> menuRows = new();
        int locVersion = -1;

        const float ButtonW = 224f, ButtonH = 84f, ButtonGap = 10f;
        const float RowPitch = 96f;

        void Start()
        {
            // Build (and thereby EnsureCanvas) FIRST: arriving from another MR
            // scene, the stale DontSave canvas still holds last scene's
            // language widget — CreateWidget's "already exists" guard would
            // see it, skip, and then the sweep inside EnsureCanvas destroys
            // it, leaving no widget at all.
            Build();
            LanguageSelect.CreateWidget();
            // Hang the shared world canvas on the miniature, not 2 m ahead.
            if (BlackHoleUI.WorldRig != null && stage != null)
                BlackHoleUI.WorldRig.target = stage.transform;
        }

        void Update()
        {
            if (locVersion == Loc.Version) return;
            locVersion = Loc.Version;
            foreach (var (label, text) in localized)
                if (label != null) label.text = text();
            if (tour != null) tour.OnLanguageChanged();
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
                (() => Loc.T("은하 투어", "Galaxy tour", "銀河ツアー", "银河导览"),
                    () => { if (tour != null) tour.Toggle(); }),
                (() => Loc.T("이름표", "Labels", "名札", "标签"),
                    () => { if (stage != null && (tour == null || !tour.Running))
                                stage.SetLabelsVisible(!stage.LabelsVisible); }),
                (() => Loc.T("자전", "Spin", "自転", "自转"),
                    () => { if (stage != null) stage.spin = !stage.spin; }),
                (() => Loc.T("원위치", "Reset", "元に戻す", "复位"),
                    () => { if (stage != null) stage.ResetPose(); }),
            };

            var scenes = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("블랙홀 전시", "Black hole", "ブラックホール展示", "黑洞展区"),
                    () => LoadScene("BlackHoleMR")),
                (() => Loc.T("태양계 전시", "Solar system", "太陽系展示", "太阳系展区"),
                    () => LoadScene("SolarSystemMR")),
                (() => Loc.T("성운 전시", "Nebulae", "星雲展示", "星云展区"),
                    () => LoadScene("NebulaMR")),
                (() => Loc.T("처음으로", "Title", "最初へ", "回标题"),
                    () => LoadScene("MRTitle")),
            };

            BuildRow(canvas.transform, "MW MR Menu", actions, 26f + RowPitch);
            BuildRow(canvas.transform, "MW MR Scenes", scenes, 26f);
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
