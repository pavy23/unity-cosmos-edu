using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// The MR front door: the desktop title screen's language + experience
    /// picker, hung in the room as the shared world-space frame. Four cards —
    /// solar system / Milky Way / nebulae / black hole — each loading its
    /// passthrough exhibit. Every MR scene's menu offers a "처음으로" button
    /// back here, so a visitor in a headset always has the same clean entry
    /// point a desktop visitor gets.
    ///
    /// The frame is the whole scene: the room stays the visitor's own. A
    /// decorative galaxy miniature used to turn above it and was cut — it sold
    /// a thinner version of an exhibit standing one card away.
    ///
    /// Placed against the visitor's head pose on entry rather than against the
    /// scene origin, and re-placed if it ever leaves reach (see Reposition). The
    /// layout is authored around the centre of the frame, not off its edges,
    /// because in a headset the vertical budget is the scarce one: the
    /// edge-anchored version spanned 40 degrees of pitch and left the language
    /// row 26 degrees below the eye line.
    /// </summary>
    public class MRTitleScreen : MonoBehaviour
    {
        [Tooltip("Pose used until a tracked head pose arrives — no longer where " +
                 "the frame ends up.")]
        public Transform frameAnchor;

        [Header("Placement, relative to the visitor's head (metres)")]
        [Tooltip("How far ahead the frame hangs. Sets text size too: the canvas is " +
                 "a fixed 2.6 m wide, so distance is the only thing scaling it.")]
        public float frameDistance = 1.9f;
        [Tooltip("Frame centre below eye level. Sustained upward gaze is the more " +
                 "tiring direction, so the poster sits slightly low.")]
        public float frameDrop = 0.12f;
        [Tooltip("Comfort budget for the card row's horizontal sweep. The canvas " +
                 "bends at true arc length and only warns when a row overruns " +
                 "this, so 52 is the check that the outermost card still sits " +
                 "inside 26 deg of yaw — no head turn to read the row.")]
        public float arcBudgetDegrees = 52f;

        [Header("Re-summon envelope")]
        [Tooltip("Beyond this the frame is re-placed in front of the visitor.")]
        public float maxDistance = 3.2f;
        [Tooltip("Closer than this it is in their face; re-place.")]
        public float minDistance = 0.9f;
        [Tooltip("Off-gaze angle that counts as lost. Below this the frame stays put.")]
        public float maxOffAxisDegrees = 75f;
        [Tooltip("How long it must stay outside the envelope before moving — a " +
                 "glance across the room must not drag the menu along.")]
        public float dwellSeconds = 1.5f;
        public float glideMetresPerSec = 2.2f;

        Text title, subtitle, hint;
        readonly (Text label, System.Func<string> text)[] cardTexts = new (Text, System.Func<string>)[8];
        readonly RectTransform[] cardRects = new RectTransform[4];
        Button[] langButtons;
        int locVersion = -1;

        Camera viewer;
        Transform placement;
        Vector3 want;
        bool placed, gliding;
        float sinceStart, outsideFor;

        DocentOrb orb;
        float idleSince;
        bool hinted;

        public bool FramePlaced => placed;

        struct Card
        {
            public string scene, image;
            public System.Func<string> title, blurb;
        }

        static readonly Card[] Cards =
        {
            new Card { scene = "SolarSystemMR", image = "TitleCards/card_solar",
                title = () => Loc.T("태양계", "Solar System", "太陽系", "太阳系"),
                blurb = () => Loc.T("방 안에 펼쳐지는 여덟 행성 —\n손으로 잡고 돌려보세요",
                                    "Eight planets across your room —\ngrab and turn them by hand",
                                    "部屋に広がる8つの惑星 —\n手でつかんで回せます",
                                    "八颗行星在房间中展开——\n用手抓住转动") },
            new Card { scene = "MilkyWayMR", image = "TitleCards/card_galaxy",
                title = () => Loc.T("우리은하", "Milky Way", "天の川銀河", "银河系"),
                blurb = () => Loc.T("손 위에 올라오는 은하 —\n수천억 별의 미니어처",
                                    "A galaxy at arm's reach —\na miniature of billions of stars",
                                    "手のひらに乗る銀河 —\n数千億の星のミニチュア",
                                    "触手可及的银河——\n数千亿颗恒星的缩影") },
            new Card { scene = "NebulaMR", image = "TitleCards/card_nebula",
                title = () => Loc.T("성운과 성단", "Nebulae & Clusters", "星雲と星団", "星云与星团"),
                blurb = () => Loc.T("방 안에 떠오르는 성운 —\n별의 일생을 따라가는 여행",
                                    "A nebula afloat in your room —\nfollow the life of a star",
                                    "部屋に浮かぶ星雲 —\n星の一生をたどる旅",
                                    "漂浮在房间里的星云——\n追随恒星的一生") },
            new Card { scene = "BlackHoleMR", image = "TitleCards/card_blackhole",
                title = () => Loc.T("블랙홀", "Black Hole", "ブラックホール", "黑洞"),
                blurb = () => Loc.T("당신의 방에 뜬 블랙홀 —\n낙하와 병합까지 체험",
                                    "A black hole in your room —\nride the fall, watch the merger",
                                    "あなたの部屋に浮かぶブラックホール —\n落下と合体まで体験",
                                    "悬浮在你房间里的黑洞——\n体验坠入与并合") },
        };

        static readonly (Loc.Lang lang, string label)[] Languages =
        {
            (Loc.Lang.Korean, "한국어"),
            (Loc.Lang.English, "English"),
            (Loc.Lang.Japanese, "日本語"),
            (Loc.Lang.Chinese, "中文"),
        };

        void Start()
        {
            // Build (=EnsureCanvas sweep) first — the scene-hop lesson: calling
            // any widget guard before the sweep leaves it holding stale UI.
            Build();

            viewer = GetComponentInChildren<Camera>() ?? Camera.main;

            // The rig aims at a transform we own, not at the scene anchor.
            //
            // The anchor is a fixed world point (0, 1.5, 2.0), and on a Quest the
            // world origin is wherever the room was set up — so a visitor who
            // starts anywhere but that exact spot, facing that exact direction,
            // gets the front door behind them, across the room, or inside a wall.
            // A menu has to arrive in front of whoever opened it.
            var go = new GameObject("MR Title Placement") { hideFlags = HideFlags.DontSave };
            placement = go.transform;
            if (frameAnchor != null) placement.position = frameAnchor.position;
            want = placement.position;

            var rig = BlackHoleUI.WorldRig;
            if (rig != null)
            {
                rig.target = placement;
                rig.verticalDrop = 0f;      // the drop is already in the placement height
                rig.arcBudgetDegrees = arcBudgetDegrees;
                rig.PlaceNow();
            }

            // The docent (see DocentOrb): greets once the frame has landed.
            // Clips mr_hub_* have no TTS pass yet — the orb chips them until
            // the audio exists, so the flow is testable today.
            orb = DocentOrb.Spawn(viewer, BlackHoleUI.TitleGold);
            StartCoroutine(Greeting());
        }

        IEnumerator Greeting()
        {
            while (!placed) yield return null;
            yield return new WaitForSeconds(0.8f);

            yield return orb.Say("mr_hub_0", Loc.T(
                "어서 오세요, 우주 전시관입니다",
                "Welcome to the Cosmos Exhibit",
                "ようこそ、宇宙展示館へ",
                "欢迎来到宇宙展览馆"));

            if (cardRects[1] != null) orb.PointAt(cardRects[1]);
            yield return orb.Say("mr_hub_1", Loc.T(
                "손 레이로 카드를 골라 보세요",
                "Point at a card with your hand ray",
                "ハンドレイでカードを選んでください",
                "用手部射线选择一张卡片"));

            if (langButtons != null && langButtons.Length > 1 && langButtons[1] != null)
                orb.PointAt(langButtons[1].transform);
            yield return orb.Say("mr_hub_2", Loc.T(
                "언어는 아래에서 바꿀 수 있어요",
                "You can switch languages below",
                "言語は下で切り替えられます",
                "可以在下方切换语言"));

            orb.PointAt(null);
            idleSince = Time.time;
        }

        void OnDestroy()
        {
            if (placement != null) Destroy(placement.gameObject);
        }

        void Update()
        {
            if (locVersion != Loc.Version)
            {
                locVersion = Loc.Version;
                Refresh();
            }
            Reposition();

            // Discoverability net: a walk-up visitor who has not chosen within
            // 45 s gets a concrete suggestion instead of a silent room.
            if (orb != null && !hinted && idleSince > 0f && Time.time - idleSince > 45f)
            {
                hinted = true;
                if (cardRects[0] != null) orb.PointAt(cardRects[0]);
                orb.Say("mr_hub_3", Loc.T(
                    "태양계부터 볼까요?",
                    "Shall we start with the Solar System?",
                    "太陽系から見てみましょうか?",
                    "从太阳系开始看看吧?"));
            }
        }

        void Reposition()
        {
            if (viewer == null || placement == null) return;

            if (!placed)
            {
                // A headset reports no head pose for the first frames, and the
                // camera then sits at the rig origin. Placing against that is the
                // same bug as the fixed anchor, so wait for tracking — but never
                // forever: a runtime that reports nothing still has to show a menu.
                sinceStart += Time.unscaledDeltaTime;
                if (!HeadTracked() && sinceStart < 1.5f) return;
                Snap(Desired());
                placed = true;
                return;
            }

            if (gliding)
            {
                placement.position = Vector3.MoveTowards(placement.position, want,
                    glideMetresPerSec * Time.deltaTime);
                if ((placement.position - want).sqrMagnitude < 1e-4f) gliding = false;
                return;
            }

            // World-locked once placed: a menu that chases the head reads as a
            // helmet HUD, not as something hanging in the room. But a pose fixed
            // forever is how a visitor loses it two steps later, so it follows
            // when — and only when — it has actually left reach.
            if (Outside())
            {
                outsideFor += Time.deltaTime;
                if (outsideFor >= dwellSeconds)
                {
                    want = Desired();
                    gliding = true;
                    outsideFor = 0f;
                }
            }
            else outsideFor = 0f;
        }

        void Snap(Vector3 pos)
        {
            want = pos;
            placement.position = pos;
            gliding = false;
            outsideFor = 0f;
            if (BlackHoleUI.WorldRig != null) BlackHoleUI.WorldRig.PlaceNow();
        }

        /// <summary>Straight ahead of the visitor at eye level, less the drop.
        /// Yaw only — pitching with the gaze would hang the poster in the floor
        /// for anyone who happened to look down as the scene loaded.</summary>
        Vector3 Desired()
        {
            var t = viewer.transform;
            Vector3 fwd = t.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            return t.position + fwd * frameDistance + Vector3.down * frameDrop;
        }

        bool Outside()
        {
            Vector3 flat = placement.position - viewer.transform.position;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist > maxDistance || dist < minDistance) return true;

            Vector3 gaze = viewer.transform.forward;
            gaze.y = 0f;
            if (gaze.sqrMagnitude < 1e-4f || dist < 1e-2f) return false;
            return Vector3.Angle(gaze.normalized, flat.normalized) > maxOffAxisDegrees;
        }

        static bool HeadTracked()
        {
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            return head.isValid
                && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked)
                && tracked;
        }

        static void Load(int i) =>
            UnityEngine.SceneManagement.SceneManager.LoadScene(Cards[i].scene);

        void Build()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponentInChildren<Camera>() ?? Camera.main);

            // Everything is centre-anchored, and every y below is a distance from
            // the middle of the frame. The desktop title hangs its rows off the top
            // and bottom edges, which is how this screen ended up 1.35 m tall: in a
            // headset that is 40 degrees of pitch, with the language row 26 degrees
            // below the eye line. Anchoring to the centre makes the vertical budget
            // something you can read off the numbers, and the whole layout now fits
            // +11 to -18 degrees at the 1.9 m viewing distance.
            var mid = new Vector2(0.5f, 0.5f);

            title = BlackHoleUI.MakeText(canvas.transform, "Title", 56, BlackHoleUI.TitleGold,
                TextAnchor.MiddleCenter, mid, mid,
                new Vector2(0f, 346f), new Vector2(1400f, 72f), FontStyle.Bold);

            subtitle = BlackHoleUI.MakeText(canvas.transform, "Subtitle", 26, BlackHoleUI.TextSecondary,
                TextAnchor.MiddleCenter, mid, mid,
                new Vector2(0f, 268f), new Vector2(1400f, 36f));

            // Four cards in a row, 0.54 m each at 1.9 m — 16 degrees wide, far
            // beyond the ~3 degree floor a hand ray wants for a target. Narrower
            // than the desktop's 430 so the row lands inside the 52 degree arc.
            const float cardW = 400f, cardH = 400f, gap = 30f;
            float x0 = -((Cards.Length - 1) * (cardW + gap)) * 0.5f;
            for (int i = 0; i < Cards.Length; i++)
            {
                int idx = i;
                var card = BlackHoleUI.MakePanel(canvas.transform, "Card " + Cards[i].scene,
                    mid, mid,
                    new Vector2(x0 + i * (cardW + gap), 0f), new Vector2(cardW, cardH));
                cardRects[i] = card;

                var cardImg = card.GetComponent<Image>();
                cardImg.raycastTarget = true;
                Graphic hoverTarget = cardImg;

                var photoSprite = Resources.Load<Sprite>(Cards[i].image);
                if (photoSprite != null)
                {
                    card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

                    var photo = new GameObject("Photo", typeof(RectTransform), typeof(Image));
                    var pr = (RectTransform)photo.transform;
                    pr.SetParent(card, false);
                    pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
                    float aspect = photoSprite.rect.width / photoSprite.rect.height;
                    pr.sizeDelta = new Vector2(cardH * aspect, cardH);
                    var pimg = photo.GetComponent<Image>();
                    pimg.sprite = photoSprite;
                    pimg.preserveAspect = true;
                    pimg.raycastTarget = false;
                    pr.SetSiblingIndex(0);

                    var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
                    var sr = (RectTransform)scrim.transform;
                    sr.SetParent(card, false);
                    sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
                    sr.offsetMin = sr.offsetMax = Vector2.zero;
                    var simg = scrim.GetComponent<Image>();
                    simg.sprite = TitleScrim.EdgeGradient;
                    simg.type = Image.Type.Simple;
                    simg.color = Color.white;
                    simg.raycastTarget = false;
                    sr.SetSiblingIndex(1);
                    hoverTarget = simg;
                }

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = hoverTarget;
                var colors = btn.colors;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.45f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.85f);
                btn.colors = colors;
                btn.onClick.AddListener(() => Load(idx));

                var cardTitle = BlackHoleUI.MakeText(card, "Title", 38, BlackHoleUI.TitleGold,
                    TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -40f), new Vector2(cardW - 30f, 50f), FontStyle.Bold);
                cardTexts[i * 2] = (cardTitle, Cards[i].title);

                var blurb = BlackHoleUI.MakeText(card, "Blurb", 24, BlackHoleUI.TextPrimary,
                    TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 20f), new Vector2(cardW - 56f, 116f));
                // The factory leaves text unwrapped, which is right for a caption
                // sized to its box. These are not: "Eight planets across your room"
                // is wider than any card, and the card carries a Mask for its photo,
                // so the overflow was being clipped mid-word at both edges rather
                // than merely spilling. Let these two wrap instead.
                blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
                cardTexts[i * 2 + 1] = (blurb, Cards[i].blurb);
            }

            // Language row just under the cards — sized for a hand ray (>= 3
            // degrees), with the active choice edged in gold. Anchored to the
            // centre rather than the frame's bottom edge: off the edge it sat
            // 0.81 m below the eye line, which is a 26 degree look down for the
            // one control every visitor uses first.
            langButtons = new Button[Languages.Length];
            const float langW = 230f, langH = 84f, langGap = 20f;
            float lx = -(Languages.Length - 1) * (langW + langGap) * 0.5f;
            for (int i = 0; i < Languages.Length; i++)
            {
                var lang = Languages[i].lang;
                langButtons[i] = BlackHoleUI.MakeButton(canvas.transform, "Lang " + Languages[i].label,
                    Languages[i].label, mid, mid,
                    new Vector2(lx + i * (langW + langGap), -278f), new Vector2(langW, langH),
                    () => Loc.SetLanguage(lang));
            }

            hint = BlackHoleUI.MakeText(canvas.transform, "Hint", 22, BlackHoleUI.TextSecondary,
                TextAnchor.MiddleCenter, mid, mid,
                new Vector2(0f, -352f), new Vector2(1200f, 30f));

            Refresh();
        }

        void Refresh()
        {
            if (title == null) return;
            title.text = Loc.T("우주 전시관", "The Cosmos Exhibit", "宇宙展示館", "宇宙展览馆");
            subtitle.text = Loc.T("당신의 방에서 체험할 우주를 선택하세요",
                                  "Choose the cosmos to bring into your room",
                                  "あなたの部屋で体験する宇宙を選んでください",
                                  "选择要带进房间的宇宙");
            hint.text = Loc.T("손 레이로 카드를 선택하세요",
                              "Point and select a card with your hand ray",
                              "ハンドレイでカードを選択",
                              "用手部射线选择卡片");
            foreach (var (label, text) in cardTexts)
                if (label != null) label.text = text();

            for (int i = 0; i < langButtons.Length; i++)
            {
                if (langButtons[i] == null) continue;
                var img = langButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = Languages[i].lang == Loc.Language
                        ? new Color(0.42f, 0.36f, 0.18f, 0.97f)
                        : BlackHoleUI.PanelBg;
                var label = langButtons[i].GetComponentInChildren<Text>();
                if (label != null)
                    label.color = Languages[i].lang == Loc.Language
                        ? BlackHoleUI.TitleGold : BlackHoleUI.TextPrimary;
            }
        }
    }

    /// <summary>The title cards' top/bottom scrim, shared by the desktop and MR
    /// title screens so the two front doors read as one design.</summary>
    public static class TitleScrim
    {
        static Sprite edgeGradient;
        public static Sprite EdgeGradient
        {
            get
            {
                if (edgeGradient != null) return edgeGradient;
                const int h = 128;
                var tex = new Texture2D(4, h, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                {
                    float v = y / (h - 1f);            // 0 bottom .. 1 top
                    float topBand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, v));
                    float botBand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0f, v));
                    float a = Mathf.Max(topBand, botBand) * 0.72f;
                    var c = new Color(0.02f, 0.03f, 0.06f, a);
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
                }
                tex.Apply();
                edgeGradient = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f));
                return edgeGradient;
            }
        }
    }
}
