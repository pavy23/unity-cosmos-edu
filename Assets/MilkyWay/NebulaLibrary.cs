using UnityEngine;
using BlackHoleEffect; // Loc

namespace MilkyWay
{
    /// <summary>
    /// The nebulae &amp; clusters exhibit's specimen table — one entry per hero
    /// object, carrying both its rendering recipe (which NebulaVolume preset or
    /// cluster kind, plus embedded-star config) and its museum-label metadata
    /// (four-language name, facts strip, blurb) and gallery placement. One place
    /// so the scene builder, the controller and the gallery all agree.
    /// </summary>
    public static class NebulaLibrary
    {
        public enum Form { Nebula, Cluster }

        public struct Hero
        {
            public string id;
            public Form form;
            public Vector3 position;
            public float framing;   // camera distance in object radii

            // --- nebula recipe (Form.Nebula) ---
            public int type;        // 0 emit 1 refl 2 planetary 3 snr 4 dark
            public Color color1, color2;
            public float brightness, radius, density, noiseScale, filament, threshold, dust;
            public float shellRadius, shellThickness;
            public bool backdrop;   // dark nebula: spawn a red emission cloud behind
            public Vector3 stretch; // non-uniform volume scale (zero => uniform)

            // --- embedded stars ---
            public Color starBright, starField;
            public int brightN, fieldN;
            public float coreFrac;  // bright-star spread, in radii

            // --- cluster recipe (Form.Cluster) ---
            public ClusterField.Kind clusterKind;
            public int clusterStars;
            public float clusterRadius;

            // --- per-specimen background (the sky this object really sits in) ---
            public float bgDensity;     // point-star density
            public float bgGalaxies;    // distant-galaxy smudges (halo objects)
            public Color bgTint;        // star colour cast (zero => white)
            public float bgBand;        // Milky-Way band strength (in-plane only)
            public Color bgBandColor;
            public Vector3 bgBandAxis;  // band pole (band = the perpendicular circle)

            // --- label ---
            public System.Func<string> name, facts, blurb;
        }

