using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The solar-system tour (F6): nine narrated stops, Sun to Neptune, on a
    /// SolarSystemRig spawned for the occasion. Unlike the galaxy tour the
    /// subjects MOVE — planets keep orbiting (motionScale slowed so a framed
    /// planet holds still enough to study) — so every step's camera pose is
    /// computed from the live body each frame: the glide chases a moving
    /// target, and the hold rides along with the planet while drifting slowly
    /// around it for parallax.
    ///
    /// The rig spawns far from the galactic centre so the Milky Way itself
    /// hangs in the background of the wider frames — the exhibit's scales
    /// linking up. F6 = start/stop, N/→ next, B/← previous (MilkyWayControls).
    /// </summary>
    public class SolarSystemTour : MonoBehaviour
    {
        public CinematicOrbit orbit;

        [Tooltip("Seconds the camera takes to glide between bodies.")]
        public float glideDuration = 3.2f;
        [Tooltip("Orbit/spin speed while touring. 1 = the zoom journey's pace; " +
                 "small enough that a framed planet stays framed, big enough " +
                 "that Jupiter visibly turns during its stop.")]
        public float tourMotionScale = 0.06f;

        public bool Running { get; private set; }

        // The rig is a prop of the tour: spawned on start, destroyed on stop.
        // Placed away from the galactic centre (|p| ≈ 88 kpc) and scaled so
        // Earth is ~1 unit — comfortably above near-plane trouble.
        static readonly Vector3 RigPosition = new Vector3(60f, 25f, 60f);
        const float RigScale = 1000f;

        SolarSystemRig rig;
        int step;
        float driftDeg;

        RectTransform card;
        Text cardTitle, cardBody, cardFooter;

        Vector3 fromPos, fromLook;
        float glideT;

        Vector3 savedPos;
        Quaternion savedRot;
        float savedNear, savedFar;

        struct Stop
        {
            public string body;            // GetBody key
            public float frameMul;         // camera distance in framing radii
            public string title, titleEn, titleJa, titleZh;
        }

        // The narration clips (Resources/Narration/{,en,ja,zh}/mw_sol_N) are
        // generated from these lines — subtitle == voice, the exhibit-wide
        // convention. Public so tooling can regenerate the audio.
        public static readonly string[] NarrationLines =
        {
            "태양입니다. 태양계 전체 질량의 99.8퍼센트가 이 한 별에 모여 있습니다. 표면의 무늬는 실제 관측 영상에서 온 것으로, 어두운 얼룩 하나가 지구보다 큽니다.",
            "수성 — 가장 작고, 태양에 가장 가까운 행성입니다. 대기가 없어 낮에는 430도까지 오르고 밤에는 영하 180도까지 떨어집니다. 표면의 흉터는 40억 년 동안 쌓인 충돌의 기록입니다.",
            "금성입니다. 두꺼운 이산화탄소 대기가 열을 가둬, 표면은 납이 녹는 470도 — 태양에 더 가까운 수성보다도 뜨겁습니다. 자전은 거꾸로, 그리고 아주 느립니다. 금성의 하루는 금성의 1년보다 깁니다.",
            "지구 — 지금까지 생명이 확인된 유일한 곳입니다. 밤이 된 쪽에는 도시의 불빛이 반짝입니다. 곁을 도는 달은 지구의 4분의 1 크기로, 행성 대비 위성치고는 유난히 큽니다.",
            "화성입니다. 붉은색은 녹슨 철 — 행성 전체가 녹슬어 있는 셈입니다. 극지방의 하얀 모자는 물얼음과 드라이아이스이고, 태양계에서 가장 큰 화산 올림푸스 몬스가 이곳에 있습니다.",
            "목성 — 나머지 행성을 전부 합친 것보다 두 배 무겁습니다. 줄무늬는 시속 수백 킬로미터의 제트기류이고, 대적점은 지구가 통째로 들어가는 폭풍입니다. 곁을 도는 네 개의 큰 달은 1610년 갈릴레오가 발견했습니다.",
            "토성과 그 고리입니다. 고리는 수십억 개의 얼음 조각 — 폭은 지구 스무 개를 늘어놓을 만큼 넓지만, 두께는 고작 수십 미터입니다. 고리 사이의 검은 틈, 카시니 간극도 실제 모습 그대로입니다.",
            "천왕성은 옆으로 누워서 돕니다. 자전축이 98도 기울어 있어, 극지방에서는 낮과 밤이 각각 42년씩 이어집니다. 대기의 메탄이 붉은빛을 삼켜, 행성은 청록색으로 보입니다.",
            "해왕성 — 여덟 번째, 마지막 행성입니다. 태양 빛이 여기까지 오는 데 4시간이 걸리고, 시속 2천 킬로미터의 태양계에서 가장 빠른 바람이 붑니다. 투어는 여기까지입니다. 이제 은하가 보이는 곳으로 돌아갑니다.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "The Sun. Ninety-nine point eight percent of the solar system's mass is gathered in this one star. The surface pattern comes from real observations — a single dark blemish is larger than Earth.",
            "Mercury — the smallest planet, and the closest to the Sun. With no atmosphere, days reach four hundred thirty degrees and nights fall to minus one hundred eighty. Its scars are four billion years of recorded impacts.",
            "Venus. A thick carbon-dioxide atmosphere traps heat until the surface reaches four hundred seventy degrees — hot enough to melt lead, hotter than Mercury which is closer to the Sun. It spins backwards, and slowly: a Venusian day outlasts its year.",
            "Earth — the only place where life has been found, so far. City lights glitter across the night side. The Moon beside it is a quarter of Earth's size, unusually large for a moon.",
            "Mars. The red is rusted iron — an entire planet gone to rust. The white polar caps are water ice and dry ice, and Olympus Mons, the largest volcano in the solar system, stands here.",
            "Jupiter — twice as heavy as every other planet combined. The stripes are jet streams running at hundreds of kilometres per hour, and the Great Red Spot is a storm that could swallow Earth whole. Its four great moons were found by Galileo in 1610.",
            "Saturn and its rings: billions of pieces of ice, wide enough to line up twenty Earths, yet only tens of metres thick. The dark gap between the rings — the Cassini Division — is right there, as observed.",
            "Uranus rolls on its side. Its axis is tipped ninety-eight degrees, so each pole gets forty-two years of day followed by forty-two years of night. Methane in its atmosphere swallows red light, leaving the planet teal.",
            "Neptune — the eighth and final planet. Sunlight takes four hours to reach here, and the fastest winds in the solar system blow at two thousand kilometres per hour. That is the end of the tour — let's return to where we can see the galaxy.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "太陽です。太陽系全体の質量の99.8パーセントが、このひとつの星に集まっています。表面の模様は実際の観測画像から来たもので、暗い染みひとつが地球より大きいのです。",
            "水星 — いちばん小さく、太陽にいちばん近い惑星です。大気がないため、昼は430度まで上がり、夜は氷点下180度まで下がります。表面の傷跡は、40億年分の衝突の記録です。",
            "金星です。厚い二酸化炭素の大気が熱を閉じ込め、表面は鉛が溶ける470度 — 太陽により近い水星よりも熱いのです。自転は逆向きで、とても遅く、金星の一日は金星の一年より長いのです。",
            "地球 — いまのところ、生命が確認された唯一の場所です。夜の側では都市の明かりがきらめきます。そばを回る月は地球の4分の1の大きさで、衛星としては異例に大きいのです。",
            "火星です。赤い色は錆びた鉄 — 惑星まるごと錆びているようなものです。極地方の白い帽子は水の氷とドライアイスで、太陽系最大の火山オリンポス山がここにあります。",
            "木星 — ほかの惑星を全部合わせたより2倍重い惑星です。縞模様は時速数百キロのジェット気流、大赤斑は地球がまるごと入る嵐です。そばを回る4つの大きな月は、1610年にガリレオが発見しました。",
            "土星とその環です。環は数十億個の氷のかけら — 幅は地球20個を並べるほど広いのに、厚さはわずか数十メートルです。環の間の黒い隙間、カッシーニの間隙も観測どおりの姿です。",
            "天王星は横倒しで回っています。自転軸が98度傾いているため、極地方では昼と夜がそれぞれ42年ずつ続きます。大気のメタンが赤い光を吸い込み、惑星は青緑色に見えます。",
            "海王星 — 8番目、最後の惑星です。太陽の光がここに届くまで4時間かかり、時速2千キロという太陽系最速の風が吹いています。ツアーはここまで。銀河の見える場所へ戻りましょう。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "太阳。太阳系全部质量的百分之九十九点八都聚集在这一颗恒星上。表面的纹理来自真实的观测影像——一个暗斑就比地球还大。",
            "水星——最小的行星，也离太阳最近。没有大气，白天升到430度，夜晚降到零下180度。表面的伤痕是四十亿年撞击的记录。",
            "金星。厚厚的二氧化碳大气锁住热量，表面高达470度——足以熔化铅，比更靠近太阳的水星还热。它逆向自转，而且非常缓慢：金星的一天比它的一年还长。",
            "地球——迄今唯一确认存在生命的地方。夜晚的一面闪烁着城市的灯光。身旁的月球是地球的四分之一大，作为卫星来说异常巨大。",
            "火星。红色来自生锈的铁——整颗行星都锈了。极地的白色冰帽是水冰和干冰，太阳系最大的火山奥林帕斯山就在这里。",
            "木星——比其余行星加起来还重一倍。条纹是时速数百公里的急流，大红斑是一场能吞下整个地球的风暴。身边的四颗大卫星，是伽利略在1610年发现的。",
            "土星和它的环。环由数十亿块冰组成——宽度足以排下二十个地球，厚度却只有几十米。环间的黑色缝隙——卡西尼缝——也和观测到的一模一样。",
            "天王星是躺着转的。自转轴倾斜98度，两极各有42年的白昼和42年的黑夜。大气中的甲烷吞掉红光，行星因此呈现青绿色。",
            "海王星——第八颗，也是最后一颗行星。阳光要走四个小时才能到这里，太阳系最快的风以每小时两千公里呼啸。旅程到此为止——让我们回到能看见银河的地方。",
        };

        static readonly Stop[] Stops =
        {
            new Stop { body = "Sun",     frameMul = 3.6f,
                title = "태양 — 우리 별", titleEn = "The Sun — Our Star",
                titleJa = "太陽 — 私たちの星", titleZh = "太阳——我们的恒星" },
            new Stop { body = "Mercury", frameMul = 5.5f,
                title = "1. 수성", titleEn = "1. Mercury",
                titleJa = "1. 水星", titleZh = "1. 水星" },
            new Stop { body = "Venus",   frameMul = 5.5f,
                title = "2. 금성", titleEn = "2. Venus",
                titleJa = "2. 金星", titleZh = "2. 金星" },
            new Stop { body = "Earth",   frameMul = 6.5f,
                title = "3. 지구와 달", titleEn = "3. Earth and Moon",
                titleJa = "3. 地球と月", titleZh = "3. 地球与月球" },
            new Stop { body = "Mars",    frameMul = 5.5f,
                title = "4. 화성", titleEn = "4. Mars",
                titleJa = "4. 火星", titleZh = "4. 火星" },
            new Stop { body = "Jupiter", frameMul = 5.0f,
                title = "5. 목성", titleEn = "5. Jupiter",
                titleJa = "5. 木星", titleZh = "5. 木星" },
            new Stop { body = "Saturn",  frameMul = 3.4f,   // framing radius already includes the rings
                title = "6. 토성", titleEn = "6. Saturn",
                titleJa = "6. 土星", titleZh = "6. 土星" },
            new Stop { body = "Uranus",  frameMul = 5.5f,
                title = "7. 천왕성", titleEn = "7. Uranus",
                titleJa = "7. 天王星", titleZh = "7. 天王星" },
            new Stop { body = "Neptune", frameMul = 5.5f,
                title = "8. 해왕성", titleEn = "8. Neptune",
                titleJa = "8. 海王星", titleZh = "8. 海王星" },
        };

        public void StartTour()
        {
            if (Running || !Application.isPlaying) return;
            Running = true;
            step = 0;
            driftDeg = 0f;

            savedPos = transform.position;
            savedRot = transform.rotation;
            var cam = GetComponent<Camera>();
            savedNear = cam != null ? cam.nearClipPlane : 0.02f;
            savedFar = cam != null ? cam.farClipPlane : 600f;
            // Planets are ~0.4–4.5 units here; the scene default near (0.02)
            // is fine but the far plane must still reach the galaxy backdrop.
            if (cam != null) { cam.nearClipPlane = 0.05f; cam.farClipPlane = 20000f; }

            rig = SolarSystemRig.Spawn(RigPosition);
            rig.gameObject.name = "Solar System (tour)";
            rig.transform.localScale = Vector3.one * RigScale;
            rig.motionScale = tourMotionScale;
            // Orbit guide lines read well from a distance but become ribbons
            // across a close-up frame.
            rig.SetOrbitLinesVisible(false);

            if (orbit != null) orbit.enabled = false;
            ApplyStep();
        }

        public void StopTour()
        {
            if (!Running) return;
            Running = false;
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            if (rig != null) Destroy(rig.gameObject);
            rig = null;

            var cam = GetComponent<Camera>();
            if (cam != null) { cam.nearClipPlane = savedNear; cam.farClipPlane = savedFar; }
            // Unlike the galaxy tour we CANNOT stay where we are: the tour
            // parks the camera 88 kpc out at planet scale, useless once the
            // rig is gone. Return to where the visitor left the galaxy.
            transform.position = savedPos;
            transform.rotation = savedRot;
            if (orbit != null) orbit.enabled = true;
        }

        public void Next() { if (Running && step < Stops.Length - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        /// <summary>Re-applies the current step after a language toggle so the
        /// card text and narration voice switch immediately.</summary>
        public void OnLanguageChanged()
        {
            if (Running) ApplyStep();
        }

        void ApplyStep()
        {
            fromPos = transform.position;
            fromLook = transform.position + transform.forward * 10f;
            glideT = 0f;

            NarrationManager.Instance.Play("mw_sol_" + step);

            var s = Stops[step];
            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = Loc.T(s.title, s.titleEn, s.titleJa, s.titleZh);
            cardBody.text = Loc.T(NarrationLines[step], NarrationLinesEn[step],
                                  NarrationLinesJa[step], NarrationLinesZh[step]);
            cardFooter.text = Loc.T("N 다음    B 이전    F6 종료",
                                    "N Next    B Prev    F6 End",
                                    "N 次へ    B 前へ    F6 終了",
                                    "N 下一步    B 上一步    F6 结束")
                            + "                                  " + (step + 1) + " / " + Stops.Length;
        }

        void Update()
        {
            if (!Running || rig == null) return;

            var body = rig.GetBody(Stops[step].body);
            if (body == null) return;

            // Live target pose: sun-lit three-quarter view of wherever the
            // body is THIS frame, drifting slowly around it for parallax.
            driftDeg += 2.2f * Time.deltaTime;
            Vector3 bodyPos = body.position;
            float radius = FramingRadius(body);
            Vector3 toSun = (rig.transform.position - bodyPos);
            toSun = toSun.sqrMagnitude < 1e-6f ? Vector3.forward : toSun.normalized; // the Sun stop looks at the Sun itself
            Vector3 side = Vector3.Cross(toSun, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.5f) side = Vector3.right;
            Vector3 baseDir = (toSun * 0.75f + side * 0.55f + Vector3.up * 0.28f).normalized;
            Vector3 camDir = Quaternion.AngleAxis(driftDeg, Vector3.up) * baseDir;
            Vector3 targetPos = bodyPos + camDir * radius * Stops[step].frameMul;

            if (glideT < 1f)
            {
                glideT = Mathf.Min(1f, glideT + Time.deltaTime / Mathf.Max(glideDuration, 0.1f));
                float u = Mathf.SmoothStep(0f, 1f, glideT);
                transform.position = Vector3.Lerp(fromPos, targetPos, u);
                transform.LookAt(Vector3.Lerp(fromLook, bodyPos, u));
            }
            else
            {
                transform.position = targetPos;
                transform.LookAt(bodyPos);
            }
        }

        /// <summary>Framing radius: the largest renderer under the body's
        /// pivot, so Saturn's rings and Earth's moon set their own frame.
        /// bounds.extents of a radius-r sphere is (r,r,r) — magnitude r√3,
        /// hence the 0.577.</summary>
        static float FramingRadius(Transform body)
        {
            float r = 0.5f;
            foreach (var mr in body.GetComponentsInChildren<MeshRenderer>())
                r = Mathf.Max(r, mr.bounds.extents.magnitude * 0.577f);
            return r;
        }

        // ---------------- card UI (the shared factory) -----------------------

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());

            card = BlackHoleUI.MakePanel(canvas.transform, "Sol Tour Card",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(920f, 218f));

            cardTitle = BlackHoleUI.MakeText(card, "Title", 26, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(860f, 34f), FontStyle.Bold);

            cardBody = BlackHoleUI.MakeText(card, "Body", 20, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -58f), new Vector2(860f, 118f));
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            cardFooter = BlackHoleUI.MakeText(card, "Footer", 15, BlackHoleUI.TextSecondary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 12f), new Vector2(860f, 22f));
        }

        void OnDisable()
        {
            if (Running) StopTour();
        }
    }
}
