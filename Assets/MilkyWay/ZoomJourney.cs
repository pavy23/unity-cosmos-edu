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
        SolarSystemRig solarSystem;
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
        // A beat may only fire once its predecessor's voice has finished —
        // thresholds alone cut the longer (ja, ko) lines mid-sentence.
        float narrEnd;
        bool NarrationDone => Time.time >= narrEnd;
        float Narrate(int i)
        {
            float len = NarrationManager.Instance.Play("mw_zoom_" + i);
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
                if (stage == 0 && u > 0.06f && NarrationDone)
                {
                    stage = 1; Narrate(1);
                    Caption(Loc.T(
                        "행성들이 먼저 사라지고, 이윽고 태양도 — 수천억 개의 별 가운데 하나가 됩니다.",
                        "The planets vanish first, and then the Sun itself — one star among hundreds of billions.",
                        "惑星がまず見えなくなり、やがて太陽も — 数千億の星のひとつになります。",
                        "行星先消失了，接着太阳也——成为数千亿颗恒星中的一颗。"));
                }
                else if (stage == 1 && u > 0.4f && NarrationDone)
                {
                    stage = 2; Narrate(2);
                    Caption(Loc.T(
                        "우리는 은하 중심에서 약 2만 6천 광년, 나선팔 사이의 조용한 자리에 삽니다.",
                        "We live about 26,000 light-years from the centre, in a quiet spot between spiral arms.",
                        "私たちは銀河中心から約2万6千光年、渦状腕のあいだの静かな場所に住んでいます。",
                        "我们住在距离银心约2.6万光年的地方，在旋臂之间一处安静的角落。"));
                }
                else if (stage == 2 && u > 0.74f && NarrationDone)
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
            for (float t = 0f; t < 6f || !NarrationDone; t += Time.deltaTime)
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
        /// The solar system we depart from — the detailed SolarSystemRig
        /// (procedural planet surfaces, real tilts, moons, Saturn's rings),
        /// placed where the real thing lives. Still wildly out of scale — a
        /// real Neptune orbit is 1.5e-9 kpc — and the caption stays honest
        /// about it: this is the model on the teacher's desk.
        /// </summary>
        void EnsureSunProp()
        {
            if (solarSystem != null) return;
            solarSystem = SolarSystemRig.Spawn(controller.SunPositionWorld);
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
            if (solarSystem != null) Destroy(solarSystem.gameObject);
            if (marker != null) Destroy(marker.gameObject);
            if (markerMat != null) Destroy(markerMat);
            solarSystem = null; marker = null; markerMat = null;
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