        public static readonly Hero[] Heroes =
        {
            // 1) Orion Nebula — emission / stellar nursery
            new Hero {
                id = "Orion", form = Form.Nebula, position = new Vector3(0, 0, 0), framing = 3.2f,
                type = 0, color1 = new Color(2.3f, 0.5f, 0.85f), color2 = new Color(0.35f, 1.7f, 1.25f),
                brightness = 1.5f, radius = 8f, density = 0.95f, noiseScale = 0.30f, filament = 1.7f,
                threshold = 0.44f, dust = 1.5f,
                starBright = new Color(0.75f, 1.15f, 1.35f), starField = new Color(1.2f, 1.05f, 0.95f),
                brightN = 5, fieldN = 70, coreFrac = 0.16f,
                // In the winter Milky Way: a dense, warm star field (a smooth band
                // would fishbowl the nebula's bounding sphere, so richness comes
                // from stars, not glow).
                bgDensity = 0.9f, bgGalaxies = 0f, bgTint = new Color(1.0f, 0.95f, 0.88f), bgBand = 0f,
                name = () => Loc.T("오리온 대성운 (M42)", "The Orion Nebula (M42)", "オリオン大星雲 (M42)", "猎户座大星云 (M42)"),
                facts = () => Loc.T("발광성운 · 1,344광년 · 지름 24광년 · 별이 태어나는 곳",
                                    "Emission nebula · 1,344 ly · 24 ly across · a stellar nursery",
                                    "散光星雲 · 1,344光年 · 直径24光年 · 星の誕生地",
                                    "发射星云 · 1,344光年 · 直径24光年 · 恒星诞生地"),
                blurb = () => Loc.T("맨눈으로 보이는 가장 가까운 별 탄생 지역. 중심의 트라페지움 — 갓 태어난 뜨거운 별 넷 — 이 뿜는 자외선이 수소 가스를 달궈 분홍빛으로 빛나게 하고, 그 복사압이 성운에 공동을 파냅니다.",
                                    "The nearest stellar nursery visible to the naked eye. At its heart the Trapezium — four hot newborn stars — floods the hydrogen with ultraviolet light, making it glow pink, while its radiation carves a cavity in the gas.",
                                    "肉眼で見える最も近い星の誕生領域。中心のトラペジウム — 生まれたばかりの高温の星4つ — が放つ紫外線が水素ガスを熱し、ピンクに輝かせ、その放射圧が星雲に空洞を刻みます。",
                                    "肉眼可见的最近的恒星诞生区。中心的猎户四边形星团——四颗炽热的新生恒星——释放的紫外线加热氢气使其发出粉红色光芒，其辐射压在星云中雕出空腔。") },

            // 2) Horsehead — dark nebula (with a red emission backdrop)
            new Hero {
                id = "Horsehead", form = Form.Nebula, position = new Vector3(42, 4, -8), framing = 4.6f,
                type = 4, color1 = new Color(0.9f, 0.55f, 0.4f), color2 = new Color(0.4f, 0.5f, 0.7f),
                brightness = 1.0f, radius = 7f, density = 1.9f, noiseScale = 0.35f, filament = 1.3f,
                threshold = 0.4f, dust = 3.0f, backdrop = true,
                starBright = Color.white, starField = Color.white, brightN = 0, fieldN = 0, coreFrac = 1f,
                bgDensity = 0.9f, bgGalaxies = 0f, bgTint = new Color(1.0f, 0.94f, 0.86f), bgBand = 0f,
                name = () => Loc.T("말머리 성운 (B33)", "The Horsehead Nebula (B33)", "馬頭星雲 (B33)", "马头星云 (B33)"),
                facts = () => Loc.T("암흑성운 · 1,375광년 · 붉은 성운 앞의 먼지 실루엣",
                                    "Dark nebula · 1,375 ly · a dust silhouette against red glow",
                                    "暗黒星雲 · 1,375光年 · 赤い星雲を背にした塵の影",
                                    "暗星云 · 1,375光年 · 红色星云前的尘埃剪影"),
                blurb = () => Loc.T("성운은 빛나기만 하는 게 아닙니다. 이 말의 머리는 짙은 먼지 구름이 뒤편 붉은 발광성운의 빛을 가려 만든 실루엣입니다. 그 차가운 먼지 속에서 지금도 새 별이 잉태되고 있습니다.",
                                    "Nebulae don't only glow. This horse's head is a silhouette — a dense dust cloud blocking the red emission nebula behind it. Inside that cold dust, new stars are still being conceived.",
                                    "星雲は輝くだけではありません。この馬の頭は、濃い塵の雲が背後の赤い散光星雲の光を遮ってできた影です。その冷たい塵の中で、今も新しい星が宿されています。",
                                    "星云不只会发光。这个马头是一道剪影——浓密的尘埃云挡住了它身后红色发射星云的光。在那寒冷的尘埃中，新的恒星仍在孕育。") },

            // 3) Pleiades — reflection nebula + open cluster
            new Hero {
                id = "Pleiades", form = Form.Nebula, position = new Vector3(-40, -3, 14), framing = 3.4f,
                type = 1, color1 = new Color(0.5f, 0.8f, 2.3f), color2 = new Color(0.6f, 0.8f, 1.6f),
                brightness = 2.6f, radius = 7f, density = 0.5f, noiseScale = 0.40f, filament = 1.5f,
                threshold = 0.6f, dust = 0.8f,
                starBright = new Color(0.8f, 0.95f, 1.5f), starField = new Color(0.85f, 0.95f, 1.3f),
                brightN = 10, fieldN = 40, coreFrac = 0.6f,
                bgDensity = 0.5f, bgGalaxies = 0f, bgTint = new Color(0.85f, 0.9f, 1.05f), bgBand = 0f,
                name = () => Loc.T("플레이아데스 (M45)", "The Pleiades (M45)", "プレアデス星団 (M45)", "昴星团 (M45)"),
                facts = () => Loc.T("반사성운 + 산개성단 · 444광년 · 1억 살 · 별 약 1,000개",
                                    "Reflection nebula + open cluster · 444 ly · 100 Myr · ~1,000 stars",
                                    "反射星雲＋散開星団 · 444光年 · 1億歳 · 約1,000個の星",
                                    "反射星云＋疏散星团 · 444光年 · 1亿岁 · 约1,000颗恒星"),
                blurb = () => Loc.T("‘일곱 자매’. 젊고 뜨거운 파란 별들이 우연히 지나던 먼지 구름을 비추어, 그 별빛을 되반사한 푸른 안개가 성단을 감쌉니다. 발광성운과 달리 가스가 스스로 빛나는 게 아니라 별빛을 거울처럼 되비추는 것입니다.",
                                    "The 'Seven Sisters'. Young hot blue stars light up a dust cloud they happen to be drifting through; the dust mirrors their blue light back as a haze around the cluster. Unlike an emission nebula, the gas doesn't glow on its own — it reflects starlight.",
                                    "「七姉妹」。若く高温の青い星々が、たまたま通りかかった塵の雲を照らし、その星明かりを反射した青い霧が星団を包みます。散光星雲と違い、ガス自体が光るのではなく、星の光を鏡のように反射しているのです。",
                                    "“七姐妹”。年轻炽热的蓝色恒星照亮了它们恰好穿过的尘埃云；尘埃将蓝光反射回来，形成环绕星团的蓝雾。与发射星云不同，气体本身并不发光，而是像镜子一样反射星光。") },

            // 4) Ring Nebula — planetary
            new Hero {
                id = "Ring", form = Form.Nebula, position = new Vector3(26, -22, 30), framing = 3.6f,
                type = 2, color1 = new Color(2.1f, 0.5f, 0.5f), color2 = new Color(0.3f, 1.7f, 1.35f),
                brightness = 1.0f, radius = 5f, density = 0.9f, noiseScale = 0.7f, filament = 0.8f,
                threshold = 0.3f, dust = 0.6f, shellRadius = 0.6f, shellThickness = 0.15f,
                stretch = new Vector3(1.12f, 0.9f, 1.0f),
                starBright = new Color(0.9f, 0.95f, 1.1f), starField = new Color(0.9f, 0.9f, 1.0f),
                brightN = 0, fieldN = 22, coreFrac = 1.6f,
                bgDensity = 0.35f, bgGalaxies = 0.1f, bgTint = new Color(0.95f, 0.95f, 1.0f), bgBand = 0f,
                name = () => Loc.T("고리 성운 (M57)", "The Ring Nebula (M57)", "環状星雲 (M57)", "环状星云 (M57)"),
                facts = () => Loc.T("행성상성운 · 2,570광년 · 지름 1광년 · 태양 같은 별의 죽음",
                                    "Planetary nebula · 2,570 ly · 1 ly across · a Sun-like star's death",
                                    "惑星状星雲 · 2,570光年 · 直径1光年 · 太陽のような星の死",
                                    "行星状星云 · 2,570光年 · 直径1光年 · 类太阳恒星之死"),
                blurb = () => Loc.T("태양 정도의 별이 맞는 잔잔한 최후. 연료가 떨어진 별이 바깥층을 부드럽게 벗어던져 고리 모양으로 부풀고, 남은 뜨거운 핵 — 백색왜성 — 이 그 껍질을 달궈 빛나게 합니다. 이름과 달리 행성과는 무관합니다.",
                                    "The gentle death of a Sun-like star. Out of fuel, it sheds its outer layers into an expanding ring, and the hot leftover core — a white dwarf — lights the shell up. Despite the name, planetary nebulae have nothing to do with planets.",
                                    "太陽ほどの星が迎える穏やかな最期。燃料が尽きた星が外層をそっと脱ぎ捨てて環状に膨らみ、残った高温の核 — 白色矮星 — がその殻を照らします。名前とは裏腹に、惑星とは無関係です。",
                                    "类太阳恒星平静的终结。燃料耗尽的恒星轻轻抛出外层，膨胀成一个环，残余的炽热核心——白矮星——点亮了这层壳。尽管名为“行星状”，却与行星毫无关系。") },

            // 5) Helix Nebula — a nearer, broader planetary ("the Eye of God")
            new Hero {
                id = "Helix", form = Form.Nebula, position = new Vector3(8, 30, 40), framing = 3.3f,
                type = 2, color1 = new Color(2.0f, 0.55f, 0.6f), color2 = new Color(0.4f, 1.45f, 1.4f),
                brightness = 0.95f, radius = 6.5f, density = 0.9f, noiseScale = 0.6f, filament = 0.9f,
                threshold = 0.3f, dust = 0.6f, shellRadius = 0.55f, shellThickness = 0.2f,
                starBright = new Color(0.9f, 0.95f, 1.1f), starField = new Color(0.9f, 0.92f, 1.0f),
                brightN = 0, fieldN = 26, coreFrac = 1.5f,
                bgDensity = 0.3f, bgGalaxies = 0.5f, bgTint = new Color(0.92f, 0.94f, 1.0f), bgBand = 0f,
                name = () => Loc.T("나선 성운 (NGC 7293)", "The Helix Nebula (NGC 7293)", "らせん星雲 (NGC 7293)", "螺旋星云 (NGC 7293)"),
                facts = () => Loc.T("행성상성운 · 655광년 · 가장 가까운 행성상성운의 하나 · ‘신의 눈’",
                                    "Planetary nebula · 655 ly · one of the nearest · the 'Eye of God'",
                                    "惑星状星雲 · 655光年 · 最も近い惑星状星雲の一つ · 「神の目」",
                                    "行星状星云 · 655光年 · 最近的行星状星云之一 · “上帝之眼”"),
                blurb = () => Loc.T("고리 성운과 같은 태양의 최후를, 훨씬 가까이서 정면으로 들여다본 모습. 안쪽 푸른 고리는 산소, 바깥 붉은 테두리는 수소와 질소입니다. 고리 안쪽 가장자리를 향한 수천 개의 ‘혜성 매듭’ — 각각 태양계만 한 가스 덩어리 — 이 이 성운의 특징입니다.",
                                    "The same Sun's-end story as the Ring Nebula, seen much closer and face-on. The inner blue ring is oxygen, the outer red rim hydrogen and nitrogen. Thousands of 'cometary knots' — each a gas clump the size of our solar system — point in toward the ring's inner edge.",
                                    "環状星雲と同じ太陽の最期を、はるかに近くから正面で覗き込んだ姿。内側の青い環は酸素、外側の赤い縁は水素と窒素です。環の内縁を向いた無数の「彗星状ノット」— それぞれ太陽系ほどの大きさ — がこの星雲の特徴です。",
                                    "与环状星云相同的太阳终局，只是离得更近、正面望入。内侧蓝环是氧，外侧红边是氢和氮。数千个朝向环内缘的“彗星状结节”——每个都有太阳系那么大——是这个星云的标志。") },

            // 6) Crab Nebula — supernova remnant
            new Hero {
                id = "Crab", form = Form.Nebula, position = new Vector3(-22, 18, 34), framing = 3.4f,
                type = 3, color1 = new Color(2.3f, 0.95f, 0.35f), color2 = new Color(0.4f, 1.6f, 1.0f),
                brightness = 0.42f, radius = 7f, density = 1.2f, noiseScale = 0.5f, filament = 1.5f,
                threshold = 0.35f, dust = 0.5f, stretch = new Vector3(1.35f, 0.82f, 1.0f),
                starBright = new Color(0.7f, 0.85f, 1.3f), starField = new Color(1.1f, 1.0f, 0.95f),
                brightN = 1, fieldN = 40, coreFrac = 0.06f,
                bgDensity = 0.85f, bgGalaxies = 0f, bgTint = new Color(0.96f, 0.93f, 0.88f), bgBand = 0f,
                name = () => Loc.T("게 성운 (M1)", "The Crab Nebula (M1)", "かに星雲 (M1)", "蟹状星云 (M1)"),
                facts = () => Loc.T("초신성 잔해 · 6,500광년 · 1054년 폭발 기록 · 중심에 펄서",
                                    "Supernova remnant · 6,500 ly · seen exploding in 1054 · a pulsar within",
                                    "超新星残骸 · 6,500光年 · 1054年に爆発を記録 · 中心にパルサー",
                                    "超新星遗迹 · 6,500光年 · 1054年记录爆发 · 中心有脉冲星"),
                blurb = () => Loc.T("무거운 별의 격렬한 죽음. 1054년 여러 문명이 대낮에도 보였다고 기록한 초신성의 잔해입니다. 오렌지빛 필라멘트는 흩날린 별의 잔해이고, 중심의 푸른 광채는 초당 30번 도는 중성자별 — 펄서 — 이 뿜는 바람입니다.",
                                    "The violent death of a massive star. This is the wreckage of a supernova that many civilizations recorded in 1054 as visible in broad daylight. The orange filaments are the scattered remains of the star; the blue interior glow is the wind of a neutron star — a pulsar — spinning 30 times a second.",
                                    "重い星の激しい死。1054年、多くの文明が真昼でも見えたと記録した超新星の残骸です。オレンジ色のフィラメントは飛び散った星の残骸、中心の青い光は毎秒30回転する中性子星 — パルサー — が放つ風です。",
                                    "大质量恒星的剧烈死亡。这是1054年多个文明记录到白昼可见的超新星遗骸。橙色的丝状结构是恒星飞散的残骸；中心的蓝色辉光是每秒自转30次的中子星——脉冲星——所吹出的风。") },

            // 6) Omega Centauri — globular cluster
            new Hero {
                id = "OmegaCen", form = Form.Cluster, position = new Vector3(56, 12, 18), framing = 3.4f,
                clusterKind = ClusterField.Kind.Globular, clusterStars = 6000, clusterRadius = 8f,
                bgDensity = 0.35f, bgGalaxies = 0.55f, bgTint = new Color(0.95f, 0.96f, 1.0f), bgBand = 0f,
                name = () => Loc.T("오메가 센타우리", "Omega Centauri", "オメガ星団", "半人马座ω星团"),
                facts = () => Loc.T("구상성단 · 17,000광년 · 120억 살 · 별 약 1,000만 개",
                                    "Globular cluster · 17,000 ly · 12 Gyr old · ~10 million stars",
                                    "球状星団 · 17,000光年 · 120億歳 · 約1,000万個の星",
                                    "球状星团 · 17,000光年 · 120亿岁 · 约1,000万颗恒星"),
                blurb = () => Loc.T("은하에서 가장 크고 밝은 구상성단. 은하가 태어나던 무렵에 함께 태어난 늙은 별 수백만 개가 중력으로 뭉쳐 공 모양을 이룹니다. 태양보다 훨씬 오래 살아남은 이 별들은, 성운→별→죽음의 순환에서 ‘살아남은 자들’의 이야기입니다.",
                                    "The biggest, brightest globular cluster in our galaxy. Millions of old stars, born when the galaxy itself was young, bound by gravity into a sphere. Long outliving the Sun, these are the survivors in the nebula → star → death cycle.",
                                    "銀河系で最も大きく明るい球状星団。銀河が生まれた頃に一緒に生まれた老いた星が数百万個、重力で球状に集まっています。太陽よりはるかに長く生き延びたこれらの星は、星雲→星→死の循環における「生き残り」の物語です。",
                                    "银河系中最大、最亮的球状星团。数百万颗古老的恒星，诞生于银河系年轻之时，被引力束缚成一个球体。这些远比太阳长寿的恒星，是星云→恒星→死亡循环中的“幸存者”。") },
        };

        static readonly int TypeId = Shader.PropertyToID("_NebulaType");

        /// <summary>Push a hero's nebula recipe into a material.</summary>
        public static void ApplyMaterial(Material m, Hero h)
        {
            m.SetFloat("_NebulaType", h.type);
            m.SetFloat("_Brightness", h.brightness);
            m.SetColor("_Color1", h.color1);
            m.SetColor("_Color2", h.color2);
            m.SetFloat("_Radius", h.radius);
            m.SetFloat("_Density", h.density);
            m.SetFloat("_NoiseScale", h.noiseScale);
            m.SetFloat("_Filament", h.filament);
            m.SetFloat("_Threshold", h.threshold);
            m.SetFloat("_DustStrength", h.dust);
            m.SetFloat("_ShellRadius", h.shellRadius <= 0f ? 0.6f : h.shellRadius);
            m.SetFloat("_ShellThickness", h.shellThickness <= 0f ? 0.1f : h.shellThickness);
            // 72 steps: the galaxy volume runs at 64, and only one nebula renders
            // at a time now, so this is comfortably enough for a clean march.
            m.SetFloat("_Steps", 72f);
        }
    }
}
