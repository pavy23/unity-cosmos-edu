using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// "이론 배경" card (X key, auto-shown at 고등 difficulty): the actual
    /// governing equation behind whatever is currently on screen, with a
    /// two-line plain-language reading of it. Context-sensitive — follows the
    /// guided tour step when the tour runs, otherwise picks the active demo.
    /// Play mode only.
    /// </summary>
    public class TheoryPanel : MonoBehaviour
    {
        public GuidedTour tour;
        public EinsteinRingDemo einstein;
        public SpaghettificationDemo spaghetti;
        public RelativisticJets jets;
        public PhotonLauncher launcher;
        public BlackHoleController controller;
        public BinaryMergerCinematic binary;

        public bool Visible { get; private set; }

        RectTransform panel;
        Text title, formula, body, hint;
        int currentKey = -1;

        struct Card
        {
            public string title, formula, body;
            // formulaEn null → the formula is language-neutral (used as-is for
            // every non-Korean language).
            public string titleEn, formulaEn, bodyEn;
            public string titleJa, bodyJa;
            public string titleZh, bodyZh;
        }

        // Index 0..10 mirror the tour steps; 11 = free-exploration default.
        static readonly Card[] Cards =
        {
            new Card { title = "측지선 방정식 — 이 화면의 전부",
                formula = "d²x/dλ² = −(3/2) h² x / r⁵    (GM = c = 1, Rs = 2)",
                body = "일반상대성이론의 빛 궤도 방정식을 매 픽셀 수치적분합니다.\nh = |x × v| 는 보존되는 각운동량 — 화면의 모든 휘어짐이 이 식의 출력입니다.",
                titleEn = "The Geodesic Equation — everything on screen",
                bodyEn = "GR's light-path equation, numerically integrated per pixel.\nh = |x × v| is the conserved angular momentum — every bend you see is its output.",
                titleJa = "測地線方程式 — この画面のすべて",
                bodyJa = "一般相対論の光の軌道方程式を、ピクセルごとに数値積分しています。\nh = |x × v| は保存される角運動量 — 画面のあらゆる曲がりはこの式の出力です。",
                titleZh = "测地线方程 — 画面上的一切",
                bodyZh = "广义相对论的光线轨道方程，正被逐像素数值积分。\nh = |x × v| 是守恒的角动量 — 你看到的每一处弯曲都是它的输出。" },
            new Card { title = "그림자의 크기",
                formula = "b_c = (3√3 ⁄ 2) Rs ≈ 2.6 Rs",
                body = "임계 충돌계수 b_c 안쪽으로 조준된 빛은 전부 포획됩니다.\n그림자가 지평선보다 2.6배 커 보이는 이유 — 적분에서 저절로 나오는 값입니다.",
                titleEn = "Size of the Shadow",
                bodyEn = "Any light aimed inside the critical impact parameter b_c is captured.\nThat is why the shadow looks 2.6× larger than the horizon — it falls out of the integration.",
                titleJa = "影の大きさ",
                bodyJa = "臨界衝突パラメータ b_c の内側を通る光は、すべて捕獲されます。\n影が地平面より2.6倍大きく見える理由 — 積分から自然に出てくる値です。",
                titleZh = "阴影的大小",
                bodyZh = "凡是瞄准临界碰撞参数 b_c 以内的光，都会被捕获。\n这就是阴影看起来比视界大2.6倍的原因 — 它是积分自然得出的值。" },
            new Card { title = "광자 고리 — 빛의 원 궤도",
                formula = "r_ph = 1.5 Rs    (유효 퍼텐셜의 불안정 원궤도)",
                body = "빛이 블랙홀을 몇 바퀴 돌 수 있는 유일한 반지름.\n불안정하므로 결국 새어 나와 그림자 가장자리의 밝은 테두리가 됩니다.",
                titleEn = "Photon Ring — light on a circular orbit",
                formulaEn = "r_ph = 1.5 Rs    (unstable circular orbit)",
                bodyEn = "The one radius where light can circle the hole.\nBeing unstable, it eventually leaks out — the bright rim at the shadow's edge.",
                titleJa = "光子リング — 光の円軌道",
                bodyJa = "光がブラックホールを周回できる唯一の半径。\n不安定なのでやがて漏れ出し、影のふちの明るいリングになります。",
                titleZh = "光子环 — 光的圆轨道",
                bodyZh = "这是光能绕黑洞转圈的唯一半径。\n因其不稳定，光终会泄出 — 成为阴影边缘明亮的光环。" },
            new Card { title = "강착원반의 온도 구조",
                formula = "r_ISCO = 3 Rs ,    T(r) ∝ r^(−3/4)",
                body = "샤쿠라–수냐예프 박원반: 안쪽일수록 뜨겁고 밝습니다.\nISCO(최내안정궤도) 안쪽에는 안정된 원궤도가 존재할 수 없습니다.",
                titleEn = "Temperature Structure of the Disk",
                bodyEn = "Shakura–Sunyaev thin disk: hotter and brighter toward the center.\nInside the ISCO (innermost stable circular orbit) no stable orbit exists.",
                titleJa = "降着円盤の温度構造",
                bodyJa = "シャクラ＝スニャーエフの薄い円盤：内側ほど高温で明るい。\nISCO（最内安定円軌道）の内側には、安定した円軌道は存在できません。",
                titleZh = "吸积盘的温度结构",
                bodyZh = "沙库拉–苏尼亚耶夫薄盘：越靠内越热越亮。\n在ISCO（最内稳定圆轨道）以内，不存在稳定的圆轨道。" },
            new Card { title = "도플러 비밍 — 왜 한쪽이 밝은가",
                formula = "I_obs = (δ·g)³ I_em ,    δ = 1 ⁄ (1 − β cosθ)",
                body = "가스가 ISCO 근처에서 광속의 50%로 돕니다(β=0.5).\n광자수 보존(I/ν³ 불변)에서 지수 3이 나옵니다 — 다가오는 쪽이 δ³배 밝아집니다.",
                titleEn = "Doppler Beaming — why one side is brighter",
                bodyEn = "Gas near the ISCO orbits at 50% of light speed (β = 0.5).\nPhoton-number conservation (I/ν³ invariant) gives the power of 3 — the approaching side glows δ³ brighter.",
                titleJa = "ドップラービーミング — なぜ片側が明るいのか",
                bodyJa = "ガスはISCO付近を光速の50%で回っています（β=0.5）。\n光子数の保存（I/ν³ 不変）から指数3が出ます — 近づく側は δ³ 倍明るくなります。",
                titleZh = "多普勒聚束 — 为何一侧更亮",
                bodyZh = "气体在ISCO附近以光速的50%旋转（β=0.5）。\n光子数守恒（I/ν³ 不变）给出3次方 — 靠近的一侧亮 δ³ 倍。" },
            new Card { title = "중력 시간 지연",
                formula = "dτ = dt · √(1 − Rs ⁄ r)",
                body = "r = 1.2 Rs 에서 √(1 − 1/1.2) ≈ 0.41.\n밖에서 1시간이 흐르는 동안 탐사선의 시계는 약 24분만 갑니다 — 정확한 계산값.",
                titleEn = "Gravitational Time Dilation",
                bodyEn = "At r = 1.2 Rs, √(1 − 1/1.2) ≈ 0.41.\nWhile one hour passes outside, the probe's clock ticks only ~24 minutes — the exact value.",
                titleJa = "重力による時間の遅れ",
                bodyJa = "r = 1.2 Rs では √(1 − 1/1.2) ≈ 0.41。\n外で1時間たつ間に、探査機の時計は約24分しか進みません — 正確な計算値です。",
                titleZh = "引力时间膨胀",
                bodyZh = "在 r = 1.2 Rs 处，√(1 − 1/1.2) ≈ 0.41。\n外面过去1小时，探测器的时钟只走约24分钟 — 这是精确的计算值。" },
            new Card { title = "아인슈타인 링",
                formula = "θ_E = √( 4GM·D_LS ⁄ (c² D_L D_S) )",
                body = "광원–렌즈–관찰자가 일직선이면 상이 완전한 고리가 됩니다.\n이 데모는 공식 대신 측지선 적분이 직접 링을 만들어냅니다.",
                titleEn = "The Einstein Ring",
                bodyEn = "When source, lens and observer align, the image becomes a full ring.\nHere the geodesic integration itself produces the ring — no formula needed.",
                titleJa = "アインシュタインリング",
                bodyJa = "光源・レンズ・観測者が一直線に並ぶと、像は完全なリングになります。\nこのデモでは公式ではなく、測地線の積分そのものがリングを作り出します。",
                titleZh = "爱因斯坦环",
                bodyZh = "当光源–透镜–观察者成一直线时，像会变成完整的圆环。\n这个演示不用公式 — 是测地线积分本身直接生成了圆环。" },
            new Card { title = "빛의 궤적 — 비네 방정식",
                formula = "u″ + u = 3u²    (u = 1/r)",
                body = "뉴턴 궤도와의 차이가 우변의 3u² 항입니다.\nb < 2.6 Rs 면 포획(빨강), 크면 휘어져 탈출(파랑) — Space로 직접 실험해보세요.",
                titleEn = "Light Trajectories — the Binet Equation",
                bodyEn = "The 3u² term on the right is the difference from Newtonian orbits.\nb < 2.6 Rs → captured (red); larger → bent but escaping (blue). Try Space yourself.",
                titleJa = "光の軌跡 — ビネ方程式",
                bodyJa = "ニュートン軌道との違いが右辺の 3u² 項です。\nb < 2.6 Rs なら捕獲（赤）、大きければ曲がって脱出（青）— Spaceで実験できます。",
                titleZh = "光的轨迹 — 比奈方程",
                bodyZh = "与牛顿轨道的区别就在右边的 3u² 项。\nb < 2.6 Rs 被捕获（红色）；更大则弯曲后逃逸（蓝色）— 按空格键亲自实验。" },
            new Card { title = "조석력 — 스파게티화",
                formula = "Δa ≈ 2GM·L ⁄ r³",
                body = "머리와 발(길이 L)에 걸리는 중력의 차이가 r³에 반비례해 폭증합니다.\n작은 블랙홀일수록 지평선 밖에서 이미 찢깁니다 — 4번(항성급)으로 바꿔 상상해보세요.",
                titleEn = "Tidal Force — Spaghettification",
                bodyEn = "The gravity difference across a body of length L explodes as 1/r³.\nSmaller holes tear you apart well outside the horizon — try preset 4 (stellar) and imagine.",
                titleJa = "潮汐力 — スパゲッティ化",
                bodyJa = "頭と足（長さL）にかかる重力の差は、r³ に反比例して急増します。\n小さいブラックホールほど、地平面の外ですでに引き裂かれます — 4番（恒星質量）で想像してみてください。",
                titleZh = "潮汐力 — 面条化",
                bodyZh = "头脚之间（长度L）的引力差随 1/r³ 急剧增大。\n黑洞越小，在视界之外就已把你撕裂 — 按4号（恒星级）体会一下。" },
            new Card { title = "상대론적 제트",
                formula = "P_jet ∝ B² a² M²    (블랜드포드–즈나젝)",
                body = "회전하는 블랙홀의 에너지를 자기장이 뽑아 양극으로 분출합니다.\n(회전 효과라 이 씬에서는 개념 연출 — 렌징처럼 적분된 결과는 아닙니다.)",
                titleEn = "Relativistic Jets",
                formulaEn = "P_jet ∝ B² a² M²    (Blandford–Znajek)",
                bodyEn = "Magnetic fields extract a spinning hole's rotational energy and blast it from the poles.\n(A spin effect — visualized conceptually here, unlike the fully-integrated lensing.)",
                titleJa = "相対論的ジェット",
                bodyJa = "回転するブラックホールのエネルギーを磁場が引き抜き、両極から噴き出します。\n（回転の効果なので、このシーンでは概念的な演出 — レンズ効果のような積分結果ではありません。）",
                titleZh = "相对论性喷流",
                bodyZh = "磁场抽取旋转黑洞的能量，并从两极喷射出去。\n（这是自旋效应，此处为概念演示 — 不同于经过完整积分的透镜效应。）" },
            new Card { title = "모든 것의 근원",
                formula = "G_μν = (8πG ⁄ c⁴) T_μν",
                body = "아인슈타인 장방정식 — 좌변은 시공간의 곡률, 우변은 물질과 에너지.\n\"물질은 시공간을 휘게 하고, 휘어진 시공간은 물질의 길을 정한다.\"",
                titleEn = "The Source of Everything",
                bodyEn = "Einstein's field equations — curvature of spacetime on the left, matter and energy on the right.\n\"Matter tells spacetime how to curve; spacetime tells matter how to move.\"",
                titleJa = "すべての源",
                bodyJa = "アインシュタイン方程式 — 左辺は時空の曲率、右辺は物質とエネルギー。\n「物質は時空の曲がり方を教え、時空は物質の進み方を教える。」",
                titleZh = "一切的根源",
                bodyZh = "爱因斯坦场方程 — 左边是时空的曲率，右边是物质与能量。\n“物质告诉时空如何弯曲，时空告诉物质如何运动。”" },
            new Card { title = "지금 보고 있는 것",
                formula = "d²x/dλ² = −(3/2) h² x / r⁵ ,    I_obs = (δ·g)³ I_em",
                body = "상의 기하(그림자·고리·렌즈 상)는 측지선 적분, 밝기와 색은 상대론적\n편이의 결과입니다. 가이드 투어를 켜면 단계마다 해당 수식이 여기 표시됩니다.",
                titleEn = "What You Are Looking At",
                bodyEn = "The image geometry (shadow, rings, lensed arcs) comes from geodesic integration;\nbrightness and color from relativistic shifts. Start the guided tour for per-step equations.",
                titleJa = "いま見ているもの",
                bodyJa = "像の形（影・リング・レンズ像）は測地線の積分、明るさと色は相対論的\n偏移の結果です。ガイドツアーを始めると、各ステップの数式がここに表示されます。",
                titleZh = "你正在看的东西",
                bodyZh = "图像的几何（阴影、光环、透镜像）来自测地线积分；亮度与颜色来自相对论\n偏移。开启导览后，每一步的公式都会显示在这里。" },
            new Card { title = "중력파 병합 — 시공간의 소리",
                formula = "f_GW = 2 f_orb ,    ℳ = (m₁m₂)^(3/5) ⁄ (m₁+m₂)^(1/5)",
                body = "중력파 진동수는 공전의 2배로 올라가는 '처프'입니다(지금 소리가 실제 궤도와 동기화). 파형을 결정하는 것이 처프 질량 ℳ. 병합 후 질량의 ~5%가 파동으로 방출됩니다. (이중 렌징은 중첩 근사)",
                titleEn = "GW Merger — the sound of spacetime",
                bodyEn = "The wave frequency is twice the orbital frequency — the rising 'chirp' (the sound you hear tracks the actual orbit). The chirp mass ℳ shapes the waveform; ~5% of the total mass is radiated away. (Dual lensing is a superposition approximation.)",
                titleJa = "重力波の合体 — 時空の音",
                bodyJa = "重力波の周波数は公転の2倍で上がっていく「チャープ」です（いまの音は実際の軌道と同期）。波形を決めるのがチャープ質量 ℳ。合体で全質量の約5%が波として放出されます。（二重レンズは重ね合わせ近似）",
                titleZh = "引力波并合 — 时空之声",
                bodyZh = "引力波频率是公转频率的2倍，不断升高 — 这就是“啁啾”（你听到的声音与真实轨道同步）。决定波形的是啁啾质量 ℳ。并合会把总质量的约5%以波的形式辐射掉。（双重透镜为叠加近似）" },
            new Card { title = "커(회전) 블랙홀 — 끌려 도는 시공간",
                formula = "r₊ = M + √(M² − a²) ,    ISCO : 6M → 1.2M",
                body = "회전이 시공간 자체를 끌고 돕니다(프레임 드래깅). 같이 도는 쪽의 빛은 더 쉽게 살아남아 그림자가 D자로 찌그러지고, ISCO가 안쪽으로 파고들어 원반이 지평선 코앞까지 붙습니다. (커–실트 좌표 실계산)",
                titleEn = "Kerr (Spinning) Black Hole — dragged spacetime",
                bodyEn = "Rotation drags spacetime itself around (frame dragging). Co-rotating light survives more easily, so the shadow squashes into a D-shape, and the ISCO plunges inward — the disk hugs the horizon. (Real Kerr-Schild integration)",
                titleJa = "カー（回転）ブラックホール — 引きずられる時空",
                bodyJa = "回転は時空そのものを引きずって回します（フレームドラッギング）。共回転する光は生き残りやすく、影はD字形にゆがみ、ISCOは内側へ食い込んで円盤が地平面のすぐそばまで迫ります。（カー＝シルト座標での実計算）",
                titleZh = "克尔（旋转）黑洞 — 被拖曳的时空",
                bodyZh = "旋转会拖着时空本身一起转（参考系拖曳）。与黑洞同向旋转的光更容易幸存，因此阴影被压成D形；ISCO向内深入，吸积盘一直贴到视界跟前。（克尔–席尔德坐标下的真实计算）" },
        };

        public void Toggle() => SetVisible(!Visible);

        public void SetVisible(bool on)
        {
            Visible = on;
            if (!on) { if (panel != null) panel.gameObject.SetActive(false); return; }
            currentKey = -1; // force refresh
            Refresh();
        }

        void Update()
        {
            if (Visible) Refresh();
        }

        void Refresh()
        {
            // Language is part of the dirty-key so a toggle refreshes the card.
            int key = ContextKey() + Loc.Version * 100;
            if (key == currentKey) return;
            currentKey = key;
            Ensure();
            panel.gameObject.SetActive(true);
            var c = Cards[ContextKey()];
            title.text = Loc.T("이론 배경 — " + c.title, "Theory — " + c.titleEn,
                               "理論背景 — " + c.titleJa, "理论背景 — " + c.titleZh);
            formula.text = Loc.NonKorean && c.formulaEn != null ? c.formulaEn : c.formula;
            body.text = Loc.T(c.body, c.bodyEn, c.bodyJa, c.bodyZh);
            if (hint != null) hint.text = Loc.T("X 닫기 · C 난이도", "X close · C level",
                                                "X 閉じる · C 難易度", "X 关闭 · C 难度");
        }

        int ContextKey()
        {
            if (binary != null && binary.Running) return 12;          // merger card
            if (tour != null && tour.Running) return Mathf.Clamp(tour.CurrentStep, 0, 10);
            if (einstein != null && einstein.active) return 6;
            if (spaghetti != null && spaghetti.active) return 8;
            if (jets != null && jets.active) return 9;
            if (launcher != null && launcher.HasTrails) return 7;
            if (controller != null && controller.spin > 0.001f) return 13; // Kerr card
            return 11;
        }

        void Ensure()
        {
            if (panel != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());

            panel = BlackHoleUI.MakePanel(canvas.transform, "Theory Panel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(470f, 200f));

            // 370 not 430: the close button now occupies the top-right corner.
            title = BlackHoleUI.MakeText(panel, "Title", 18, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(370f, 26f), FontStyle.Bold);

            formula = BlackHoleUI.MakeText(panel, "Formula", 21, BlackHoleUI.Accent, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(430f, 50f), FontStyle.Bold);

            body = BlackHoleUI.MakeText(panel, "Body", 15, BlackHoleUI.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -102f), new Vector2(430f, 70f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap; // long lines stay inside the card

            hint = BlackHoleUI.MakeText(panel, "Hint", 13, BlackHoleUI.TextSecondary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 8f), new Vector2(260f, 18f));
            hint.text = Loc.T("'설명 난이도'로 깊이를 바꿉니다", "'Level' changes the depth",
                              "「難易度」で深さを変えられます", "用「难度」切换深度");

            // A real close control: the hint used to promise an X key that was
            // never bound, and the panel covers the top-right corner.
            BlackHoleUI.MakeButton(panel, "Theory Close", "×",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f),
                new Vector2(48f, 48f), () => SetVisible(false));
        }
    }
}
