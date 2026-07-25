using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// Guided tour (F4): steps through the anatomy of the galaxy one concept
    /// at a time. Unlike the black-hole tour (where the camera never moves and
    /// each step toggles a demo), here the CAMERA is the demo — every step has
    /// an authored viewpoint that frames the structure being narrated, with a
    /// slow drift while the step holds so the frame never freezes.
    /// F4 = start/stop, N/→ = next, B/← = previous (wired in MilkyWayControls).
    /// </summary>
    public class MilkyWayTour : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Seconds the camera takes to glide between step viewpoints.")]
        public float glideDuration = 3.2f;

        public bool Running { get; private set; }

        int step;
        RectTransform card;
        Text cardTitle, cardBody, cardFooter;

        // Camera glide state: from-pose → step pose, then drift on hold.
        Vector3 fromPos, fromLook, toPos, toLook;
        float glideT;
        Vector3 curLook;

        LineRenderer marker;
        Material markerMat;
        float baseHii, baseDust;

        struct Step
        {
            public string title, titleEn, titleJa, titleZh;
            public Vector3 pos, look;      // authored viewpoint (galaxy at origin)
            public bool sunMarker;         // show the gold "we are here" ring
            // Emphasis multipliers on the controller while the step holds —
            // the step turns up the feature it is talking about (0 = leave at 1).
            public float hiiMul, dustMul;
        }

        // The narration clips (Resources/Narration/{,en,ja,zh}/mw_tour_N) are
        // generated from these lines — subtitle == voice, the exhibit-wide
        // convention. Public so tooling can regenerate the audio.
        public static readonly string[] NarrationLines =
        {
            "우리 은하 투어에 오신 것을 환영합니다. 지금 보는 나선 원반이 우리 은하입니다. 지름 약 10만 광년, 별 수천억 개. 이제 가까이 다가가 구조를 하나씩 살펴봅시다.",
            "은하 중심의 노랗게 빛나는 부분은 벌지입니다. 나이 많은 별들이 빽빽하게 모여 따뜻한 색을 냅니다. 자세히 보면 중심은 둥글지 않고 길쭉한 막대 모양입니다. 우리 은하는 막대나선은하이고, 막대 한가운데에는 태양 4백만 배 질량의 블랙홀, 궁수자리 A 스타가 숨어 있습니다.",
            "은하를 감아 도는 밝은 팔은 고정된 구조물이 아닙니다. 밀도파라고 부르는, 일종의 교통 정체입니다. 별과 가스는 이 구간을 지나가며 잠시 붐비고, 붐비는 곳에서 새 별이 태어나 팔이 밝게 빛납니다.",
            "팔 안쪽을 따라 흐르는 갈색 줄무늬는 성간 먼지입니다. 먼지는 파란 빛을 더 많이 흡수하기 때문에 갈색으로 보입니다. 어둡게 가리고 있지만, 이 먼지야말로 다음 세대의 별과 행성이 될 재료입니다.",
            "팔 곳곳에 분홍빛 매듭이 보이나요? 갓 태어난 뜨거운 별들이 주변의 수소 가스를 달구어 내는 빛입니다. 지금 이 순간에도 별이 태어나고 있다는 표지이고, 밤하늘의 오리온 대성운도 그중 하나입니다.",
            "금색 고리가 표시하는 곳, 은하 중심에서 약 2만 6천 광년 떨어진 이곳에 태양이 있습니다. 태양은 초속 230킬로미터로 은하를 돌고 있고, 한 바퀴에 약 2억 3천만 년이 걸립니다. 공룡 시대가 시작된 뒤로 겨우 한 바퀴를 돌았을 뿐입니다.",
            "원반을 감싸는 성긴 별들의 구름이 헤일로입니다. 그리고 눈에 보이지 않지만, 이 모든 것이 흩어지지 않게 붙잡아 주는 암흑물질이 은하 질량의 대부분을 차지합니다. 이제 자유롭게 탐험해 보세요.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "Welcome to the Milky Way tour. The spiral disk you are looking at is our galaxy — about one hundred thousand light-years across, holding hundreds of billions of stars. Let's move in and examine its structure, one piece at a time.",
            "The warm yellow glow at the centre is the bulge — old stars packed densely together. Look closely and the centre is not round but elongated: our galaxy is a barred spiral, and hidden at the very middle of the bar sits a black hole of four million solar masses, Sagittarius A star.",
            "The bright arms winding around the galaxy are not fixed structures. They are density waves — a kind of traffic jam. Stars and gas crowd together briefly as they pass through, and where they crowd, new stars are born and the arm lights up.",
            "The brown streaks running along the inner edges of the arms are interstellar dust. Dust absorbs blue light more than red, which is why it looks brown. Dark as it is, this dust is the raw material of the next generation of stars and planets.",
            "Do you see the pink knots scattered along the arms? That is the light of hot newborn stars heating the hydrogen gas around them — a sign that stars are being born right now. The Orion Nebula in our night sky is one of them.",
            "Where the gold ring marks, about twenty-six thousand light-years from the centre, sits the Sun. It circles the galaxy at two hundred thirty kilometres per second, taking about two hundred thirty million years per lap. Since the age of dinosaurs began, we have completed barely one orbit.",
            "The sparse cloud of stars wrapped around the disk is the halo. And invisible to the eye, dark matter — most of the galaxy's mass — is what holds all of this together. Now, explore freely.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "天の川銀河ツアーへようこそ。いまご覧の渦巻きの円盤が、私たちの銀河です。差し渡し約10万光年、数千億の星。近づいて、構造をひとつずつ見ていきましょう。",
            "中心で黄色く輝く部分はバルジです。年老いた星々が密集して、暖かな色を放っています。よく見ると中心は丸くなく、細長い棒の形をしています。天の川銀河は棒渦巻銀河で、棒の真ん中には太陽の400万倍の質量のブラックホール、いて座Aスターが隠れています。",
            "銀河に巻きつく明るい腕は、固定された構造物ではありません。密度波と呼ばれる、いわば渋滞です。星とガスはここを通り過ぎるとき一時的に混み合い、混み合う場所で新しい星が生まれて、腕が明るく輝くのです。",
            "腕の内側に沿って流れる茶色い筋は、星間塵です。塵は青い光をより多く吸収するため、茶色に見えます。暗く覆い隠していますが、この塵こそ、次の世代の星と惑星の材料です。",
            "腕のあちこちにピンクの結び目が見えますか？生まれたばかりの高温の星が、周りの水素ガスを熱して放つ光です。いままさに星が生まれている印で、夜空のオリオン大星雲もそのひとつです。",
            "金色の輪が示す場所、銀河中心から約2万6千光年のここに太陽があります。太陽は秒速230キロメートルで銀河を回り、一周に約2億3千万年かかります。恐竜の時代が始まってから、まだ一周しか回っていません。",
            "円盤を包む、まばらな星の雲がハローです。そして目には見えませんが、このすべてをつなぎ留めている暗黒物質が、銀河の質量の大部分を占めています。さあ、自由に探検してみてください。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "欢迎来到银河系之旅。您眼前的旋涡圆盘就是我们的银河系——直径约十万光年，容纳数千亿颗恒星。现在让我们靠近一些，逐一观察它的结构。",
            "中心黄色的光辉是核球——年老的恒星密集地聚在一起，发出温暖的颜色。仔细看，中心并不是圆的，而是细长的棒状。银河系是一个棒旋星系，而在棒的正中央，隐藏着一个四百万倍太阳质量的黑洞——人马座A星。",
            "缠绕星系的明亮旋臂并不是固定的结构，而是所谓的密度波——就像一场交通堵塞。恒星和气体经过这里时短暂地拥挤起来，而在拥挤之处，新的恒星诞生，旋臂因此发亮。",
            "沿着旋臂内侧流淌的棕色条纹是星际尘埃。尘埃吸收蓝光多于红光，所以看起来是棕色的。它虽然遮挡了光芒，但正是下一代恒星和行星的原材料。",
            "看到旋臂上散布的粉色光结了吗？那是刚诞生的炽热恒星加热周围氢气发出的光——恒星此刻正在诞生的标志。我们夜空中的猎户座大星云就是其中之一。",
            "金色圆环标记之处，距离银心约二点六万光年，就是太阳的位置。它以每秒230公里的速度绕银河运行，一圈约需二亿三千万年。自恐龙时代开始以来，我们才刚转完一圈。",
            "包裹着圆盘的稀疏恒星云就是银晕。还有肉眼看不见的暗物质——占星系质量的大部分——正是它把这一切维系在一起。现在，自由探索吧。",
        };

        // Viewpoints are authored against the showcase layout: galaxy at the
        // origin, disk radius 16, Sun at (8.2, 0, 0), bar leaning 27° off +x.
        static readonly Step[] Steps =
        {
            // The title is NOT the narration's first sentence — the body text
            // below it is (subtitle == voice), and a verbatim repeat looked
            // like a rendering bug on the panel.
            new Step { pos = new Vector3(0f, 16f, -30f), look = Vector3.zero,
                title = "투어를 시작합니다 — 우리 은하", titleEn = "The Tour Begins — Our Galaxy",
                titleJa = "ツアー開始 — 私たちの銀河", titleZh = "旅程开始 — 我们的银河" },
            new Step { pos = new Vector3(3f, 25f, -9f), look = Vector3.zero,
                title = "1. 벌지와 막대", titleEn = "1. The Bulge and the Bar",
                titleJa = "1. バルジと棒", titleZh = "1. 核球与棒" },
            new Step { pos = new Vector3(9f, 10f, -19f), look = Vector3.zero,
                title = "2. 나선팔 — 별들의 교통 정체", titleEn = "2. Spiral Arms — a Stellar Traffic Jam",
                titleJa = "2. 渦状腕 — 星の渋滞", titleZh = "2. 旋臂——恒星的交通堵塞" },
            new Step { pos = new Vector3(2f, 7.5f, -18.5f), look = new Vector3(0f, 0f, -3f), dustMul = 1.4f,
                title = "3. 먼지 레인", titleEn = "3. Dust Lanes",
                titleJa = "3. ダストレーン", titleZh = "3. 尘埃带" },
            new Step { pos = new Vector3(10.5f, 5f, -12.5f), look = new Vector3(4.5f, 0f, -3.5f), hiiMul = 2.2f,
                title = "4. 별이 태어나는 곳", titleEn = "4. Stellar Nurseries",
                titleJa = "4. 星の生まれる場所", titleZh = "4. 恒星的诞生地" },
            new Step { pos = new Vector3(11.5f, 4.5f, -6f), look = new Vector3(8.2f, 0f, 0f), sunMarker = true,
                title = "5. 태양의 자리", titleEn = "5. The Sun's Place",
                titleJa = "5. 太陽の場所", titleZh = "5. 太阳的位置" },
            new Step { pos = new Vector3(0f, 26f, -50f), look = Vector3.zero,
                title = "6. 헤일로와 암흑물질", titleEn = "6. The Halo and Dark Matter",
                titleJa = "6. ハローと暗黒物質", titleZh = "6. 银晕与暗物质" },
        };

        // ---- data accessors for the MR edition (shares titles and narration;
        // points with a highlight ring instead of flying the camera) ---------
        public static int StepCount => Steps.Length;
        public static string StepTitle(int i)
        {
            var s = Steps[i];
            return Loc.T(s.title, s.titleEn, s.titleJa, s.titleZh);
        }

        public void StartTour()
        {
            if (Running) return;
            Running = true;
            step = 0;
            if (controller != null) { baseHii = controller.hiiStrength; baseDust = controller.dustStrength; }
            if (orbit != null) orbit.enabled = false;
            ApplyStep();
        }

        public void StopTour()
        {
            if (!Running) return;
            Running = false;
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            DestroyMarker();
            if (controller != null)
            {
                controller.hiiStrength = baseHii;
                controller.dustStrength = baseDust;
                controller.Apply();
            }
            // Stay where the tour left the camera — the ambient orbit re-syncs
            // from the transform (the exhibit's layering trick), so there is no
            // snap. The tour's last step IS a good overview.
            if (orbit != null) orbit.enabled = true;
        }

        public void Next() { if (Running && step < Steps.Length - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        /// <summary>Re-applies the current step after a language toggle so the
        /// card text and narration voice switch immediately.</summary>
        public void OnLanguageChanged()
        {
            if (Running) ApplyStep();
        }

        void ApplyStep()
        {
            var s = Steps[step];

            // Glide from wherever we are now to the step's viewpoint.
            fromPos = transform.position;
            fromLook = curLook = transform.position + transform.forward * 10f;
            toPos = s.pos;
            toLook = s.look;
            glideT = 0f;

            if (s.sunMarker) EnsureMarker(); else DestroyMarker();

            // Turn up the feature this step is talking about, back down after.
            if (controller != null)
            {
                controller.hiiStrength = baseHii * (s.hiiMul > 0f ? s.hiiMul : 1f);
                controller.dustStrength = baseDust * (s.dustMul > 0f ? s.dustMul : 1f);
                controller.Apply();
            }

            if (Application.isPlaying) NarrationManager.Instance.Play("mw_tour_" + step);

            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = Loc.T(s.title, s.titleEn, s.titleJa, s.titleZh);
            cardBody.text = Loc.T(NarrationLines[step], NarrationLinesEn[step],
                                  NarrationLinesJa[step], NarrationLinesZh[step]);
            cardFooter.text = Loc.T("◀ ▶ 이동    × 종료",
                                    "◀ ▶ Step    × End",
                                    "◀ ▶ 移動    × 終了",
                                    "◀ ▶ 切换    × 结束")
                            + "                                  " + (step + 1) + " / " + Steps.Length;
        }

        void Update()
        {
            if (!Running) return;

            if (glideT < 1f)
            {
                glideT = Mathf.Min(1f, glideT + Time.deltaTime / Mathf.Max(glideDuration, 0.1f));
                float u = Mathf.SmoothStep(0f, 1f, glideT);
                transform.position = Vector3.Lerp(fromPos, toPos, u);
                curLook = Vector3.Lerp(fromLook, toLook, u);
                transform.LookAt(curLook);
            }
            else
            {
                // Holding a step: a slow drift around the framed point, so the
                // parallax keeps selling the 3D structure while the narration
                // plays. RotateAround preserves the look-at by construction.
                transform.RotateAround(toLook, Vector3.up, 0.5f * Time.deltaTime);
            }

            if (marker != null)
            {
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.6f);
                var c = new Color(1.7f, 1.25f, 0.55f, pulse);
                marker.startColor = c;
                marker.endColor = c;
            }
        }

        // ---------------- the "we are here" ring (zoom journey's marker) -----

        void EnsureMarker()
        {
            if (marker != null || controller == null) return;
            var go = new GameObject("You Are Here (tour)");
            var line = go.AddComponent<LineRenderer>();
            const int N = 64;
            line.positionCount = N;
            line.loop = true;
            line.useWorldSpace = true;
            // The tour views the ring from ~5 kpc, not 52 — thinner than the
            // zoom journey's finale ring or it reads as a hoop, not a marker.
            line.widthMultiplier = 0.08f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerMat = new Material(Shader.Find("Sprites/Default"));
            line.material = markerMat;
            Vector3 sun = controller.SunPositionWorld;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                line.SetPosition(i, sun + new Vector3(Mathf.Cos(a), 0.06f, Mathf.Sin(a)) * 0.9f);
            }
            marker = line;
        }

        void DestroyMarker()
        {
            if (marker != null) Destroy(marker.gameObject);
            if (markerMat != null) Destroy(markerMat);
            marker = null; markerMat = null;
        }

        // ---------------- card UI (the shared factory) -----------------------

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());

            card = BlackHoleUI.MakePanel(canvas.transform, "MW Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(920f, 218f));

            cardTitle = BlackHoleUI.MakeText(card, "Title", 26, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(860f, 34f), FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", 20, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -58f), new Vector2(860f, 118f));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardFooter = BlackHoleUI.MakeText(card, "Footer", 15, BlackHoleUI.TextSecondary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 12f), new Vector2(860f, 22f));

            BlackHoleUI.MakeTourNav(card, Prev, Next, StopTour);
        }

        void OnDisable()
        {
            if (Running) StopTour();
        }
    }
}
