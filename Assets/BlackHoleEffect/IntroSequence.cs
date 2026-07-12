using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// Birth-of-a-black-hole intro (F5): a star burns, swells into a red
    /// giant, shudders, collapses, detonates as a supernova (flash + double
    /// shock shell), and the black hole fades in where the star used to be.
    /// Narrated — each phase waits for its narration clip, so captions and
    /// voice stay in sync. Play mode only.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        public Renderer holeRenderer;
        public BlackHoleController controller;
        public DesktopControls controls;

        [Tooltip("Play the birth-of-a-black-hole intro automatically when play mode starts (once per session; F5 replays it).")]
        public bool autoPlayOnStart = true;

        public bool IsPlaying { get; private set; }

        Text caption;
        RectTransform captionPanel;
        Image flash;
        Button skipButton;
        Coroutine routine;
        float savedBrightness = 9.5f;

        // Captions double as the narration transcript — the TTS clips
        // (Resources/Narration/intro_* and Narration/en/intro_*) are generated
        // from these exact lines. Keep code and audio in sync.
        public static readonly string[] Lines =
        {
            "어느 별의 마지막 날입니다. 수백만 년 동안 빛나던 별이, 이제 연료를 거의 다 써 갑니다.",
            "연료가 바닥난 별은 붉게 부풀어 올라, 거대한 적색거성이 됩니다.",
            "더 이상 자신의 무게를 버틸 수 없게 된 중심핵이, 순식간에 무너져 내립니다.",
            "초신성 폭발! 별의 바깥층이 우주 저편으로 흩어집니다.",
            "그리고 그 자리에 남은 것은... 빛조차 빠져나올 수 없는, 블랙홀입니다.",
        };

        public static readonly string[] LinesEn =
        {
            "This is the final day of a star. After shining for millions of years, it is running out of fuel.",
            "With its fuel exhausted, the star swells up red, becoming a vast red giant.",
            "No longer able to hold up its own weight, the core collapses in an instant.",
            "Supernova! The star's outer layers are hurled into deep space.",
            "And what remains behind... is a black hole, from which not even light can escape.",
        };

        public static readonly string[] LinesJa =
        {
            "ある星の最後の日です。何百万年も輝いてきた星が、いま燃料を使い果たそうとしています。",
            "燃料が尽きた星は赤くふくらみ、巨大な赤色巨星になります。",
            "自らの重さを支えきれなくなった中心核が、一瞬で崩れ落ちます。",
            "超新星爆発！星の外層が宇宙のかなたへ吹き飛ばされます。",
            "そして残されたのは……光さえ逃げられない、ブラックホールです。",
        };

        public static readonly string[] LinesZh =
        {
            "这是一颗恒星的最后一天。闪耀了数百万年的星，燃料即将耗尽。",
            "燃料耗尽后，恒星膨胀发红，成为巨大的红巨星。",
            "再也支撑不住自身重量的核心，在一瞬间坍缩。",
            "超新星爆发！恒星的外层被抛向宇宙深处。",
            "最后留下的……是一个连光都无法逃脱的黑洞。",
        };

        void Start()
        {
            if (autoPlayOnStart && Application.isPlaying)
                StartCoroutine(AutoPlay());
        }

        IEnumerator AutoPlay()
        {
            // A short beat so the scene (canvas, narration, audio) settles in.
            yield return new WaitForSeconds(0.8f);
            // If the user already launched something in that first moment
            // (F6/F7/tour), don't hijack their experience.
            if (controls != null && controls.CinematicBusy) yield break;
            Play();
        }

        void Update()
        {
            if (!IsPlaying) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Skip();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Skip();
#endif
        }

        public void Play()
        {
            if (!Application.isPlaying || IsPlaying) return;
            routine = StartCoroutine(Run());
        }

        /// <summary>Aborts the intro (skip button / Esc): kills the coroutine,
        /// sweeps every stage prop and restores the exploration view.</summary>
        public void Skip()
        {
            if (!IsPlaying) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            Sweep("Doomed Star");
            Sweep("Supernova Shell");
            if (flash != null) flash.color = Color.clear;
            if (holeRenderer != null) holeRenderer.enabled = true;
            if (controller != null)
            {
                controller.diskBrightness = savedBrightness;
                controller.Apply();
            }
            Finish();
        }

        void Finish()
        {
            HideCaption();
            ShowSkip(false);
            if (controls != null) controls.SetImmersive(false);
            IsPlaying = false;
        }

        void ShowSkip(bool on)
        {
            if (skipButton == null)
            {
                if (!on) return;
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                skipButton = BlackHoleUI.MakeButton(canvas.transform, "Intro Skip", "",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(170f, 44f), Skip);
            }
            skipButton.gameObject.SetActive(on);
            if (on)
                skipButton.GetComponentInChildren<Text>().text =
                    Loc.T("건너뛰기 ▸", "Skip ▸", "スキップ ▸", "跳过 ▸");
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            if (controls != null) controls.SetImmersive(true);
            ShowSkip(true);

            float baseBrightness = controller != null ? controller.diskBrightness : 9.5f;
            savedBrightness = baseBrightness;
            Vector3 center = holeRenderer != null ? holeRenderer.transform.position : Vector3.zero;
            if (holeRenderer != null) holeRenderer.enabled = false;

            // Sweep leftovers from an interrupted earlier run (HideAndDontSave
            // objects used to survive play-mode exit and haunt the next run).
            Sweep("Doomed Star");
            Sweep("Supernova Shell");

            // --- the doomed star -------------------------------------------------
            // Plain runtime objects: destroyed automatically when play stops.
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "Doomed Star";
            Destroy(star.GetComponent<Collider>());
            star.transform.position = center;
            var starMat = new Material(Shader.Find("BlackHole/StarSurface"));
            star.GetComponent<MeshRenderer>().sharedMaterial = starMat;

            // Phase 1 — quiet burning: tight granulation, gentle pulse.
            float dur = Narrate(0, 6f);
            starMat.SetColor("_StarColor", new Color(2.6f, 2.3f, 1.7f));
            starMat.SetFloat("_Granulation", 0.3f);
            starMat.SetFloat("_GranScale", 7f);
            starMat.SetFloat("_SpotStrength", 0.25f);
            starMat.SetFloat("_CoronaBoost", 0.7f);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float pulse = 1.1f + 0.04f * Mathf.Sin(t * 2.6f);
                star.transform.localScale = Vector3.one * pulse;
                star.transform.Rotate(0f, 4f * Time.deltaTime, 0f);
                yield return null;
            }

            // Phase 2 — red giant: cooler, bigger convection cells, irregular
            // breathing like a real M-type giant, corona flaring up.
            dur = Narrate(1, 5f);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(t / Mathf.Min(dur, 5.5f), 1f));
                float wobble = 1f + 0.05f * Mathf.Sin(t * 5.3f) + 0.035f * Mathf.Sin(t * 2.1f + 1.7f);
                star.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 2.9f, k) * wobble;
                star.transform.Rotate(0f, 2.5f * Time.deltaTime, 0f);
                starMat.SetColor("_StarColor", Color.Lerp(new Color(2.6f, 2.3f, 1.7f), new Color(2.9f, 0.62f, 0.18f), k));
                starMat.SetFloat("_Granulation", Mathf.Lerp(0.3f, 0.7f, k));
                starMat.SetFloat("_GranScale", Mathf.Lerp(7f, 3.2f, k)); // giant convection cells
                starMat.SetFloat("_SpotStrength", Mathf.Lerp(0.25f, 0.5f, k));
                starMat.SetFloat("_CoronaBoost", Mathf.Lerp(0.7f, 1.6f, k));
                yield return null;
            }

            // Phase 3 — the star shudders while the narration builds tension,
            // then the core lets go in 1.2 s.
            dur = Narrate(2, 4f);
            float shudder = Mathf.Max(dur - 1.2f, 0.8f);
            Vector3 giantScale = star.transform.localScale;
            for (float t = 0f; t < shudder; t += Time.deltaTime)
            {
                float k = t / shudder;
                float tremor = 1f + (0.02f + 0.05f * k) * Mathf.Sin(t * (18f + 30f * k));
                star.transform.localScale = giantScale * Mathf.Lerp(1f, 0.94f, k) * tremor;
                yield return null;
            }
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float k = Mathf.Pow(t / 1.2f, 3f);
                star.transform.localScale = giantScale * Mathf.Lerp(0.94f, 0.014f, k);
                yield return null;
            }

            // Phase 4 — supernova: screen flash + fast blue shock front
            // + slower glowing ejecta shell.
            dur = Mathf.Max(Narrate(3, 3f), 2.8f);
            EnsureFlash();
            var shell = MakeShell(center, out var shellMat);
            var shock = MakeShell(center, out var shockMat);
            Destroy(star);

            float ejectaTime = Mathf.Max(2.8f, dur * 0.92f); // keep glowing while the voice speaks
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.Min(t / ejectaTime, 1f);
                // Ejecta: warm, decelerating.
                shell.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 16f, Mathf.Pow(k, 0.6f));
                shellMat.SetColor("_Tint", new Color(3.2f, 2.6f, 2.0f, Mathf.Pow(1f - k, 1.6f)));
                // Shock front: thin, blue, faster, fades sooner.
                float ks = Mathf.Min(t / 1.7f, 1f);
                shock.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 22f, Mathf.Pow(ks, 0.55f));
                shockMat.SetColor("_Tint", new Color(1.6f, 2.4f, 3.6f, 0.55f * Mathf.Pow(1f - ks, 2.2f)));
                // Retina flash right at detonation.
                if (flash != null)
                    flash.color = new Color(1f, 0.97f, 0.9f, 0.85f * Mathf.Clamp01(1f - t / 0.45f));
                yield return null;
            }
            if (flash != null) flash.color = Color.clear;
            Destroy(shell);
            Destroy(shock);

            // Phase 5 — the black hole fades in.
            dur = Mathf.Max(Narrate(4, 4f), 4f);
            if (holeRenderer != null) holeRenderer.enabled = true;
            if (controller != null)
            {
                for (float t = 0f; t < dur; t += Time.deltaTime)
                {
                    controller.diskBrightness = Mathf.Lerp(0f, baseBrightness, Mathf.Min(t / 4f, 1f));
                    controller.Apply();
                    yield return null;
                }
                controller.diskBrightness = baseBrightness;
                controller.Apply();
            }
            yield return new WaitForSeconds(2f);

            Finish();
        }

        /// <summary>Shows the caption for phase i, starts its narration clip,
        /// and returns how long the phase should hold (clip length + a beat,
        /// never shorter than the animation's minimum).</summary>
        float Narrate(int i, float minSeconds)
        {
            Caption(Loc.T(Lines[i], LinesEn[i], LinesJa[i], LinesZh[i]));
            float len = NarrationManager.Instance.Play("intro_" + i);
            return Mathf.Max(minSeconds, len + 0.4f);
        }

        GameObject MakeShell(Vector3 center, out Material mat)
        {
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Supernova Shell";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.position = center;
            mat = new Material(Shader.Find("BlackHole/JetParticle"));
            shell.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return shell;
        }

        /// <summary>Destroys stray hidden objects left behind by runs that
        /// were interrupted before their cleanup (e.g. play mode stopped
        /// while paused mid-intro).</summary>
        static void Sweep(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.name == name
                    && ((go.hideFlags & HideFlags.HideAndDontSave) != 0 || go.scene.IsValid()))
                    DestroyImmediate(go);
        }

        void EnsureFlash()
        {
            if (flash != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
            var go = new GameObject("Supernova Flash") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            flash = go.AddComponent<Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;
        }

        void Caption(string text)
        {
            if (caption == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Intro Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(860f, 110f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 22, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 90f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap; // long lines stay inside the panel
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
