using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The Sagittarius A* crossover (F9): the bridge between the two exhibits.
    /// A log dolly dives from the galaxy overview into the bulge core — three
    /// decades of scale, stars crowding, exposure riding down against the
    /// blinding centre — while the narration sets up the hidden four-million-
    /// solar-mass point. Visible light ends at the dust, the screen fades to
    /// black, and the visitor lands in the BLACK HOLE EXHIBIT itself
    /// (BlackHoleShowcase), where F9 leads back. Two scenes, one universe.
    ///
    /// Scene-transition discipline: the ride mutates controller exposure,
    /// which writes into the SHARED .mat assets — everything is restored to
    /// its saved values BEFORE LoadScene, or the drift outlives play mode
    /// (the StarfieldSkybox lesson).
    /// </summary>
    public class SgrACrossover : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Seconds for the dive from overview to the bulge core.")]
        public float diveDuration = 30f;

        public bool IsPlaying { get; private set; }

        const float DStart = 34f, DEnd = 0.05f;
        const string TargetScene = "BlackHoleShowcase";

        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedNear, savedFar;
        float savedBrightness, savedStarBrightness;
        Image fade;
        Text caption;
        RectTransform captionPanel;
        Button stopButton;

        public static readonly string[] NarrationLines =
        {
            "이제 은하의 심장으로 들어갑니다. 나선팔을 지나고 막대를 지나면 별들이 점점 붐빕니다 — 은하 중심 근처는 태양 주변보다 별이 수백만 배나 빽빽한 곳입니다.",
            "수십 년 동안 천문학자들은 중심의 별들이 보이지 않는 무언가를 무서운 속도로 도는 것을 지켜봤습니다 — 초속 수천 킬로미터. 그 궤도들이 가리키는 한 점에, 태양 4백만 배의 질량이 숨어 있습니다.",
            "가시광으로는 여기까지입니다 — 먼지가 중심을 가리고 있으니까요. 전파 망원경들이 마침내 그 그림자를 찍어냈습니다. 궁수자리 A 스타 — 우리 은하의 블랙홀입니다. 이제, 그곳으로 갑니다.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "Now we dive into the heart of the galaxy. Past the arms, past the bar, the stars crowd tighter and tighter — near the centre they are packed millions of times denser than around the Sun.",
            "For decades astronomers watched the innermost stars whip around something unseen at thousands of kilometres per second. At the one point every orbit agrees on, four million solar masses lie hidden.",
            "Visible light ends here — dust curtains the centre. Radio telescopes finally photographed the shadow itself: Sagittarius A star, our galaxy's black hole. Now — let's go there.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "いよいよ銀河の心臓部へ入ります。腕を過ぎ、棒を過ぎると、星はどんどん密になります — 銀河中心の近くでは、太陽のまわりの数百万倍も星が詰まっています。",
            "何十年ものあいだ、天文学者たちは中心の星々が見えない何かのまわりを恐ろしい速さで回るのを見つめてきました — 秒速数千キロ。すべての軌道が指し示すその一点に、太陽の400万倍の質量が隠れています。",
            "可視光で見えるのはここまでです — 塵が中心を覆い隠しているからです。電波望遠鏡がついにその影を撮影しました。いて座Aスター — 私たちの銀河のブラックホールです。さあ、そこへ行きましょう。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "现在，我们潜入银河的心脏。越过旋臂，越过棒，恒星越来越拥挤——银心附近的恒星密度是太阳周围的数百万倍。",
            "几十年来，天文学家看着最内侧的恒星以每秒数千公里的速度绕着某个看不见的东西疾驰。在所有轨道共同指向的那一点上，藏着四百万倍太阳的质量。",
            "可见光到此为止——尘埃遮住了中心。射电望远镜终于拍下了那道阴影本身：人马座A星——我们银河系的黑洞。现在，我们就去那里。",
        };

        public void Begin()
        {
            if (!Application.isPlaying || IsPlaying || controller == null) return;
            routine = StartCoroutine(Run());
        }

        public void Abort()
        {
            if (!IsPlaying) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            RestoreAll();
            IsPlaying = false;
        }

        void RestoreAll()
        {
            var cam = GetComponent<Camera>();
            if (cam != null) { cam.nearClipPlane = savedNear; cam.farClipPlane = savedFar; }
            controller.brightness = savedBrightness;
            controller.starBrightness = savedStarBrightness;
            controller.Apply();
            transform.position = savedPos;
            transform.rotation = savedRot;
            if (fade != null) fade.gameObject.SetActive(false);
            HideCaption();
            ShowStop(false);
            if (orbit != null) orbit.enabled = true;
        }

        // A beat may only fire once its predecessor's voice has finished —
        // thresholds alone cut the longer (ja, ko) lines mid-sentence.
        float narrEnd;
        bool NarrationDone => Time.time >= narrEnd;
        float Narrate(int i)
        {
            float len = NarrationManager.Instance.Play("mw_sgr_" + i);
            narrEnd = Time.time + len + 0.4f;
            return len;
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            var cam = GetComponent<Camera>();
            savedPos = transform.position;
            savedRot = transform.rotation;
            savedNear = cam != null ? cam.nearClipPlane : 0.02f;
            savedFar = cam != null ? cam.farClipPlane : 600f;
            savedBrightness = controller.brightness;
            savedStarBrightness = controller.starBrightness;
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);

            // Dive along whatever azimuth the visitor is at — the centre is
            // the destination, the direction is theirs.
            Vector3 dir = transform.position.sqrMagnitude > 1e-4f
                ? transform.position.normalized
                : new Vector3(0f, 0.47f, -0.88f);

            // Stage 0: glide onto the departure ray.
            float len0 = Narrate(0);
            Caption(Loc.T(NarrationLines[0], NarrationLinesEn[0], NarrationLinesJa[0], NarrationLinesZh[0]));
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            for (float t = 0f, dur = 2.6f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(fromPos, dir * DStart, u);
                transform.rotation = Quaternion.Slerp(fromRot, Quaternion.LookRotation(-dir, Vector3.up), u);
                yield return null;
            }

            // The dive: three decades of log dolly into the bulge core, the
            // exposure riding DOWN so the crowding core stays readable
            // instead of blowing out.
            float lnA = Mathf.Log(DStart), lnB = Mathf.Log(DEnd);
            int stage = 0;
            for (float t = 0f; t < diveDuration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / diveDuration);
                float d = Mathf.Exp(Mathf.Lerp(lnA, lnB, u));
                transform.position = dir * d;
                transform.LookAt(Vector3.zero);
                if (cam != null) cam.nearClipPlane = Mathf.Max(d * 0.002f, 0.0006f);

                controller.brightness = Mathf.Lerp(savedBrightness, 0.9f, Mathf.SmoothStep(0f, 1f, u));
                controller.starBrightness = Mathf.Lerp(savedStarBrightness, 0.7f, u);
                controller.Apply();

                if (stage == 0 && u > 0.52f && NarrationDone)
                {
                    stage = 1; Narrate(1);
                    Caption(Loc.T(NarrationLines[1], NarrationLinesEn[1], NarrationLinesJa[1], NarrationLinesZh[1]));
                }
                yield return null;
            }

            // Final beat at the core: the dust wall, the radio image, the exit.
            while (!NarrationDone)
            {
                transform.RotateAround(Vector3.zero, Vector3.up, 1.2f * Time.deltaTime);
                yield return null;
            }
            float len2 = Narrate(2);
            Caption(Loc.T(NarrationLines[2], NarrationLinesEn[2], NarrationLinesJa[2], NarrationLinesZh[2]));
            for (float t = 0f, dur = Mathf.Max(6f, len2 - 1.5f); t < dur; t += Time.deltaTime)
            {
                transform.RotateAround(Vector3.zero, Vector3.up, 1.2f * Time.deltaTime);
                yield return null;
            }

            // Fade to black, put every shared asset back, and cross over.
            EnsureFade();
            fade.gameObject.SetActive(true);
            for (float t = 0f; t < 1.8f; t += Time.deltaTime)
            {
                fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / 1.8f));
                yield return null;
            }
            fade.color = Color.black;

            controller.brightness = savedBrightness;
            controller.starBrightness = savedStarBrightness;
            controller.Apply();
            IsPlaying = false;
            SceneManager.LoadScene(TargetScene);
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

        // ---------------- UI (the shared factory) ---------------------------

        void EnsureFade()
        {
            if (fade != null) return;
            fade = BlackHoleUI.MakeFullViewOverlay(GetComponent<Camera>(), "SgrA Fade");
            fade.color = new Color(0f, 0f, 0f, 0f);
        }

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "SgrA Stop", Abort);
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
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "SgrA Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(940f, 104f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 19, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 92f));
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
