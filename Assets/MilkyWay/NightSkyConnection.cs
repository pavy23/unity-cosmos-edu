using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect;

namespace MilkyWay
{
    /// <summary>
    /// The night-sky connection (F2): begin under a dark horizon looking at
    /// the band every human has seen — the Milky Way as it appears from Earth
    /// — then lift off, and watch that band turn into THIS disk. The inverse
    /// of the zoom journey's pedagogy: not "here is where we are" but "that
    /// thing you have already seen IS this."
    ///
    /// A matte ground plane sells the "standing outside at night" reading and
    /// fades away at liftoff. Same log-height ascent, exposure grading and
    /// scale-triggered captions as the zoom journey.
    /// </summary>
    public class NightSkyConnection : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Seconds for the liftoff (log-scale height ramp).")]
        public float liftDuration = 20f;

        public bool IsPlaying { get; private set; }

        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedFov, savedBrightness, savedStarBrightness;
        float savedDust, savedClump;
        Text caption;
        RectTransform captionPanel;
        Button stopButton;
        GameObject stage;          // ground + ridge + airglow, faded as one
        Material groundMat, ridgeMat, glowMat, propMat;

        public void Begin()
        {
            if (!Application.isPlaying || IsPlaying || controller == null) return;
            routine = StartCoroutine(Run());
        }

        void Update()
        {
            if (!IsPlaying) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Abort();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Abort();
#endif
        }

        public void Abort()
        {
            if (!IsPlaying) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            transform.position = savedPos;
            transform.rotation = savedRot;
            RestoreExposure();
            Finish();
        }

