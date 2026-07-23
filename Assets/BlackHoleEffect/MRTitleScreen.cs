using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// The MR front door: the desktop title screen's language + experience
    /// picker, hung in the room as the shared world-space frame. Three cards —
    /// solar system / Milky Way / black hole — each loading its passthrough
    /// exhibit, with a slowly turning galaxy miniature floating above the
    /// frame as the room's only decoration. Every MR scene's menu offers a
    /// "처음으로" button back here, so a visitor in a headset always has the
    /// same clean entry point a desktop visitor gets.
    /// </summary>
    public class MRTitleScreen : MonoBehaviour
    {
        [Tooltip("Where the frame hangs in the room (MRWorldCanvas target).")]
        public Transform frameAnchor;
        [Tooltip("Decorative galaxy miniature, spun slowly about its disk axis.")]
        public Transform decor;
        public float decorSpinDegPerSec = 2.5f;

        Text title, subtitle, hint;
        readonly (Text label, System.Func<string> text)[] cardTexts = new (Text, System.Func<string>)[8];
        Button[] langButtons;
        int locVersion = -1;

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
            // Pin the frame to the room anchor instead of the (absent) hole.
            if (BlackHoleUI.WorldRig != null && frameAnchor != null)
                BlackHoleUI.WorldRig.target = frameAnchor;
        }

        void Update()
        {
            if (locVersion != Loc.Version)
            {
                locVersion = Loc.Version;
                Refresh();
            }
            if (decor != null)
                decor.Rotate(0f, decorSpinDegPerSec * Time.deltaTime, 0f, Space.Self);
        }

        static void Load(int i) =>
            UnityEngine.SceneManagement.SceneManager.LoadScene(Cards[i].scene);

        void Build()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponentInChildren<Camera>() ?? Camera.main);

            title = BlackHoleUI.MakeText(canvas.transform, "Title", 64, BlackHoleUI.TitleGold,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -96f), new Vector2(1400f, 96f), FontStyle.Bold);

            subtitle = BlackHoleUI.MakeText(canvas.transform, "Subtitle", 28, BlackHoleUI.TextSecondary,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -204f), new Vector2(1400f, 40f));

            // Four cards across the 1920 frame — still far beyond the ~3 degree
            // floor a hand ray wants for a target.
            const float cardW = 430f, cardH = 430f, gap = 36f;
            float x0 = -((Cards.Length - 1) * (cardW + gap)) * 0.5f;
            for (int i = 0; i < Cards.Length; i++)
            {
                int idx = i;
                var card = BlackHoleUI.MakePanel(canvas.transform, "Card " + Cards[i].scene,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x0 + i * (cardW + gap), -10f), new Vector2(cardW, cardH));

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

                var cardTitle = BlackHoleUI.MakeText(card, "Title", 42, BlackHoleUI.TitleGold,
                    TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -46f), new Vector2(cardW - 30f, 56f), FontStyle.Bold);
                cardTexts[i * 2] = (cardTitle, Cards[i].title);

                var blurb = BlackHoleUI.MakeText(card, "Blurb", 26, BlackHoleUI.TextPrimary,
                    TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 24f), new Vector2(cardW - 50f, 100f));
                cardTexts[i * 2 + 1] = (blurb, Cards[i].blurb);
            }

            // Language row along the bottom of the frame — sized for a hand
            // ray (>= 3 degrees), with the active choice edged in gold.
            langButtons = new Button[Languages.Length];
            const float langW = 250f, langH = 92f, langGap = 22f;
            float lx = -(Languages.Length - 1) * (langW + langGap) * 0.5f;
            for (int i = 0; i < Languages.Length; i++)
            {
                var lang = Languages[i].lang;
                langButtons[i] = BlackHoleUI.MakeButton(canvas.transform, "Lang " + Languages[i].label,
                    Languages[i].label, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(lx + i * (langW + langGap), 120f), new Vector2(langW, langH),
                    () => Loc.SetLanguage(lang));
            }

            hint = BlackHoleUI.MakeText(canvas.transform, "Hint", 22, BlackHoleUI.TextSecondary,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 52f), new Vector2(1200f, 32f));

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
