using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, LanguageSelect

namespace MilkyWay
{
    /// <summary>
    /// Hand-ray menu and museum label for the MR nebula vitrine.
    ///
    /// The label — name / facts / blurb from NebulaLibrary — rides the top-left
    /// of the world frame and follows whichever specimen the case is showing,
    /// in whichever language. The row underneath is the two verbs the exhibit
    /// actually has: step through the specimens, or start "별의 일생" and let
    /// the tour drive. What is deliberately NOT here is 원위치 — the case is
    /// not grabbable (see NebulaMRStage), so there is nothing to put back.
    /// </summary>
    public class NebulaMRControls : MonoBehaviour
    {
        public NebulaMRStage stage;
        public NebulaMRTour tour;

        readonly List<(Text label, System.Func<string> text)> localized = new();
        readonly List<GameObject> menuRows = new();
        int locVersion = -1;
        int shownSpecimen = -1;

        RectTransform factCard;
        Text factName, factLine, factBlurb;

        const float ButtonW = 224f, ButtonH = 84f, ButtonGap = 10f;
        const float RowPitch = 96f;

        void Start()
        {
            // Build (=EnsureCanvas sweep) FIRST — the scene-hop lesson: any
            // widget guard called before the sweep is left holding stale UI.
            Build();
            LanguageSelect.CreateWidget();
            // Hang the shared world frame on the miniature, not 2 m ahead.
            if (BlackHoleUI.WorldRig != null && stage != null)
                BlackHoleUI.WorldRig.target = stage.transform;
        }

        void Update()
        {
            bool langChanged = locVersion != Loc.Version;
            if (langChanged)
            {
                locVersion = Loc.Version;
                foreach (var (label, text) in localized)
                    if (label != null) label.text = text();
                if (tour != null) tour.OnLanguageChanged();
            }
            // The museum label tracks whatever specimen is on show.
            if (stage != null && (langChanged || shownSpecimen != stage.Current))
            {
                shownSpecimen = stage.Current;
                RefreshFactCard();
            }
        }

        public void SetMenuVisible(bool on)
        {
            menuRows.RemoveAll(r => r == null);
            foreach (var row in menuRows) row.SetActive(on);
            if (factCard != null) factCard.gameObject.SetActive(on);
        }

        void Build()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponentInChildren<Camera>() ?? Camera.main);

            // Prev/next are inert while the tour runs: the tour owns which
            // specimen is up, and its own card carries 이전/다음 for stepping
            // the story instead.
            var actions = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("별의 일생 투어", "Life of a star", "星の一生ツアー", "恒星一生导览"),
                    () => { if (tour != null) tour.Toggle(); }),
                (() => Loc.T("◀ 이전 천체", "◀ Previous", "◀ 前の天体", "◀ 上一个"),
                    () => { if (stage != null && (tour == null || !tour.Running)) stage.Prev(); }),
                (() => Loc.T("다음 천체 ▶", "Next ▶", "次の天体 ▶", "下一个 ▶"),
                    () => { if (stage != null && (tour == null || !tour.Running)) stage.Next(); }),
            };

            var exit = new (System.Func<string> text, UnityEngine.Events.UnityAction act)[]
            {
                (() => Loc.T("처음으로", "Title", "最初へ", "回标题"),
                    () => LoadScene("MRTitle")),
            };

            BuildRow(canvas.transform, "Nebula MR Menu", actions, 26f + RowPitch);
            BuildRow(canvas.transform, "Nebula MR Exit", exit, 26f);

            // The museum label: name / facts / blurb in the top-left corner
            // column (the language widget owns top-centre in MR, and the
            // cylinder brings the corner to a comfortable head turn anyway).
            // Top-pivoted, so MR's taller reading text grows the card downward
            // into the empty left column rather than up off the frame.
            factCard = BlackHoleUI.MakePanel(canvas.transform, "Nebula MR Fact Card",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f),
                new Vector2(640f, BlackHoleUI.ReadingY(264f)));
            menuRows.Add(factCard.gameObject);

            factName = BlackHoleUI.MakeText(factCard, "Name", BlackHoleUI.ReadingSize(30), BlackHoleUI.TitleGold,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, BlackHoleUI.ReadingY(-18f)), new Vector2(588f, BlackHoleUI.ReadingY(40f)),
                FontStyle.Bold);

            factLine = BlackHoleUI.MakeText(factCard, "Facts", BlackHoleUI.ReadingSize(18), BlackHoleUI.Accent,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, BlackHoleUI.ReadingY(-62f)), new Vector2(588f, BlackHoleUI.ReadingY(52f)));
            factLine.horizontalOverflow = HorizontalWrapMode.Wrap;

            factBlurb = BlackHoleUI.MakeText(factCard, "Blurb", BlackHoleUI.ReadingSize(20), BlackHoleUI.TextPrimary,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, BlackHoleUI.ReadingY(-118f)), new Vector2(588f, BlackHoleUI.ReadingY(132f)));
            factBlurb.horizontalOverflow = HorizontalWrapMode.Wrap;

            RefreshFactCard();
        }

        void RefreshFactCard()
        {
            if (factName == null || stage == null) return;
            var h = stage.CurrentHero;
            factName.text = h.name();
            factLine.text = h.facts();
            factBlurb.text = h.blurb();
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
