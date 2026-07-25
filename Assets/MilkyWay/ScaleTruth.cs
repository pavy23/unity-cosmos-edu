using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect; // Loc, BlackHoleUI, NarrationManager, CinematicOrbit

namespace MilkyWay
{
    /// <summary>
    /// The scale truth (F2 in the solar-system exhibit): every orrery — ours
    /// included — lies about proportions to stay legible, and this experience
    /// confesses interactively. It blends the stage rig from the legibility
    /// mapping to TRUE proportions (SetRealism 0→1) while the camera pulls
    /// back to frame Neptune's real orbit: the planets shrink into invisible
    /// grains, even the Sun collapses toward a point, and the visitor is left
    /// looking at empty space held together by orbit lines. Then it puts the
    /// friendly map back — the lesson keeps, the legibility returns.
    /// </summary>
    public class ScaleTruth : MonoBehaviour
    {
        public SolarSystemRig rig;
        public SolarSystemStage stage;
        public CinematicOrbit orbit;

        [Tooltip("Seconds for the map → truth blend (and half that back).")]
        public float blendDuration = 12f;

        public bool IsPlaying { get; private set; }

        // Stage framing (world units, rig scale ×1000): the display map's
        // Neptune sits at ~60; the TRUE map's at ~331.
        static readonly Vector3 MapPose = new Vector3(0f, 92f, -104f);
        static readonly Vector3 TruthPose = new Vector3(0f, 640f, -720f);

        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        float savedFar;
        Text caption;
        RectTransform captionPanel;
        Button stopButton;

        public static readonly string[] NarrationLines =
        {
            "지금 보는 태양계는 보기 좋게 그린 지도입니다. 행성은 수천 배 크게, 궤도는 실제보다 바짝 당겨 그렸습니다. 진짜 비율은 어떤 모습일까요 — 이제 바꿔 봅니다.",
            "행성들이 모래알이 되어 사라집니다. 태양마저 점이 되어 갑니다. 이것이 진짜 태양계입니다 — 거의 완벽하게 텅 빈 공간. 궤도선이 없다면 여기에 무언가 있다는 것조차 알 수 없습니다.",
            "빛조차 태양에서 해왕성까지 4시간이 걸립니다. 탐사선 보이저 2호는 12년을 날아서야 해왕성을 스쳤습니다. 우주가 넓다는 말은, 사실 이 텅 빈 간격의 이야기입니다. 이제 다시 보기 좋은 지도로 돌아갑니다.",
        };

        public static readonly string[] NarrationLinesEn =
        {
            "The solar system you are looking at is a friendly map: planets drawn thousands of times too large, orbits pulled far closer than they are. What do the true proportions look like? Let's switch.",
            "The planets shrink into grains and vanish. Even the Sun collapses toward a point. This is the real solar system — almost perfectly empty space. Without the orbit lines you could not tell anything is here at all.",
            "Even light needs four hours to travel from the Sun to Neptune; Voyager 2 flew for twelve years before it swept past. When we say space is vast, this emptiness is what we mean. Now — let's bring the friendly map back.",
        };

        public static readonly string[] NarrationLinesJa =
        {
            "いまご覧の太陽系は、見やすく描いた地図です。惑星は数千倍も大きく、軌道は実際よりずっと近くに描かれています。本当の比率はどんな姿でしょう — 切り替えてみましょう。",
            "惑星が砂粒になって消えていきます。太陽さえ点になっていきます。これが本当の太陽系 — ほぼ完全に空っぽの空間です。軌道線がなければ、ここに何かがあることすら分かりません。",
            "光でさえ、太陽から海王星まで4時間かかります。探査機ボイジャー2号は12年飛んで、ようやく海王星をかすめました。宇宙が広いというのは、実はこの空っぽの隔たりの話なのです。さあ、見やすい地図に戻しましょう。",
        };

