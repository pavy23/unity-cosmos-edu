using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The zoom journey (F1): from beside the Sun to the full galaxy, in the
    /// spirit of Powers of Ten. Distance runs on a LOG scale — every decade of
    /// scale takes the same time, which is what makes a scale journey feel
    /// steady instead of slamming through the interesting part in one second.
    ///
    /// Exposure rides along: inside the disk the eye is dark-adapted
    /// (brightness graded down, the night-sky look), and it brightens as we
    /// pull out — the same physical honesty as the fall-in's blackness in the
    /// black-hole exhibit.
    /// </summary>
    public class ZoomJourney : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Seconds for the main pull-out (the log-scale dolly).")]
        public float zoomDuration = 26f;

        public bool IsPlaying { get; private set; }

        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedFov, savedBrightness, savedStarBrightness;
        Text caption;
        RectTransform captionPanel;
        Button stopButton;
        GameObject sunProp;
        Material sunMat;
        LineRenderer marker;
        Material markerMat;

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

        /// <summary>Stop button / Esc: put everything back where it was.</summary>
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
            DestroyProps();
            if (orbit != null) orbit.enabled = true;
            IsPlaying = false;
        }

        void RestoreExposure()
        {
            controller.brightness = savedBrightness;
            controller.starBrightness = savedStarBrightness;
            controller.Apply();
        }

        /// <summary>Plays mw_zoom_{i}; returns clip length (0 while the TTS
        /// clips are not generated yet — stages fall back to their minimums).</summary>
        static float Narrate(int i) => NarrationManager.Instance.Play("mw_zoom_" + i);

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
            EnsureSunProp();

            Vector3 sun = controller.SunPositionWorld;
            // Departure direction: outward with a tangential slant so the pull
            // -out sweeps around the Sun instead of backing straight off; the
            // overview direction is the showcase's classic three-quarter view.
            Vector3 dirNear = new Vector3(0.55f, 0.16f, 0.82f).normalized;
            Vector3 dirFar = new Vector3(0f, 0.52f, -0.86f).normalized;

            // ---- Stage 0: standing at the Sun, dark-adapted ----------------
            controller.brightness = 0.85f;
            controller.starBrightness = 0.75f;
            controller.Apply();
            if (cam != null) cam.fieldOfView = 56f;

            float len = Narrate(0);
            Caption(Loc.T(
                "여기는 우리 태양계입니다. (행성의 크기와 궤도는 보기 좋게 과장했습니다)\n그 뒤로 하늘을 가로지르는 빛의 띠 — 우리 은하를 안에서 본 모습입니다.",
                "This is our solar system (planet sizes and orbits exaggerated to be visible).\nBehind it, a band of light crosses the sky — our galaxy, seen from inside.",
                "ここは私たちの太陽系です。（惑星の大きさと軌道は見やすく誇張しています）\nその奥、空を横切る光の帯 — 銀河を内側から見た姿です。",
                "这是我们的太阳系（行星大小与轨道为便于观看而夸大）。\n它身后横贯天空的光带——就是从内部看到的银河。"));
            // A slow drift while the framing line lands; never a frozen frame.
            for (float t = 0f, dur = Mathf.Max(7f, len + 0.5f); t < dur; t += Time.deltaTime)
            {
                float d = Mathf.Lerp(0.10f, 0.14f, t / dur);
                transform.position = sun + dirNear * d;
                transform.LookAt(sun);
                yield return null;
            }

            // ---- Stages 1-3: the log-scale pull-out -------------------------
            float d0 = 0.14f, d1 = 52f;
            int stage = 0;
            for (float t = 0f; t < zoomDuration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / zoomDuration);
                float d = Mathf.Exp(Mathf.Lerp(Mathf.Log(d0), Mathf.Log(d1), u));

                // The pivot slides from the Sun to the galactic centre while
                // we are still close enough for the hand-off to be invisible.
                float pivotT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.55f, u));
                Vector3 pivot = Vector3.Lerp(sun, Vector3.zero, pivotT);
                Vector3 dir = Vector3.Slerp(dirNear, dirFar, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.72f, u)));

                transform.position = pivot + dir * d;
                transform.LookAt(pivot);

                // Dark-adapted eye brightening as we leave the disk.
                float ex = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.75f, u));
                controller.brightness = Mathf.Lerp(0.85f, 2.2f, ex);
                controller.starBrightness = Mathf.Lerp(0.75f, 1.15f, ex);
                controller.Apply();
                if (cam != null) cam.fieldOfView = Mathf.Lerp(56f, 38f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.7f, u)));

                // Captions swap at scale thresholds, not clock times.
                if (stage == 0 && u > 0.06f)
                {
                    stage = 1; Narrate(1);
                    Caption(Loc.T(
                        "행성들이 먼저 사라지고, 이윽고 태양도 — 수천억 개의 별 가운데 하나가 됩니다.",
                        "The planets vanish first, and then the Sun itself — one star among hundreds of billions.",
                        "惑星がまず見えなくなり、やがて太陽も — 数千億の星のひとつになります。",
                        "行星先消失了，接着太阳也——成为数千亿颗恒星中的一颗。"));
                }
                else if (stage == 1 && u > 0.4f)
                {
                    stage = 2; Narrate(2);
                    Caption(Loc.T(
                        "우리는 은하 중심에서 약 2만 6천 광년, 나선팔 사이의 조용한 자리에 삽니다.",
                        "We live about 26,000 light-years from the centre, in a quiet spot between spiral arms.",
                        "私たちは銀河中心から約2万6千光年、渦状腕のあいだの静かな場所に住んでいます。",
                        "我们住在距离银心约2.6万光年的地方，在旋臂之间一处安静的角落。"));
                }
                else if (stage == 2 && u > 0.74f)
                {
                    stage = 3; Narrate(3);
                    EnsureMarker(sun);
                    Caption(Loc.T(
                        "지름 약 10만 광년 — 이것이 우리 은하입니다.\n금색 원이 우리의 자리입니다: 우리는 여기 있습니다.",
                        "About 100,000 light-years across — this is the Milky Way.\nThe gold ring marks our place: we are here.",
                        "差し渡し約10万光年 — これが天の川銀河です。\n金色の輪が私たちの場所。私たちはここにいます。",
                        "直径约十万光年——这就是银河系。\n金色圆环标记着我们的位置：我们在这里。"));
                }

                if (marker != null) PulseMarker(t);
                yield return null;
            }

            // ---- Hold the overview while the closing line finishes ----------
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                if (marker != null) PulseMarker(zoomDuration + t);
                yield return null;
            }

            // The journey ENDS at the overview — that is the point of it. The
            // ambient orbit takes over from here; only Abort snaps back.
            RestoreExposure(); // overview exposure == the defaults we saved
            Finish();
        }

        // ---------------- props ----------------

        /// <summary>
        /// The solar system we depart from: the Sun (the black-hole exhibit's
        /// StarSurface shader — granulation + limb darkening) and an orrery of
        /// the eight planets on faint orbit rings, ticking along at Kepler
        /// -proportioned speeds. Wildly out of scale — a real Neptune orbit is
        /// 1.5e-9 kpc — and honest about it in the caption: this is the model
        /// on the teacher's desk, placed where the real thing lives.
        /// </summary>
        void EnsureSunProp()
        {
            if (sunProp != null) return;
            sunProp = new GameObject("Solar System (journey prop)");
            sunProp.transform.position = controller.SunPositionWorld;

            // ---- the Sun ----
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun";
            Destroy(sun.GetComponent<Collider>());
            sun.transform.SetParent(sunProp.transform, false);
            sun.transform.localScale = Vector3.one * 0.012f;
            var starShader = Shader.Find("BlackHole/StarSurface");
            if (starShader != null)
            {
                sunMat = new Material(starShader);
                sunMat.SetColor("_StarColor", new Color(2.6f, 2.2f, 1.4f)); // G-type warmth
                sunMat.SetFloat("_Granulation", 0.45f);
                sunMat.SetFloat("_GranScale", 8f);
                sunMat.SetFloat("_SpotStrength", 0.22f);
                sunMat.SetFloat("_CoronaBoost", 0.8f);
                sun.GetComponent<MeshRenderer>().sharedMaterial = sunMat;
            }

            // ---- the planets: radius(kpc-orrery), size, colour, has ring ----
            var defs = new (string name, float orbit, float size, Color col, bool ring)[]
            {
                ("Mercury", 0.010f, 0.0011f, new Color(0.62f, 0.58f, 0.54f), false),
                ("Venus",   0.014f, 0.0016f, new Color(0.92f, 0.82f, 0.62f), false),
                ("Earth",   0.018f, 0.0017f, new Color(0.35f, 0.55f, 0.95f), false),
                ("Mars",    0.023f, 0.0013f, new Color(0.88f, 0.45f, 0.28f), false),
                ("Jupiter", 0.030f, 0.0036f, new Color(0.85f, 0.72f, 0.55f), false),
                ("Saturn",  0.037f, 0.0031f, new Color(0.90f, 0.80f, 0.58f), true),
                ("Uranus",  0.043f, 0.0023f, new Color(0.60f, 0.85f, 0.88f), false),
                ("Neptune", 0.048f, 0.0022f, new Color(0.30f, 0.45f, 0.90f), false),
            };

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var lineShader = Shader.Find("Sprites/Default");
            var orrery = sunProp.AddComponent<Orrery>();
            orrery.planets = new Transform[defs.Length];
            orrery.radii = new float[defs.Length];
            orrery.speeds = new float[defs.Length];
            orrery.phases = new float[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.name = d.name;
                Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(sunProp.transform, false);
                p.transform.localScale = Vector3.one * d.size;
                var pm = new Material(unlit);
                pm.color = d.col * 1.15f; // a touch of glow so bloom lifts them
                p.GetComponent<MeshRenderer>().sharedMaterial = pm;
                orrery.mats.Add(pm);

                if (d.ring)
                {
                    // Saturn's ring: a flattened unlit disc.
                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.name = "Ring";
                    Destroy(ring.GetComponent<Collider>());
                    ring.transform.SetParent(p.transform, false);
                    ring.transform.localScale = new Vector3(2.3f, 0.02f, 2.3f);
                    ring.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
                    var rm = new Material(unlit);
                    rm.color = new Color(0.85f, 0.78f, 0.60f, 1f) * 0.8f;
                    ring.GetComponent<MeshRenderer>().sharedMaterial = rm;
                    orrery.mats.Add(rm);
                }

                // Faint orbit ring so the architecture of the system reads.
                var line = new GameObject(d.name + " Orbit").AddComponent<LineRenderer>();
                line.transform.SetParent(sunProp.transform, false);
                const int N = 96;
                line.positionCount = N;
                line.loop = true;
                line.useWorldSpace = false;
                line.widthMultiplier = 0.00045f;
                line.material = new Material(lineShader);
                orrery.mats.Add(line.material);
                line.startColor = line.endColor = new Color(0.6f, 0.7f, 0.9f, 0.22f);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int k = 0; k < N; k++)
                {
                    float a = k / (float)N * Mathf.PI * 2f;
                    line.SetPosition(k, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d.orbit);
                }

                orrery.planets[i] = p.transform;
                orrery.radii[i] = d.orbit;
                // Kepler proportions (ω ∝ r^-1.5), scaled so Mercury laps
                // visibly during the framing beat.
                orrery.speeds[i] = 2.2f * Mathf.Pow(defs[0].orbit / d.orbit, 1.5f);
                orrery.phases[i] = (i * 137.5f) * Mathf.Deg2Rad; // spread them out
            }
        }

        /// <summary>Ticks the planets around their rings — self-updating so the
        /// orrery lives through every journey stage without coroutine plumbing.</summary>
        class Orrery : MonoBehaviour
        {
            public Transform[] planets;
            public float[] radii, speeds, phases;
            public System.Collections.Generic.List<Material> mats = new();

            void Update()
            {
                float t = Time.time;
                for (int i = 0; i < planets.Length; i++)
                {
                    if (planets[i] == null) continue;
                    float a = phases[i] + t * speeds[i];
                    planets[i].localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radii[i];
                }
            }

            // The prop materials are created per run; a GameObject's death does
            // not free them, so the orrery cleans up after itself.
            void OnDestroy()
            {
                foreach (var m in mats)
                    if (m != null) Destroy(m);
            }
        }

        /// <summary>"We are here": a gold ring in the disk plane at the Sun's
        /// radius-neighbourhood, faded in for the final overview.</summary>
        void EnsureMarker(Vector3 sun)
        {
            if (marker != null) return;
            var go = new GameObject("You Are Here");
            var line = go.AddComponent<LineRenderer>();
            const int N = 64;
            line.positionCount = N;
            line.loop = true;
            line.useWorldSpace = true;
            // Bold enough to read from the 52 kpc overview — at that range the
            // ring subtends barely 2°, and a thin line drowns in the starfield.
            line.widthMultiplier = 0.22f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerMat = new Material(Shader.Find("Sprites/Default"));
            line.material = markerMat;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                line.SetPosition(i, sun + new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * 1.6f);
            }
            marker = line;
        }

        void PulseMarker(float t)
        {
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 2.6f);
            // HDR gold: bright enough that bloom picks the ring out of the disk.
            var c = new Color(1.7f, 1.25f, 0.55f, pulse);
            marker.startColor = c;
            marker.endColor = c;
        }

        void DestroyProps()
        {
            if (sunProp != null) Destroy(sunProp);
            if (sunMat != null) Destroy(sunMat);
            if (marker != null) Destroy(marker.gameObject);
            if (markerMat != null) Destroy(markerMat);
            sunProp = null; sunMat = null; marker = null; markerMat = null;
        }

        // ---------------- UI (the black-hole exhibit's shared factory) ------

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "Zoom Stop", Abort);
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
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Zoom Caption",
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
