using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The cosmic zoom-out (F5): the zoom journey's sequel, one scale-ladder
    /// rung up. We leave the galaxy along a log-scale dolly — Local Group,
    /// Virgo cluster, cosmic web — until every point of light on screen is a
    /// galaxy, then fly home. The Milky Way stays centred the whole way: the
    /// "you are here" anchor shrinking from a world into a dot.
    ///
    /// Unlike the zoom journey this one RETURNS at the end: ambient mouse
    /// control is tuned for the galaxy's scale (distance clamp ~110), so
    /// ending 180,000 kpc out would strand the user. The return is also the
    /// lesson — all of that, and here we are again.
    /// </summary>
    public class CosmicZoomOut : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;
        public CosmicWebField field;

        [Tooltip("Seconds for the outbound log dolly.")]
        public float outDuration = 44f;
        [Tooltip("Seconds for the flight home.")]
        public float returnDuration = 8f;
        [Tooltip("Impostor brightness multiplier at full depth. The energy-" +
                 "conserving impostors are physically dim points out there; " +
                 "this is the exposure ride that keeps the deep field FULL of " +
                 "galaxies — the whole point of the shot.")]
        public float deepExposure = 40f;

        public bool IsPlaying { get; private set; }

        const float DNear = 35f, DFar = 180000f;

        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedFov, savedNear, savedFar;
        Canvas uiCanvas;
        float savedPlaneDistance;
        Material skyOriginal, skyInstance;
        float skyStars, skyGalaxies;
        Text caption;
        RectTransform captionPanel;
        Button stopButton;

        public void Begin()
        {
            if (!Application.isPlaying || IsPlaying || controller == null || field == null) return;
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
            Finish();
        }

        void Finish()
        {
            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = savedFov;
                cam.nearClipPlane = savedNear;
                cam.farClipPlane = savedFar;
            }
            field.SetBrightness(0f);
            field.GetComponent<MeshRenderer>().enabled = false;
            if (uiCanvas != null) uiCanvas.planeDistance = savedPlaneDistance;
            // Swap the original skybox asset back and drop our fade instance —
            // never write fade values into the shared .mat (the StarfieldSkybox
            // drift lesson).
            if (skyOriginal != null) RenderSettings.skybox = skyOriginal;
            if (skyInstance != null) Destroy(skyInstance);
            skyInstance = null;
            HideCaption();
            ShowStop(false);
            if (orbit != null) orbit.enabled = true;
            IsPlaying = false;
        }

        // A beat may only fire once its predecessor's voice has finished —
        // thresholds alone cut the longer (ja, ko) lines mid-sentence.
        float narrEnd;
        bool NarrationDone => Time.time >= narrEnd;
        float Narrate(int i)
        {
            float len = NarrationManager.Instance.Play("mw_web_" + i);
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
            savedNear = cam != null ? cam.nearClipPlane : 0.02f;
            savedFar = cam != null ? cam.farClipPlane : 600f;
            uiCanvas = cam != null ? BlackHoleUI.EnsureCanvas(cam) : null;
            savedPlaneDistance = uiCanvas != null ? uiCanvas.planeDistance : 0.15f;
            skyOriginal = RenderSettings.skybox;
            if (skyOriginal != null)
            {
                skyInstance = new Material(skyOriginal);
                skyStars = skyInstance.HasProperty("_StarDensity") ? skyInstance.GetFloat("_StarDensity") : 0f;
                skyGalaxies = skyInstance.HasProperty("_GalaxyCount") ? skyInstance.GetFloat("_GalaxyCount") : 0f;
                RenderSettings.skybox = skyInstance;
            }
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);

            field.EnsureBuilt();
            field.GetComponent<MeshRenderer>().enabled = true;
            field.SetBrightness(0f);
            if (cam != null) cam.fieldOfView = 38f;

            Vector3 dir = CosmicWebField.CameraDir;

            // ---- Stage 0: glide from wherever we are onto the departure ray
            float len = Narrate(0);
            Caption(Loc.T(
                "이제 우리 은하를 떠나 봅니다. 우주가 얼마나 큰지 —\n지금까지 본 이 원반이, 곧 하나의 점이 됩니다.",
                "Now we leave our galaxy, to see how big the universe is.\nThis disk — everything so far — is about to become a single dot.",
                "さあ、銀河を離れてみましょう。宇宙がどれほど広いか —\nこれまで見てきたこの円盤が、まもなくひとつの点になります。",
                "现在，让我们离开银河系，去看看宇宙有多大——\n迄今所见的这个圆盘，很快会变成一个点。"));
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            for (float t = 0f, dur = Mathf.Max(5f, len * 0.55f); t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(fromPos, dir * DNear, u);
                transform.rotation = Quaternion.Slerp(fromRot,
                    Quaternion.LookRotation(-dir, Vector3.up), u);
                yield return null;
            }

            // ---- Outbound: the log dolly ------------------------------------
            float lnA = Mathf.Log(DNear), lnB = Mathf.Log(DFar);
            int stage = 0;
            for (float t = 0f; t < outDuration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / outDuration);
                float d = Mathf.Exp(Mathf.Lerp(lnA, lnB, u));
                Ride(cam, dir, d);

                // Impostors fade in as we leave — no pop at the start.
                Atmosphere(u, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.16f, u)));

                if (stage == 0 && u > 0.23f && NarrationDone)
                {
                    stage = 1; Narrate(1);
                    Caption(Loc.T(
                        "저기 — 안드로메다 은하입니다. 우리 은하와 안드로메다, 그리고 수십 개의\n작은 은하들이 '국부은하군'이라는 가족을 이룹니다.",
                        "There — the Andromeda galaxy. Our galaxy, Andromeda, and dozens of\nsmall companions form a family called the Local Group.",
                        "ほら — アンドロメダ銀河です。私たちの銀河とアンドロメダ、そして数十の\n小さな銀河が「局所銀河群」という家族をつくっています。",
                        "看——仙女座星系。我们的银河系、仙女座，以及数十个小星系，\n组成一个叫做'本星系群'的家族。"));
                }
                else if (stage == 1 && u > 0.60f && NarrationDone)
                {
                    stage = 2; Narrate(2);
                    Caption(Loc.T(
                        "은하들이 모여듭니다 — 처녀자리 은하단. 은하 천여 개가 서로의 중력에\n묶여 있습니다. 우리 국부은하군은 이 거대한 도시의 변두리 마을입니다.",
                        "Galaxies gather — the Virgo cluster, a thousand of them bound by each\nother's gravity. Our Local Group is a small village on its outskirts.",
                        "銀河が集まってきます — おとめ座銀河団。千あまりの銀河が互いの重力で\n結ばれています。局所銀河群は、この大都市の郊外の村です。",
                        "星系聚集起来——室女座星系团，上千个星系被彼此的引力束缚。\n我们的本星系群，只是这座大城市郊外的小村庄。"));
                }
                else if (stage == 2 && u > 0.83f && NarrationDone)
                {
                    stage = 3; Narrate(3);
                    Caption(Loc.T(
                        "더 물러나면 은하들은 거미줄처럼 이어집니다. 필라멘트를 따라 은하가 흐르고\n그 사이는 텅 빈 보이드 — 우주의 가장 큰 구조, 코스믹 웹입니다.",
                        "Pull back further and the galaxies join into a spider's web: they stream\nalong filaments, around vast empty voids — the cosmic web, the largest structure there is.",
                        "さらに退くと、銀河はクモの巣のようにつながります。フィラメントに沿って銀河が流れ\nそのあいだは空っぽのボイド — 宇宙最大の構造、コズミックウェブです。",
                        "再往后退，星系连成了蛛网：它们沿着纤维流淌，其间是空荡的巨洞——\n这就是宇宙最大的结构，宇宙网。"));
                }
                yield return null;
            }

            // ---- Deep-field hold --------------------------------------------
            while (!NarrationDone)
            {
                transform.RotateAround(Vector3.zero, Vector3.up, 0.25f * Time.deltaTime);
                yield return null;
            }
            float len4 = Narrate(4);
            Caption(Loc.T(
                "이 화면에서 빛나는 점 하나하나가 — 별이 아니라 은하입니다. 각각 수천억 개의\n별을 품고서요. 관측 가능한 우주에 이런 은하가 약 2조 개 있습니다. 이제 집으로 돌아갑니다.",
                "Every point of light on this screen is not a star but a galaxy, each holding\nhundreds of billions of stars. The observable universe has about two trillion of them. Now — let's go home.",
                "この画面で光る点のひとつひとつが — 星ではなく銀河です。それぞれが数千億の\n星を抱えて。観測可能な宇宙には、こんな銀河が約2兆個あります。さあ、家に帰りましょう。",
                "这屏幕上的每一个光点——都不是恒星，而是星系，每个都拥有数千亿颗恒星。\n可观测宇宙中约有两万亿个这样的星系。现在——我们回家吧。"));
            for (float t = 0f, dur = Mathf.Max(9f, len4 + 1f); t < dur; t += Time.deltaTime)
            {
                // A whisper of drift so the web parallaxes instead of freezing.
                transform.RotateAround(Vector3.zero, Vector3.up, 0.25f * Time.deltaTime);
                yield return null;
            }

            // ---- The flight home --------------------------------------------
            for (float t = 0f; t < returnDuration; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / returnDuration));
                float d = Mathf.Exp(Mathf.Lerp(lnB, lnA, u));
                Ride(cam, dir, d);
                Atmosphere(1f - u, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.95f, u)));
                yield return null;
            }

            // Ends back at the galaxy overview; the ambient orbit takes over.
            Finish();
        }

        /// <summary>One dolly step: position, look, and the clip planes that
        /// have to ride along — a fixed near plane loses all depth precision
        /// at 180,000 kpc, a fixed far plane culls the web entirely.</summary>
        void Ride(Camera cam, Vector3 dir, float d)
        {
            transform.position = dir * d;
            transform.LookAt(Vector3.zero);
            if (cam != null)
            {
                cam.nearClipPlane = Mathf.Clamp(d * 0.002f, 0.02f, 300f);
                cam.farClipPlane = Mathf.Max(600f, d * 8f);
                // The screen-space-camera canvas lives at planeDistance from
                // the lens; once near rides past it the captions and the stop
                // button vanish (verified: gone for 99% of the journey). Keep
                // the canvas just outside the near plane the whole ride.
                if (uiCanvas != null)
                    uiCanvas.planeDistance = Mathf.Max(savedPlaneDistance, cam.nearClipPlane * 2f);
            }
        }

        /// <summary>Depth-dependent grading, driven by log-depth ud (0 = home,
        /// 1 = deep field). Two moves, one lesson each: the LOCAL stars fade
        /// out — at fifty million light-years there is no Milky Way foreground,
        /// so every remaining point of light really is a galaxy — and the
        /// impostor field rides an exposure boost, because energy-conserving
        /// galaxies at that range are physically a few percent of a pixel's
        /// worth of light.</summary>
        void Atmosphere(float ud, float fieldFade)
        {
            field.SetBrightness(fieldFade * Mathf.Lerp(1f, deepExposure, ud * ud));
            if (skyInstance != null)
            {
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.35f, ud));
                if (skyInstance.HasProperty("_StarDensity")) skyInstance.SetFloat("_StarDensity", skyStars * k);
                if (skyInstance.HasProperty("_GalaxyCount")) skyInstance.SetFloat("_GalaxyCount", skyGalaxies * k);
            }
        }

        // ---------------- UI (the shared factory) ---------------------------

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "Web Stop", Abort);
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
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Web Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(940f, 104f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 20, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 88f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            captionPanel.gameObject.SetActive(true);
            caption.text = text;
        }

        void HideCaption()
        {
            if (captionPanel != null) captionPanel.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (IsPlaying) Abort();
        }
    }
}
