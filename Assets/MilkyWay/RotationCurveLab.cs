using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The rotation-curve lab (F7): the evidence for dark matter, as an
    /// experiment the visitor runs. Twelve tracer stars orbit the galaxy at
    /// speeds drawn from a rotation curve v(r); a live graph plots v against
    /// r. The D key swaps the universe between two curves:
    ///
    ///   PREDICTED — visible mass only (bulge + exponential disk): Keplerian
    ///   beyond the disk, v falls with distance. The outer tracers crawl.
    ///   OBSERVED  — what Doppler measurements actually show: v stays flat.
    ///   The outer tracers keep pace, which the visible mass cannot explain.
    ///
    /// The gap between the curves IS the dark-matter halo (shown as a faint
    /// sheath, deliberately schematic). Four narrated beats set the puzzle
    /// up, then the lab stays open for toggling until F7/Esc.
    /// </summary>
    public class RotationCurveLab : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Sun-radius tracer's angular speed at the observed 230 km/s, deg/s. " +
                 "Pure presentation: real stars take two hundred million years per lap.")]
        public float sunTracerDegPerSec = 4.5f;

        public bool IsPlaying { get; private set; }

        // ---- rotation curves (r in kpc; v in km/s) --------------------------
        // Observed: rises quickly, then flat out to the edge (Vera Rubin's
        // curves). Predicted: circular speed from the VISIBLE enclosed mass —
        // a compact bulge plus an exponential disk (scale 2.8, matching the
        // volume shader) — which peaks early and falls roughly Keplerian.
        const float VFlat = 230f;

        static float VObserved(float r) => VFlat * (1f - Mathf.Exp(-r / 1.2f));

        static float VisibleMass(float r)
        {
            float bulge = 0.35f * (1f - Mathf.Exp(-(r / 1.5f) * (r / 1.5f)));
            float x = r / 2.8f;
            float disk = 0.65f * (1f - (1f + x) * Mathf.Exp(-x));
            return bulge + disk;
        }

        static float VPredicted(float r) =>
            r < 0.05f ? 0f : 530f * Mathf.Sqrt(VisibleMass(r) / r);

        // ---- state ----------------------------------------------------------

        bool darkMatterOn = true;   // true → observed curve drives the tracers
        float modeBlend = 1f;       // 1 = observed, 0 = predicted (smoothed)
        bool haloRevealed;          // beat 3 unveils the halo; D respects it after
        bool benchReady;            // the intro glide owns the camera until true

        class Tracer
        {
            public Transform t;
            public TrailRenderer trail;
            public Material mat;
            public float r, angle;
        }

        readonly List<Tracer> tracers = new();
        Transform tracerRoot;
        GameObject halo;
        Material haloMat;
        Texture2D haloTex, graphTex;

        RectTransform graphPanel, captionPanel;
        RawImage graphImage;
        Text graphMode, caption;
        Button stopButton;

        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        float driftDeg;

        static readonly Color ObservedGold = new Color(1.00f, 0.78f, 0.35f);
        static readonly Color PredictedBlue = new Color(0.45f, 0.70f, 1.00f);

        public void Begin()
        {
            if (!Application.isPlaying || IsPlaying) return;
            routine = StartCoroutine(Run());
        }

        public void Abort()
        {
            if (!IsPlaying) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            Finish();
        }

        void Finish()
        {
            foreach (var tr in tracers)
            {
                if (tr.mat != null) Destroy(tr.mat);
                if (tr.t != null) Destroy(tr.t.gameObject);
            }
            tracers.Clear();
            if (tracerRoot != null) Destroy(tracerRoot.gameObject);
            if (halo != null) Destroy(halo);
            if (haloMat != null) Destroy(haloMat);
            if (haloTex != null) Destroy(haloTex);
            if (graphTex != null) Destroy(graphTex);
            halo = null; haloMat = null; haloTex = null; graphTex = null; tracerRoot = null;

            if (graphPanel != null) graphPanel.gameObject.SetActive(false);
            if (captionPanel != null) captionPanel.gameObject.SetActive(false);
            ShowStop(false);

            transform.position = savedPos;
            transform.rotation = savedRot;
            if (orbit != null) orbit.enabled = true;
            IsPlaying = false;
        }

        // ---- narration -------------------------------------------------------

        public static readonly string[] NarrationLines =
        {
            "은하를 거의 바로 위에서 봅니다. 추적자 별 열두 개를 띄웠습니다 — 안쪽부터 바깥까지 각자의 궤도로 은하를 돕니다. 오른쪽 그래프는 중심에서의 거리에 따른 공전 속도입니다.",
            "만약 은하의 질량이 눈에 보이는 별과 가스가 전부라면, 태양계처럼 바깥으로 갈수록 느려져야 합니다. 지금 추적자들은 그 예측 — 파란 곡선 — 대로 돌고 있습니다. 바깥 별들이 눈에 띄게 굼뜹니다.",
            "그런데 실제 은하를 관측하면, 이렇습니다. 바깥 별들이 느려지지 않습니다 — 금색 곡선처럼 속도가 끝까지 평평합니다. 보이는 질량만으로는 이 속도를 붙잡아 둘 수 없습니다. 이대로라면 은하는 흩어져야 합니다.",
            "결론은 하나입니다. 보이지 않는 질량이 은하를 통째로 감싸고 있습니다 — 암흑물질입니다. 은하 질량의 약 85퍼센트를 차지하지만, 빛을 내지도 가리지도 않아 아직 정체를 모릅니다. D 키로 두 우주를 오가며 비교해 보세요.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "We are looking almost straight down at the galaxy. Twelve tracer stars are in orbit, from the inner disk to the rim. The graph on the right plots orbital speed against distance from the centre.",
            "If the galaxy's mass were only the stars and gas we can see, speed should fall with distance — just like the solar system. The tracers are now following that prediction, the blue curve. Watch the outer ones crawl.",
            "But when we measure a real galaxy, this happens: the outer stars do not slow down — the speed stays flat, the gold curve, all the way out. Visible mass cannot hold onto stars moving this fast. The galaxy should fly apart.",
            "Only one conclusion fits: an invisible mass wraps the entire galaxy — dark matter. It makes up about eighty-five percent of the galaxy's mass, yet emits no light and blocks none, and we still do not know what it is. Press D to switch between the two universes.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "銀河をほぼ真上から見ています。トレーサーの星を12個浮かべました — 内側から外縁まで、それぞれの軌道で銀河を回ります。右のグラフは、中心からの距離に対する公転速度です。",
            "もし銀河の質量が目に見える星とガスだけなら、太陽系と同じように、外へ行くほど遅くなるはずです。いまトレーサーはその予測 — 青い曲線 — のとおりに回っています。外側の星が目に見えて鈍いでしょう。",
            "ところが実際の銀河を観測すると、こうなります。外側の星は遅くなりません — 金色の曲線のように、速度は最後まで平らです。見える質量だけでは、この速さの星をつなぎ留められません。このままでは銀河はばらばらになるはずです。",
            "結論はひとつ。見えない質量が銀河をまるごと包んでいます — 暗黒物質です。銀河の質量の約85パーセントを占めながら、光を出しも遮りもせず、正体はまだわかっていません。Dキーでふたつの宇宙を行き来してみてください。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "我们几乎从正上方俯视银河。十二颗示踪星已经就位——从内盘到边缘，各自沿轨道绕行。右边的图表画出公转速度随中心距离的变化。",
            "如果星系的质量只有看得见的恒星和气体，速度就该像太阳系那样随距离下降。示踪星现在正按那个预测——蓝色曲线——运行。看，外圈的星明显变慢了。",
            "然而实际观测星系时，是这样的：外圈的恒星并没有变慢——速度像金色曲线一样一路平坦。可见的质量根本拉不住转得这么快的恒星。照这样，星系早该散架了。",
            "只有一个结论：一团看不见的质量包裹着整个星系——暗物质。它约占星系质量的百分之八十五，却不发光也不挡光，我们至今不知道它是什么。按 D 键，在两个宇宙之间切换比较吧。",
        };

        IEnumerator Run()
        {
            IsPlaying = true;
            savedPos = transform.position;
            savedRot = transform.rotation;
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);
            darkMatterOn = true;
            modeBlend = 1f;
            driftDeg = 0f;
            haloRevealed = false;
            benchReady = false;

            BuildTracers();
            BuildHalo();
            EnsureGraph();
            RedrawGraph();
            graphPanel.gameObject.SetActive(true);

            // Glide to the lab bench: a high oblique view, whole disk in frame.
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            Vector3 benchPos = new Vector3(0f, 38f, -15f);
            Quaternion benchRot = Quaternion.LookRotation(-benchPos.normalized, Vector3.forward);
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / 2.6f);
                transform.position = Vector3.Lerp(fromPos, benchPos, u);
                transform.rotation = Quaternion.Slerp(fromRot, benchRot, u);
                yield return null;
            }
            transform.position = benchPos;
            transform.LookAt(Vector3.zero, Vector3.forward);
            benchReady = true;

            // Beat 0: the setup (observed universe, no comment yet).
            yield return Beat(0);
            // Beat 1: the visible-mass prediction — outer tracers crawl.
            SetDarkMatter(false, narrate: false);
            yield return Beat(1);
            // Beat 2: what we actually observe — flat, fast, impossible.
            SetDarkMatter(true, narrate: false);
            yield return Beat(2);
            // Beat 3: the resolution — the halo fades in; then hands-free.
            haloRevealed = true;
            yield return Beat(3);

            // Interactive hold: D toggles until F7/Esc ends the lab.
            while (true) yield return null;
        }

        IEnumerator Beat(int i)
        {
            float len = NarrationManager.Instance.Play("mw_rot_" + i);
            Caption(Loc.T(NarrationLines[i], NarrationLinesEn[i], NarrationLinesJa[i], NarrationLinesZh[i]));
            for (float t = 0f, dur = Mathf.Max(6f, len + 1f); t < dur; t += Time.deltaTime)
                yield return null;
        }

        public void ToggleDarkMatter() => SetDarkMatter(!darkMatterOn, narrate: false);

        void SetDarkMatter(bool on, bool narrate)
        {
            darkMatterOn = on;
            RedrawGraph();
            UpdateGraphModeText();
        }

        void Update()
        {
            if (!IsPlaying) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { Abort(); return; }
                if (kb.dKey.wasPressedThisFrame) ToggleDarkMatter();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape)) { Abort(); return; }
            if (Input.GetKeyDown(KeyCode.D)) ToggleDarkMatter();
