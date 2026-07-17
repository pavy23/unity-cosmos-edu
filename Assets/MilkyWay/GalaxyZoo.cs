using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The galaxy zoo (F8): the Hubble sequence as an exhibit hall. Four
    /// VOLUMETRIC specimens — each a clone of the showcase galaxy with its
    /// materials re-authored into a different species (the Andromeda
    /// collision's clone + instanced-materials technique) — plus an impostor
    /// swarm of dwarf galaxies as supporting cast:
    ///
    ///   0  barred spiral — our own species, the reference point
    ///   1  classic spiral — no bar, looser bluer arms (pitch/width/young)
    ///   2  elliptical — arms off, dust gone, stars phase-mixed (_Scramble),
    ///      the whole clone rescaled into a spheroid: the merger's end state
    ///   3  irregular — small, tidally warped, starbursting pink; ringed by
    ///      impostor dwarfs (the most NUMEROUS kind of galaxy)
    ///   4  finale — the whole row at once; every deep-field point is one
    ///      of these
    ///
    /// Tour controls (N/B/arrows, K, F8) via MilkyWayControls.
    /// </summary>
    public class GalaxyZoo : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Seconds the camera takes to glide between specimens.")]
        public float glideDuration = 3.2f;

        public bool Running { get; private set; }

        // The exhibit hall sits far from the origin so the real galaxy is a
        // distant backdrop, not a fifth specimen photobombing the frames.
        static readonly Vector3 HallCentre = new Vector3(240f, 30f, 0f);
        const float RowSpacing = 56f;
        const float SpecimenScale = 0.55f;

        Transform zooRoot;
        readonly List<Material> mats = new();
        readonly List<Transform> specimens = new();
        Mesh dwarfMesh;

        int step;
        RectTransform card;
        Text cardTitle, cardBody, cardFooter;

        Vector3 fromPos, fromLook, toPos, toLook;
        float glideT;
        Vector3 savedPos;
        Quaternion savedRot;

        // ---- narration -------------------------------------------------------

        public static readonly string[] NarrationLines =
        {
            "은하 동물원에 오신 것을 환영합니다. 첫 번째 우리는 낯익습니다 — 막대나선은하, 우리 은하와 같은 종입니다. 중심을 가로지르는 막대는 가스를 안쪽으로 흘려보내는 통로입니다. 관측되는 큰 은하 셋 중 둘이 이런 원반은하입니다.",
            "막대가 없는 고전 나선은하입니다. 팔이 더 느슨하게 감기고, 더 푸릅니다 — 푸른 빛은 갓 태어난 무거운 별들입니다. 원반이 푸를수록 지금 활발히 별을 만들고 있다는 뜻입니다. 이웃 안드로메다가 이 종에 가깝습니다.",
            "타원은하 — 팔도, 원반도, 먼지 띠도 없습니다. 늙고 붉은 별들이 저마다의 방향으로 벌떼처럼 돕니다. 가스를 다 써 버려 새 별이 태어나지 않는, 은하의 은퇴한 모습입니다. 큰 타원은하는 대부분 병합의 산물입니다 — 안드로메다 충돌의 결말을 기억하시나요.",
            "불규칙은하 — 작고, 정해진 형태가 없습니다. 큰 은하 곁을 지나다 조석력에 뜯긴 경우가 많고, 그 충격으로 오히려 별이 왕성하게 태어나기도 합니다. 우리 은하의 위성 마젤란은하가 이런 모습입니다. 주위의 작은 점들은 왜소은하 — 우주에서 수가 가장 많은 은하입니다.",
            "네 종을 한눈에 봅니다. 우주 줌아웃에서 본 2조 개의 점 하나하나가 이들 가운데 하나입니다. 형태는 은하의 이력서입니다 — 나선은 현역, 타원은 은퇴, 불규칙은 상처. 그리고 50억 년 뒤, 우리 은하와 안드로메다는 함께 하나의 타원은하가 됩니다.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "Welcome to the galaxy zoo. The first enclosure looks familiar — a barred spiral, the same species as our own galaxy. The bar across its centre is a channel that funnels gas inward. Two out of three large galaxies we observe are disk galaxies like this.",
            "A classic spiral, without the bar. Its arms wind more loosely and shine bluer — blue is the light of massive newborn stars. The bluer the disk, the busier its star formation. Our neighbour Andromeda is close to this species.",
            "An elliptical — no arms, no disk, no dust lanes. Old red stars swarm on every private orbit at once. Its gas is spent, so no new stars are born: a galaxy in retirement. Most giant ellipticals are the products of mergers — remember how the Andromeda collision ended.",
            "An irregular — small, with no fixed shape. Many were torn by tides while passing a larger galaxy, and the shock often ignites furious star birth. Our own satellites, the Magellanic Clouds, look like this. The specks around it are dwarf galaxies — the most numerous kind in the universe.",
            "All four species at a glance. Every one of the two trillion points from the cosmic zoom-out is one of these. Shape is a galaxy's résumé — spirals are working, ellipticals are retired, irregulars are scarred. And in five billion years, our galaxy and Andromeda will become one elliptical, together.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "銀河動物園へようこそ。最初の檻は見覚えがあります — 棒渦巻銀河、私たちの銀河と同じ種です。中心を横切る棒は、ガスを内側へ流し込む水路です。観測される大きな銀河の3つに2つが、このような円盤銀河です。",
            "棒のない、古典的な渦巻銀河です。腕はよりゆるく巻き、より青く輝きます — 青い光は生まれたての重い星たちです。円盤が青いほど、いま盛んに星を作っているという印です。お隣のアンドロメダがこの種に近いのです。",
            "楕円銀河 — 腕も、円盤も、塵の帯もありません。年老いた赤い星々が、めいめいの軌道で蜂の群れのように回っています。ガスを使い果たし、新しい星は生まれません。銀河の隠退した姿です。大きな楕円銀河の多くは合体の産物 — アンドロメダ衝突の結末を覚えていますか。",
            "不規則銀河 — 小さく、決まった形がありません。大きな銀河のそばを通って潮汐力に引き裂かれたものが多く、その衝撃でかえって星が盛んに生まれることもあります。私たちの銀河の衛星、マゼラン雲がこんな姿です。まわりの小さな点は矮小銀河 — 宇宙でもっとも数の多い銀河です。",
            "4つの種をひと目で見わたします。宇宙ズームアウトで見た2兆の点のひとつひとつが、このどれかです。形は銀河の履歴書 — 渦巻は現役、楕円は隠退、不規則は傷跡。そして50億年後、私たちの銀河とアンドロメダは、ともにひとつの楕円銀河になります。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "欢迎来到星系动物园。第一个展区似曾相识——棒旋星系，和我们银河系同一物种。横贯中心的棒，是把气体输送向内的通道。我们观测到的大星系中，三个里有两个是这样的盘星系。",
            "没有棒的经典旋涡星系。旋臂缠绕得更松，也更蓝——蓝光来自刚诞生的大质量恒星。星系盘越蓝，说明造星越旺盛。邻居仙女座就接近这个物种。",
            "椭圆星系——没有旋臂、没有星系盘、没有尘埃带。年老的红色恒星像蜂群一样各自绕行。气体耗尽，不再有新星诞生：这是星系的退休状态。巨型椭圆星系大多是并合的产物——还记得仙女座相撞的结局吗。",
            "不规则星系——小，而且没有固定形状。许多是在经过大星系时被潮汐力撕扯的，而那份冲击反而常常点燃剧烈的造星。我们银河系的卫星麦哲伦云就是这副模样。周围的小点是矮星系——宇宙中数量最多的星系。",
            "四个物种尽收眼底。宇宙缩放中看到的两万亿个光点，每一个都是它们中的一员。形状是星系的履历——旋涡在职，椭圆退休，不规则带伤。而五十亿年后，银河系与仙女座将一起变成一个椭圆星系。",
        };

        struct Stop
        {
            public int specimen;          // -1 = finale (frame the whole row)
            public Vector3 offset;        // camera offset from the framed point
            public string title, titleEn, titleJa, titleZh;
        }

        static readonly Stop[] Stops =
        {
            new Stop { specimen = 0, offset = new Vector3(-22f, 17f, -13f),
                title = "1. 막대나선은하 — 우리의 종", titleEn = "1. Barred Spiral — Our Species",
                titleJa = "1. 棒渦巻銀河 — 私たちの種", titleZh = "1. 棒旋星系——我们的物种" },
            new Stop { specimen = 1, offset = new Vector3(-22f, 17f, -13f),
                title = "2. 나선은하", titleEn = "2. Spiral",
                titleJa = "2. 渦巻銀河", titleZh = "2. 旋涡星系" },
            new Stop { specimen = 2, offset = new Vector3(-14f, 8f, -9f),
                title = "3. 타원은하 — 은하의 은퇴", titleEn = "3. Elliptical — Retirement",
                titleJa = "3. 楕円銀河 — 銀河の隠退", titleZh = "3. 椭圆星系——星系的退休" },
            new Stop { specimen = 3, offset = new Vector3(-10f, 5f, -6f),
                title = "4. 불규칙은하와 왜소은하", titleEn = "4. Irregulars and Dwarfs",
                titleJa = "4. 不規則銀河と矮小銀河", titleZh = "4. 不规则星系与矮星系" },
            new Stop { specimen = -1, offset = new Vector3(-150f, 52f, 0f),
                title = "은하 동물원", titleEn = "The Galaxy Zoo",
                titleJa = "銀河動物園", titleZh = "星系动物园" },
        };

        public void StartZoo()
        {
            if (Running || !Application.isPlaying || controller == null) return;
            Running = true;
            step = 0;
            savedPos = transform.position;
            savedRot = transform.rotation;
            if (orbit != null) orbit.enabled = false;
            BuildHall();
            ApplyStep();
        }

        public void StopZoo()
        {
            if (!Running) return;
            Running = false;
            NarrationManager.Instance.Stop();
            if (card != null) card.gameObject.SetActive(false);
            if (zooRoot != null) Destroy(zooRoot.gameObject);
            foreach (var m in mats) if (m != null) Destroy(m);
            mats.Clear();
            specimens.Clear();
            if (dwarfMesh != null) Destroy(dwarfMesh);
            dwarfMesh = null; zooRoot = null;
            transform.position = savedPos;
            transform.rotation = savedRot;
            if (orbit != null) orbit.enabled = true;
        }

        public void Next() { if (Running && step < Stops.Length - 1) { step++; ApplyStep(); } }
        public void Prev() { if (Running && step > 0) { step--; ApplyStep(); } }

        public void OnLanguageChanged()
        {
            if (Running) ApplyStep();
        }

        // ---- the exhibit hall ------------------------------------------------

        void BuildHall()
        {
            zooRoot = new GameObject("Galaxy Zoo").transform;

            // Species recipes: (volume params, star params, local scale).
            Spawn(0, v =>
            {
                // Barred spiral: the showcase's own look — the reference cage.
            }, s => { });

            Spawn(1, v =>
            {
                // Classic spiral: looser, wider, bluer arms; modest bulge.
                v.SetFloat("_PitchTan", 0.32f);
                v.SetFloat("_ArmWidth", 1.35f);
                v.SetFloat("_YoungStrength", 1.9f);
                v.SetFloat("_HiiStrength", 1.5f);
                v.SetFloat("_BulgeBoost", 0.75f);
                v.SetFloat("_DustStrength", 2.1f);
            }, s => { });

            Spawn(2, v =>
            {
                // Elliptical: arms and dust gone, warm bulge light everywhere.
                v.SetFloat("_ArmStrength", 0f);
                v.SetFloat("_DustStrength", 0.1f);
                v.SetFloat("_HiiStrength", 0f);
                v.SetFloat("_YoungStrength", 0f);
                v.SetFloat("_BulgeBoost", 2.6f);
                v.SetFloat("_Clumpiness", 0.15f);
            }, s =>
            {
                s.SetFloat("_Scramble", 1f); // phase-mixed swarm, no disk
            }, new Vector3(0.62f, 2.6f, 0.62f)); // disk ellipsoid → near-spheroid

            Spawn(3, v =>
            {
                // Irregular: small, torn, starbursting.
                v.SetFloat("_ArmStrength", 0.35f);
                v.SetFloat("_ArmWidth", 2.0f);
                v.SetFloat("_BulgeBoost", 0.25f);
                v.SetFloat("_DustStrength", 3.4f);
                v.SetFloat("_HiiStrength", 2.4f);
                v.SetFloat("_YoungStrength", 1.7f);
                v.SetVector("_TidalDir", new Vector4(0.9f, 0.35f, 0.25f, 0f));
                v.SetFloat("_TidalAmount", 0.95f);
            }, s =>
            {
                s.SetVector("_TidalDir", new Vector4(0.9f, 0.35f, 0.25f, 0f));
                s.SetFloat("_TidalAmount", 0.95f);
                s.SetFloat("_Scramble", 0.25f);
            }, Vector3.one * 0.34f);

            BuildDwarfSwarm();
        }

        Vector3 SpecimenPos(int i) =>
            HallCentre + new Vector3(0f, 0f, (i - 1.5f) * RowSpacing);

        void Spawn(int index, System.Action<Material> paintVolume, System.Action<Material> paintStars,
                   Vector3? localScale = null)
        {
            var clone = Instantiate(controller.gameObject);
            clone.name = "Specimen " + index;
            clone.transform.SetParent(zooRoot, false);
            clone.transform.position = SpecimenPos(index);
            // Each specimen tilted its own way so the row never reads stamped.
            clone.transform.rotation = Quaternion.Euler(14f + 9f * index, 55f * index + 20f, 6f * index - 8f);
            clone.transform.localScale = Vector3.Scale(Vector3.one * SpecimenScale,
                                                       localScale ?? Vector3.one);
            // The clone's controller would keep writing the SHARED materials —
            // the collision experience's hard-won lesson.
            Destroy(clone.GetComponent<MilkyWayController>());

            Material vol = null, stars = null;
            foreach (var mr in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.sharedMaterial == controller.volumeMaterial)
                    mr.sharedMaterial = vol = new Material(controller.volumeMaterial);
                else if (mr.sharedMaterial == controller.starMaterial)
                    mr.sharedMaterial = stars = new Material(controller.starMaterial);
            }
            var sf = clone.GetComponentInChildren<GalaxyStarField>();
            if (sf != null && stars == null) stars = new Material(controller.starMaterial);
            if (sf != null)
            {
                sf.material = stars;
                foreach (var mr in clone.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.name == "Galaxy Stars Mesh") mr.sharedMaterial = stars;
            }

            if (vol != null) { paintVolume(vol); mats.Add(vol); }
            if (stars != null) { paintStars(stars); mats.Add(stars); }
            specimens.Add(clone.transform);
        }

        /// <summary>The supporting cast: impostor dwarf sprites scattered
        /// through the hall, densest around the irregular's cage — one mesh,
        /// one draw, the cosmic web's technique in miniature.</summary>
        void BuildDwarfSwarm()
        {
            var rng = new System.Random(19);
            var list = new List<(Vector3 p, float size, Color tint)>();
            Vector3 irr = SpecimenPos(3);
            for (int i = 0; i < 26; i++)
            {
                // cluster around the irregular, thinning outward along the row
                Vector3 centre = i < 14 ? irr : HallCentre;
                float spread = i < 14 ? 16f : 90f;
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f,
                                      (float)rng.NextDouble() * 2f - 1f,
                                      (float)rng.NextDouble() * 2f - 1f).normalized;
                Vector3 p = centre + dir * (5f + spread * (float)rng.NextDouble());
                float size = 0.8f + 2.4f * (float)rng.NextDouble();
                var tint = Color.Lerp(new Color(0.62f, 0.82f, 1f), new Color(1f, 0.82f, 0.62f),
                                      (float)rng.NextDouble()) * 0.8f;
                list.Add((p, size, tint));
            }

            int n = list.Count;
            var verts = new Vector3[n * 4];
            var cols = new Color[n * 4];
            var corners = new List<Vector2>(n * 4);
            var sizeRand = new List<Vector2>(n * 4);
            var pixelFloor = new List<Vector2>(n * 4);
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float rand = (float)((i * 2654435761u % 1000000u) / 1000000.0);
                for (int k = 0; k < 4; k++)
                {
                    verts[i * 4 + k] = list[i].p;
                    cols[i * 4 + k] = list[i].tint;
                    corners.Add(new Vector2((k & 1) * 2 - 1, (k >> 1) * 2 - 1));
                    sizeRand.Add(new Vector2(list[i].size, rand));
                    pixelFloor.Add(new Vector2(2.5f, 0f)); // shader's per-galaxy floor
                }
                int t = i * 6, b = i * 4;
                tris[t] = b; tris[t + 1] = b + 2; tris[t + 2] = b + 1;
                tris[t + 3] = b + 1; tris[t + 4] = b + 2; tris[t + 5] = b + 3;
            }
            dwarfMesh = new Mesh();
            dwarfMesh.vertices = verts;
            dwarfMesh.colors = cols;
            dwarfMesh.SetUVs(0, corners);
            dwarfMesh.SetUVs(1, sizeRand);
            dwarfMesh.SetUVs(2, pixelFloor);
            dwarfMesh.triangles = tris;
            dwarfMesh.bounds = new Bounds(HallCentre, Vector3.one * 260f);

            var go = new GameObject("Dwarf Swarm");
            go.transform.SetParent(zooRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = dwarfMesh;
            var mr2 = go.AddComponent<MeshRenderer>();
            var m = new Material(Shader.Find("MilkyWay/GalaxyImpostor"));
            m.SetFloat("_Brightness", 1.6f);
            mr2.sharedMaterial = m;
            mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mats.Add(m);
        }

        // ---- the tour loop ---------------------------------------------------

        void ApplyStep()
        {
            var s = Stops[step];
            Vector3 focus = s.specimen >= 0 ? SpecimenPos(s.specimen) : HallCentre;

            fromPos = transform.position;
            fromLook = transform.position + transform.forward * 10f;
            toPos = focus + s.offset;
            toLook = focus;
            glideT = 0f;

            NarrationManager.Instance.Play("mw_zoo_" + step);

            EnsureCard();
            card.gameObject.SetActive(true);
            cardTitle.text = Loc.T(s.title, s.titleEn, s.titleJa, s.titleZh);
            cardBody.text = Loc.T(NarrationLines[step], NarrationLinesEn[step],
                                  NarrationLinesJa[step], NarrationLinesZh[step]);
            cardFooter.text = Loc.T("N 다음    B 이전    F8 종료",
                                    "N Next    B Prev    F8 End",
                                    "N 次へ    B 前へ    F8 終了",
                                    "N 下一步    B 上一步    F8 结束")
                            + "                                  " + (step + 1) + " / " + Stops.Length;
        }

        void Update()
        {
            if (!Running) return;

            if (glideT < 1f)
            {
                glideT = Mathf.Min(1f, glideT + Time.deltaTime / Mathf.Max(glideDuration, 0.1f));
                float u = Mathf.SmoothStep(0f, 1f, glideT);
                transform.position = Vector3.Lerp(fromPos, toPos, u);
                transform.LookAt(Vector3.Lerp(fromLook, toLook, u));
            }
            else
            {
                // Slow drift around the framed cage — parallax keeps the
                // volumetrics reading as volumes.
                transform.RotateAround(toLook, Vector3.up, 0.6f * Time.deltaTime);
            }
        }

        // ---- card UI (the shared factory) ------------------------------------

        void EnsureCard()
        {
            if (card != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());

            card = BlackHoleUI.MakePanel(canvas.transform, "Zoo Card",
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
            if (Running) StopZoo();
        }
    }
}