        void Finish()
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = savedFov;
            HideCaption();
            ShowStop(false);
            DestroyGround();
            if (orbit != null) orbit.enabled = true;
            IsPlaying = false;
        }

        void RestoreExposure()
        {
            controller.brightness = savedBrightness;
            controller.starBrightness = savedStarBrightness;
            if (savedDust > 0f) controller.dustStrength = savedDust;
            if (savedClump > 0f) controller.clumpiness = savedClump;
            controller.Apply();
        }

        // A beat may only fire once its predecessor's voice has finished —
        // thresholds alone cut the longer (ja, ko) lines mid-sentence.
        float narrEnd;
        bool NarrationDone => Time.time >= narrEnd;
        float Narrate(int i)
        {
            float len = NarrationManager.Instance.Play("mw_sky_" + i);
            narrEnd = Time.time + len + 0.4f;
            return len;
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            var cam = GetComponent<Camera>();
            savedPos = transform.position;
            savedRot = transform.rotation;
            savedFov = cam != null ? cam.fieldOfView : 38f;
            savedBrightness = controller.brightness;
            savedStarBrightness = controller.starBrightness;
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);

            Vector3 sun = controller.SunPositionWorld;
            Vector3 toCentre = (Vector3.zero - sun).normalized;

            // The observer's ground is NOT parallel to the galactic plane —
            // that alignment turns the band into 360° of horizon glow. Tilt
            // the whole stage around the centre line (real photographs'
            // geometry: Sagittarius low on the horizon, the band ARCHING
            // diagonally overhead). ~55° reads like a mid-latitude summer sky.
            Quaternion tilt = Quaternion.AngleAxis(55f, toCentre);
            Vector3 up = tilt * Vector3.up;

            // ---- Stage 0: a night sky. Dark-adapted, horizon below. --------
            controller.brightness = 0.85f;
            controller.starBrightness = 0.75f;
            // The Great Rift is dust: push extinction and clumping up while
            // we stand on the ground, so the band is visibly SPLIT and
            // mottled the way the real sky is. Restored during liftoff.
            savedDust = controller.dustStrength;
            savedClump = controller.clumpiness;
            controller.dustStrength = savedDust * 1.5f;
            controller.clumpiness = Mathf.Min(1.5f, savedClump * 1.35f);
            controller.Apply();
            if (cam != null) cam.fieldOfView = 58f;
            EnsureGround(sun, tilt);

            transform.position = sun;
            // Start 60° away from the centre, gazing slightly above the horizon.
            Quaternion fromRot = Quaternion.LookRotation(
                Quaternion.AngleAxis(60f, up) * toCentre + up * 0.12f, up);
            Quaternion toRot = Quaternion.LookRotation(toCentre + up * 0.10f, up);
            transform.rotation = fromRot;

            float len = Narrate(0);
            Caption(Loc.T(
                "맑은 밤, 불빛 없는 곳에서 하늘을 보면 — 뿌연 빛의 띠가 하늘을 가로지릅니다.\n사람들이 은하수라 부르는 그것입니다.",
                "On a clear, dark night, a hazy band of light crosses the sky.\nPeople call it the Milky Way.",
                "澄んだ暗い夜、空を見上げると — ぼんやりした光の帯が横切っています。\n人々が天の川と呼ぶものです。",
                "在晴朗无光害的夜晚，一条朦胧的光带横贯天空。\n人们称它为银河。"));
            // Slow pan along the band toward the galactic centre.
            for (float t = 0f, dur = Mathf.Max(9f, len + 0.5f); t < dur; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(fromRot, toRot, Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }

            // ---- Stage 1: the centre direction + the rift -------------------
            len = Narrate(1);
            Caption(Loc.T(
                "띠가 가장 밝고 두꺼운 이쪽 — 은하 중심 방향입니다.\n띠를 가르는 검은 틈은 별빛을 가린 먼지 구름입니다.",
                "Here the band is at its brightest and widest — the direction of the galactic centre.\nThe dark rift splitting it is dust, blocking the starlight.",
                "帯がもっとも明るく厚いこちら側 — 銀河中心の方向です。\n帯を裂く黒い筋は、星の光をさえぎる塵の雲です。",
                "光带在这里最亮最宽——那是银心的方向。\n将它割开的黑暗裂缝，是遮挡星光的尘埃云。"));
            for (float t = 0f, dur = Mathf.Max(7f, len + 0.5f); t < dur; t += Time.deltaTime)
                yield return null;

            // ---- Stage 2: liftoff — the band becomes the disk ---------------
            len = Narrate(2);
            Caption(Loc.T(
                "이제 떠올라 봅니다.\n하늘의 띠가... 우리가 안에서 옆으로 바라본, 은하의 원반이었습니다.",
                "Now we rise.\nThat band in the sky... was this disk, seen edge-on from the inside.",
                "さあ、浮かび上がります。\n空の帯は…円盤を内側から真横に見た姿だったのです。",
                "现在我们升空。\n天上的那条带……原来是这个圆盘，从内部侧看的样子。"));

            float h0 = 0.02f, h1 = 26f;
            int stage = 0;
            for (float t = 0f; t < liftDuration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / liftDuration);
                float h = Mathf.Exp(Mathf.Lerp(Mathf.Log(h0), Mathf.Log(h1), u));
                // Rise along the STAGE's up (world-up drifts in as height
                // makes the tilt meaningless), drifting outward so the whole
                // disk fits.
                Vector3 riseUp = Vector3.Slerp(up, Vector3.up, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.7f, u)));
                float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, u)) * 9f;
                transform.position = sun + riseUp * h + new Vector3(0.35f, 0f, 0.2f).normalized * drift;
                // Gaze: from the horizon-line of the centre up... to looking DOWN
                // at the centre as we gain height. LookAt handles it continuously.
                transform.LookAt(Vector3.Lerp(sun + toCentre * 8f, Vector3.zero,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, u))), riseUp);
                // The ground-stage dust boost eases back out with height.
                float dustEase = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, u));
                controller.dustStrength = Mathf.Lerp(savedDust * 1.5f, savedDust, dustEase);
                controller.clumpiness = Mathf.Lerp(Mathf.Min(1.5f, savedClump * 1.35f), savedClump, dustEase);

                // The night stage fades away in the first quarter of the climb.
                if (stage != null)
                {
                    float a = 1f - Mathf.InverseLerp(0.02f, 0.18f, u);
                    FadeStage(a);
                    if (a <= 0f) DestroyGround();
                }

                // Exposure brightens as we leave the disk.
                float ex = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, u));
                controller.brightness = Mathf.Lerp(0.85f, 2.2f, ex);
                controller.starBrightness = Mathf.Lerp(0.75f, 1.15f, ex);
                controller.Apply();
                if (cam != null) cam.fieldOfView = Mathf.Lerp(58f, 40f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, u)));

                if (stage == 0 && u > 0.55f && NarrationDone)
                {
                    stage = 1; len = Narrate(3);
                    Caption(Loc.T(
                        "은하수를 본 적이 있다면 — 이미 우리 은하의 옆모습을 본 것입니다.",
                        "If you have ever seen the Milky Way, you have already seen our galaxy — edge-on.",
                        "天の川を見たことがあるなら — もう銀河の横顔を見ていたのです。",
                        "如果你见过银河——你其实早已见过我们星系的侧影。"));
                }
                yield return null;
            }

            // Hold the reveal, then hand back to the ambient orbit.
            for (float t = 0f; t < 5f || !NarrationDone; t += Time.deltaTime) yield return null;
            RestoreExposure();
            Finish();
        }

        // ---------------- props ----------------

        /// <summary>
        /// The night stage: a dark ground plane, a NOISY MOUNTAIN RIDGE running
        /// the whole horizon, and a faint blue airglow band above it — the three
        /// cues that read as "standing on Earth at night" instead of "floating
        /// in space next to a black disk". Pure staging; fades out at liftoff.
        /// </summary>
        void EnsureGround(Vector3 sun, Quaternion tilt)
        {
            if (stage != null) return;
            stage = new GameObject("Night Stage (staging)");
            stage.transform.position = sun;
            stage.transform.rotation = tilt; // ground plane tilted off the galactic plane

            var sprite = Shader.Find("Sprites/Default"); // vertex colours + alpha, draws fine in URP

            // ---- ground: matte disc just below the eye ----
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Ground";
            Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(stage.transform, false);
            ground.transform.localPosition = Vector3.down * 0.012f;
            ground.transform.localScale = new Vector3(1.2f, 0.001f, 1.2f);
            groundMat = new Material(sprite) { renderQueue = 3100 };
            groundMat.color = new Color(0.010f, 0.013f, 0.020f, 1f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // ---- mountain ridge: a ring strip whose top edge is noise ----
            // Reaches well below the eye line so no gap opens between the
            // silhouette and the ground disc; near-black, or ACES lifts it grey.
            ridgeMat = new Material(sprite) { renderQueue = 3102 };
            BuildRing(stage.transform, "Ridge", ridgeMat, 0.5f,
                yBottom: -0.03f,
                topOf: a => 0.006f
                          + 0.028f * Mathf.PerlinNoise(a * 1.7f, 3.7f)
                          + 0.008f * Mathf.PerlinNoise(a * 6.3f, 9.1f),
                bottomCol: new Color(0.004f, 0.005f, 0.009f, 1f),
                topCol: new Color(0.004f, 0.005f, 0.009f, 1f));

            // ---- airglow: a subtle blue gradient above the ridge tops ----
            glowMat = new Material(sprite) { renderQueue = 3101 };
            BuildRing(stage.transform, "Airglow", glowMat, 0.55f,
                yBottom: 0.004f,
                topOf: a => 0.06f,
                bottomCol: new Color(0.09f, 0.14f, 0.24f, 0.14f),
                topCol: new Color(0.09f, 0.14f, 0.24f, 0f));

            BuildForegroundProps(sprite, sun);
        }

        // ---- foreground silhouettes: the people this sky belongs to ----
        // A parent and child hand in hand, their dog, a house, a few trees —
        // the classic night-photo foreground, built from primitives in the
        // same near-black as the ridge. Scale: the eye stands 0.012 units
        // above the ground, so 1 unit ≈ 140 m; an adult is ~0.012.
        void BuildForegroundProps(Shader sprite, Vector3 sun)
        {
            propMat = new Material(sprite) { renderQueue = 3103 };
            propMat.color = new Color(0.004f, 0.005f, 0.009f, 1f);

            Vector3 toCentre = -sun; toCentre.y = 0f; toCentre.Normalize();
            float groundY = -0.012f;

            // Position helper: azimuth degrees off the galactic-centre line.
            Vector3 At(float azDeg, float r) =>
                Quaternion.AngleAxis(azDeg, Vector3.up) * toCentre * r + Vector3.up * groundY;

            Transform P(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(stage.transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                go.GetComponent<MeshRenderer>().sharedMaterial = propMat;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go.transform;
            }

            // The family, 12 m out, just beside the direction we end up
            // facing — they watch the centre with us.
            Vector3 adultPos = At(13f, 0.085f);
            P(PrimitiveType.Capsule, adultPos + Vector3.up * 0.0055f, new Vector3(0.0034f, 0.0042f, 0.0034f)); // body
            P(PrimitiveType.Sphere, adultPos + Vector3.up * 0.0112f, Vector3.one * 0.0036f);                  // head
            Vector3 childPos = At(16.5f, 0.083f);
            P(PrimitiveType.Capsule, childPos + Vector3.up * 0.0032f, new Vector3(0.0026f, 0.0026f, 0.0026f));
            P(PrimitiveType.Sphere, childPos + Vector3.up * 0.0068f, Vector3.one * 0.0028f);
            // Held hands: one thin bar between the two silhouettes.
            Vector3 handA = adultPos + Vector3.up * 0.0052f;
            Vector3 handB = childPos + Vector3.up * 0.0042f;
            P(PrimitiveType.Cylinder, (handA + handB) * 0.5f,
              new Vector3(0.0007f, Vector3.Distance(handA, handB) * 0.5f, 0.0007f),
              Quaternion.FromToRotation(Vector3.up, handB - handA));

            // The dog, sitting a step ahead of the child.
            Vector3 dogPos = At(19f, 0.079f);
            P(PrimitiveType.Capsule, dogPos + Vector3.up * 0.0018f, new Vector3(0.0014f, 0.0016f, 0.0014f),
              Quaternion.Euler(70f, 0f, 0f));                                                  // body, leaning back on haunches
            P(PrimitiveType.Sphere, dogPos + Vector3.up * 0.0036f + Quaternion.AngleAxis(19f, Vector3.up) * toCentre * 0.0012f,
              Vector3.one * 0.0018f);                                                          // head
            P(PrimitiveType.Capsule, dogPos + Vector3.up * 0.0044f, new Vector3(0.0005f, 0.0009f, 0.0005f),
              Quaternion.Euler(0f, 0f, 24f));                                                  // an ear

            // The house, off to the side: box, gable roof, chimney, 30 m out.
            Vector3 housePos = At(-42f, 0.21f);
            P(PrimitiveType.Cube, housePos + Vector3.up * 0.009f, new Vector3(0.030f, 0.018f, 0.020f));
            P(PrimitiveType.Cube, housePos + Vector3.up * 0.021f, new Vector3(0.023f, 0.010f, 0.0145f),
              Quaternion.Euler(0f, 0f, 45f));
            P(PrimitiveType.Cube, housePos + Vector3.up * 0.026f + Vector3.right * 0.008f,
              new Vector3(0.004f, 0.008f, 0.004f));

            // A few trees, both sides, varied heights — spheres on a trunk
            // read as broadleaf silhouettes at night.
            void Tree(float az, float r, float s)
            {
                Vector3 basePos = At(az, r);
                P(PrimitiveType.Cylinder, basePos + Vector3.up * 0.006f * s, new Vector3(0.0016f * s, 0.006f * s, 0.0016f * s));
                P(PrimitiveType.Sphere, basePos + Vector3.up * 0.0135f * s, Vector3.one * 0.012f * s);
                P(PrimitiveType.Sphere, basePos + Vector3.up * 0.019f * s, Vector3.one * 0.008f * s);
                P(PrimitiveType.Sphere, basePos + (Vector3.up * 0.012f + Vector3.forward * 0.005f) * s, Vector3.one * 0.008f * s);
            }
            Tree(-56f, 0.16f, 1.0f);
            Tree(-30f, 0.28f, 1.5f);
            Tree(36f, 0.22f, 1.2f);
            Tree(62f, 0.13f, 0.8f);
            Tree(6f, 0.34f, 1.6f);
        }

        /// <summary>Builds a cylindrical strip around the viewer whose top edge
        /// height is a function of azimuth — mountains are just noise on a ring.</summary>
        static void BuildRing(Transform parent, string name, Material mat, float radius,
            float yBottom, System.Func<float, float> topOf, Color bottomCol, Color topCol)
        {
            const int N = 240;
            var verts = new Vector3[(N + 1) * 2];
            var cols = new Color[(N + 1) * 2];
            var tris = new int[N * 6];
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * radius + Vector3.up * yBottom;
                verts[i * 2 + 1] = dir * radius + Vector3.up * topOf(a);
                cols[i * 2] = bottomCol;
                cols[i * 2 + 1] = topCol;
                if (i < N)
                {
                    int t = i * 6, v = i * 2;
                    tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                    tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
                }
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void FadeStage(float a)
        {
            if (propMat != null) { var c = propMat.color; c.a = a; propMat.color = c; }
            if (groundMat != null) { var c = groundMat.color; c.a = a; groundMat.color = c; }
            if (ridgeMat != null) { var c = ridgeMat.color; c.a = a; ridgeMat.color = c; }
            if (glowMat != null) { var c = glowMat.color; c.a = a; glowMat.color = c; }
        }

        void DestroyGround()
        {
            if (stage != null) Object.Destroy(stage);
            if (groundMat != null) Object.Destroy(groundMat);
            if (ridgeMat != null) Object.Destroy(ridgeMat);
            if (glowMat != null) Object.Destroy(glowMat);
            if (propMat != null) Object.Destroy(propMat);
            stage = null;
            groundMat = null; ridgeMat = null; glowMat = null; propMat = null;
        }

        // ---------------- UI ----------------

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "Sky Stop", Abort);
            }
            stopButton.gameObject.SetActive(on);
            if (on)
                stopButton.GetComponentInChildren<Text>().text =
                    Loc.T("중단 ■", "Stop ■", "中止 ■", "停止 ■");
        }

        void Caption(string text)
        {
            if (caption == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Sky Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(900f, 100f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 21, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 84f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            captionPanel.gameObject.SetActive(true);
            caption.text = text;
        }

        void HideCaption()
        {
            if (captionPanel != null) captionPanel.gameObject.SetActive(false);
        }
    }
}