#endif

            // Universe swap is a smooth crossfade, not a pop.
            modeBlend = Mathf.MoveTowards(modeBlend, darkMatterOn ? 1f : 0f, Time.deltaTime / 1.2f);

            // Tracers: ω = v/r, with the exhibit's time exaggeration.
            float omegaScale = (sunTracerDegPerSec * Mathf.Deg2Rad) * 8.2f / VFlat;
            foreach (var tr in tracers)
            {
                float v = Mathf.Lerp(VPredicted(tr.r), VObserved(tr.r), modeBlend);
                tr.angle += omegaScale * (v / tr.r) * Time.deltaTime;
                tr.t.localPosition = new Vector3(Mathf.Cos(tr.angle), 0.02f, Mathf.Sin(tr.angle)) * tr.r
                                   + Vector3.up * 0.25f;
                // Colour tells speed at a glance: gold = observed pace,
                // fading toward blue as the star falls behind it.
                float k = Mathf.InverseLerp(0.45f, 1f, v / VObserved(tr.r));
                var c = Color.Lerp(PredictedBlue, ObservedGold, k);
                tr.mat.SetColor("_BaseColor", c * 2.2f);
                tr.trail.startColor = new Color(c.r, c.g, c.b, 0.85f);
                tr.trail.endColor = new Color(c.r, c.g, c.b, 0f);
            }

            // The halo is the dark-matter universe's furniture, unveiled at
            // beat 3 and honouring the D toggle afterwards.
            if (haloMat != null)
            {
                var hc = haloMat.color;
                hc.a = Mathf.MoveTowards(hc.a, (darkMatterOn && haloRevealed) ? 0.055f : 0f, Time.deltaTime * 0.05f);
                haloMat.color = hc;
                if (halo != null) halo.transform.rotation =
                    Quaternion.LookRotation(halo.transform.position - transform.position);
            }

            // A whisper of camera drift so the bench never feels frozen —
            // but only once the intro glide has handed the camera over.
            if (benchReady)
            {
                driftDeg += 0.25f * Time.deltaTime;
                var bench = new Vector3(0f, 38f, -15f);
                transform.position = Quaternion.AngleAxis(driftDeg, Vector3.up) * bench;
                transform.LookAt(Vector3.zero, Vector3.forward);
            }
        }

        // ---- scene props -----------------------------------------------------

        void BuildTracers()
        {
            tracerRoot = new GameObject("Rotation Tracers").transform;
            var lineShader = Shader.Find("Sprites/Default");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var rng = new System.Random(7);
            for (int i = 0; i < 12; i++)
            {
                float r = 2f + i * (14f / 11f); // 2 .. 16 kpc
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Tracer " + i;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(tracerRoot, false);
                go.transform.localScale = Vector3.one * 0.5f;
                var mat = new Material(unlit);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var trail = go.AddComponent<TrailRenderer>();
                trail.time = 2.6f;
                trail.startWidth = 0.22f;
                trail.endWidth = 0.02f;
                trail.minVertexDistance = 0.08f;
                trail.material = new Material(lineShader);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                tracers.Add(new Tracer
                {
                    t = go.transform,
                    trail = trail,
                    mat = mat,
                    r = r,
                    angle = (float)(rng.NextDouble() * Mathf.PI * 2.0),
                });
            }
        }

        void BuildHalo()
        {
            // Deliberately schematic: a soft radial glow billboard, radius ~2×
            // the disk — "the invisible sheath", not a physical simulation.
            haloTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var px = new Color[256 * 256];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f)) / 127.5f;
                    float a = Mathf.SmoothStep(1f, 0f, d) * (0.35f + 0.65f * Mathf.SmoothStep(0f, 0.45f, d));
                    px[y * 256 + x] = new Color(0.55f, 0.55f, 1f, a);
                }
            haloTex.SetPixels(px);
            haloTex.Apply();

            halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "Dark Matter Halo (schematic)";
            Destroy(halo.GetComponent<Collider>());
            halo.transform.localScale = Vector3.one * 64f;
            var shader = Shader.Find("Sprites/Default");
            haloMat = new Material(shader) { mainTexture = haloTex };
            haloMat.color = new Color(1f, 1f, 1f, 0f); // beats 0-2: invisible
            halo.GetComponent<MeshRenderer>().sharedMaterial = haloMat;
            halo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---- the graph -------------------------------------------------------

        void EnsureGraph()
        {
            if (graphPanel != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());

            graphPanel = BlackHoleUI.MakePanel(canvas.transform, "Rotation Graph",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 40f), new Vector2(430f, 372f));

            BlackHoleUI.MakeText(graphPanel, "Title", 22, BlackHoleUI.TitleGold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(380f, 30f), FontStyle.Bold)
                .text = Loc.T("회전 곡선", "Rotation Curve", "回転曲線", "旋转曲线");

            var img = new GameObject("Plot") { hideFlags = HideFlags.DontSave };
            img.transform.SetParent(graphPanel, false);
            var rt = img.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -54f);
            rt.sizeDelta = new Vector2(382f, 220f);
            graphImage = img.AddComponent<RawImage>();
            graphImage.raycastTarget = false;

            BlackHoleUI.MakeText(graphPanel, "XLabel", 14, BlackHoleUI.TextSecondary, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(380f, 20f))
                .text = Loc.T("← 은하 중심에서의 거리 →", "← distance from the centre →",
                              "← 銀河中心からの距離 →", "← 与银心的距离 →");

            var legend = BlackHoleUI.MakeText(graphPanel, "Legend", 15, BlackHoleUI.TextPrimary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 42f), new Vector2(380f, 24f));
            legend.text =
                "<color=#FFC759>━ " + Loc.T("관측된 속도", "observed", "観測された速度", "观测速度") + "</color>    " +
                "<color=#73B3FF>━ " + Loc.T("보이는 물질만의 예측", "visible mass only", "見える物質だけの予測", "仅可见物质的预测") + "</color>";

            graphMode = BlackHoleUI.MakeText(graphPanel, "Mode", 16, BlackHoleUI.TextPrimary, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 14f), new Vector2(380f, 24f));
            UpdateGraphModeText();
        }

        void UpdateGraphModeText()
        {
            if (graphMode == null) return;
            graphMode.text = darkMatterOn
                ? Loc.T("<color=#FFC759>지금: 관측된 우주 (암흑물질 있음)</color>   [D] 전환",
                        "<color=#FFC759>Now: the observed universe (dark matter)</color>   [D] switch",
                        "<color=#FFC759>いま: 観測された宇宙（暗黒物質あり）</color>   [D] 切替",
                        "<color=#FFC759>当前: 观测宇宙（有暗物质）</color>   [D] 切换")
                : Loc.T("<color=#73B3FF>지금: 보이는 물질만의 우주</color>   [D] 전환",
                        "<color=#73B3FF>Now: a visible-matter-only universe</color>   [D] switch",
                        "<color=#73B3FF>いま: 見える物質だけの宇宙</color>   [D] 切替",
                        "<color=#73B3FF>当前: 只有可见物质的宇宙</color>   [D] 切换");
        }

        void RedrawGraph()
        {
            const int W = 382, H = 220, M = 14; // margins
            if (graphTex == null)
            {
                graphTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                graphTex.wrapMode = TextureWrapMode.Clamp;
                if (graphImage != null) graphImage.texture = graphTex;
            }
            var bg = new Color(0f, 0f, 0f, 0.28f);
            var axis = new Color(1f, 1f, 1f, 0.35f);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // axes
            for (int x = M; x < W - 4; x++) px[M * W + x] = axis;
            for (int y = M; y < H - 4; y++) px[y * W + M] = axis;

            // curves: r 0..17 kpc → x, v 0..260 → y
            System.Action<System.Func<float, float>, Color> plot = (f, c) =>
            {
                int prevY = -1;
                for (int x = M; x < W - 6; x++)
                {
                    float r = (x - M) / (float)(W - 6 - M) * 17f;
                    float v = Mathf.Clamp(f(r), 0f, 258f);
                    int y = M + Mathf.RoundToInt(v / 260f * (H - M - 8));
                    int lo = prevY < 0 ? y : Mathf.Min(prevY, y), hi = prevY < 0 ? y : Mathf.Max(prevY, y);
                    for (int yy = lo; yy <= hi; yy++)
                        for (int t = 0; t < 2; t++)
                            if (yy + t < H) px[(yy + t) * W + x] = c;
                    prevY = y;
                }
            };
            // Inactive curve dimmed; active curve full — the reader's eye goes
            // to the universe currently driving the tracers.
            var goldC = ObservedGold; var blueC = PredictedBlue;
            var goldDim = new Color(goldC.r, goldC.g, goldC.b, 0.30f);
            var blueDim = new Color(blueC.r, blueC.g, blueC.b, 0.30f);
            if (darkMatterOn)
            {
                plot(VPredicted, blueDim);
                plot(VObserved, goldC);
            }
            else
            {
                plot(VObserved, goldDim);
                plot(VPredicted, blueC);
            }

            // tracer radii ticks on the active curve
            System.Func<float, float> active = darkMatterOn ? (System.Func<float, float>)VObserved : VPredicted;
            var tickC = Color.white;
            foreach (var tr in tracers)
            {
                int x = M + Mathf.RoundToInt(tr.r / 17f * (W - 6 - M));
                int y = M + Mathf.RoundToInt(Mathf.Clamp(active(tr.r), 0f, 258f) / 260f * (H - M - 8));
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (dx * dx + dy * dy <= 4 && x + dx > 0 && x + dx < W && y + dy > 0 && y + dy < H)
                            px[(y + dy) * W + (x + dx)] = tickC;
            }

            graphTex.SetPixels(px);
            graphTex.Apply();
        }

        // ---------------- shared UI ------------------------------------------

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "Rot Stop", Abort);
            }
            stopButton.gameObject.SetActive(on);
            if (on)
                stopButton.GetComponentInChildren<Text>().text =
                    Loc.T("종료 ■", "End ■", "終了 ■", "结束 ■");
        }

        void Caption(string text)
        {
            if (caption == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Rot Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-90f, 96f), new Vector2(940f, 104f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 19, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 92f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            captionPanel.gameObject.SetActive(true);
            caption.text = text;
        }

        void OnDisable()
        {
            if (IsPlaying) Abort();
        }
    }
}