        public static readonly string[] NarrationLinesZh =
        {
            "您眼前的太阳系是一张便于观看的地图：行星画大了几千倍，轨道也比实际拉近了许多。真实的比例是什么样子？我们切换看看。",
            "行星缩成沙粒消失了，连太阳也塌缩成一个点。这才是真实的太阳系——几乎完全空荡的空间。若没有轨道线，你甚至无法察觉这里有任何东西。",
            "即使是光，从太阳到海王星也要走四个小时；旅行者2号飞了十二年才掠过海王星。我们说宇宙辽阔，说的其实就是这份空旷。现在——把友好的地图换回来吧。",
        };

        public void Begin()
        {
            if (rig == null && stage != null) rig = stage.Rig;
            if (!Application.isPlaying || IsPlaying || rig == null) return;
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
            rig.SetRealism(0f);
            var cam = GetComponent<Camera>();
            if (cam != null) cam.farClipPlane = savedFar;
            transform.position = savedPos;
            transform.rotation = savedRot;
            HideCaption();
            ShowStop(false);
            if (orbit != null) orbit.enabled = true;
            IsPlaying = false;
        }

        static float Narrate(int i) => NarrationManager.Instance.Play("ss_scale_" + i);

        IEnumerator Run()
        {
            IsPlaying = true;
            var cam = GetComponent<Camera>();
            savedPos = transform.position;
            savedRot = transform.rotation;
            savedFar = cam != null ? cam.farClipPlane : 20000f;
            if (cam != null) cam.farClipPlane = Mathf.Max(savedFar, 6000f);
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);
            rig.SetOrbitLinesVisible(true);

            // Beat 0: confess the map, glide to the overview bench.
            float len0 = Narrate(0);
            Caption(Loc.T(NarrationLines[0], NarrationLinesEn[0], NarrationLinesJa[0], NarrationLinesZh[0]));
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            Quaternion mapRot = Quaternion.LookRotation(-MapPose.normalized, Vector3.forward);
            for (float t = 0f, dur = 3f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(fromPos, MapPose, u);
                transform.rotation = Quaternion.Slerp(fromRot, mapRot, u);
                yield return null;
            }
            for (float t = 0f, dur = Mathf.Max(3f, len0 - 3f); t < dur; t += Time.deltaTime)
                yield return null;

            // The blend out: map → truth while the camera pulls back.
            float len1 = Narrate(1);
            Caption(Loc.T(NarrationLines[1], NarrationLinesEn[1], NarrationLinesJa[1], NarrationLinesZh[1]));
            for (float t = 0f; t < blendDuration; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / blendDuration));
                rig.SetRealism(u);
                transform.position = Vector3.Lerp(MapPose, TruthPose, u);
                transform.LookAt(Vector3.zero, Vector3.forward);
                yield return null;
            }
            rig.SetRealism(1f);
            for (float t = 0f, dur = Mathf.Max(4f, len1 - blendDuration + 2f); t < dur; t += Time.deltaTime)
            {
                transform.RotateAround(Vector3.zero, Vector3.up, 0.8f * Time.deltaTime);
                yield return null;
            }

            // The emptiness beat, then the blend home.
            float len2 = Narrate(2);
            Caption(Loc.T(NarrationLines[2], NarrationLinesEn[2], NarrationLinesJa[2], NarrationLinesZh[2]));
            for (float t = 0f, dur = Mathf.Max(6f, len2 - 5f); t < dur; t += Time.deltaTime)
            {
                transform.RotateAround(Vector3.zero, Vector3.up, 0.8f * Time.deltaTime);
                yield return null;
            }
            Vector3 outPos = transform.position;
            for (float t = 0f, dur = blendDuration * 0.5f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                rig.SetRealism(1f - u);
                transform.position = Vector3.Lerp(outPos, MapPose, u);
                transform.LookAt(Vector3.zero, Vector3.forward);
                yield return null;
            }
            Finish();
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

        void ShowStop(bool on)
        {
            // The stop pill and the language button share the top-right
            // corner (both anchored -26,-26): show one, hide the other.
            LanguageSelect.SetVisible(!on);
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "Scale Stop", Abort);
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
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Scale Caption",
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
