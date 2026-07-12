using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// First-person fall into the event horizon (F6): the camera free-falls
    /// toward the hole while the shadow swells to fill the sky. Looking
    /// forward everything ends black — but the epilogue turns the view
    /// around: the outside universe stays visible, gathered into an
    /// ever-shrinking, blueshifting circle (aberration for an infalling
    /// observer). A stop button / Esc aborts at any time. Play mode only.
    /// </summary>
    public class FallInMode : MonoBehaviour
    {
        public Transform hole;
        public DesktopControls controls;
        public CinematicOrbit orbit;
        public float fallDuration = 14f;

        public bool IsFalling { get; private set; }

        Text caption;
        RectTransform captionPanel;
        Button stopButton;
        Coroutine routine;
        Vector3 savedPos;
        Quaternion savedRot;
        Image skyImage;
        Texture2D skyTex;

        public void Begin()
        {
            if (!Application.isPlaying || IsFalling || hole == null) return;
            routine = StartCoroutine(Run());
        }

        void Update()
        {
            if (!IsFalling) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Abort();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Abort();
#endif
        }

        /// <summary>Stop button / Esc: kill the coroutine and put the camera
        /// (and every overlay) back exactly where the fall started.</summary>
        public void Abort()
        {
            if (!IsFalling) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            DestroySkyDisk();
            transform.position = savedPos;
            transform.rotation = savedRot;
            Finish();
        }

        void Finish()
        {
            HideCaption();
            ShowStop(false);
            if (controls != null) { controls.SetImmersive(false); controls.suspendCamera = false; }
            if (orbit != null) orbit.enabled = true; // the drift never stays off
            IsFalling = false;
        }

        /// <summary>Moves the camera from rFrom to rTo (in Rs) over dur
        /// seconds with the given easing exponent, keeping the live r
        /// readout in the caption via the formatter.</summary>
        /// <summary>Plays fall_{i} in the active language; returns clip length.</summary>
        static float Narrate(int i) => NarrationManager.Instance.Play("fall_" + i);

        /// <summary>Unspoken gray live-distance line under the caption.</summary>
        static string RLine(float r) => "\n<color=#9AA3B5>r = " + r.ToString("0.0") + " Rs</color>";

        /// <summary>tiltFrom/tiltTo: upward camera pitch in degrees — near the
        /// horizon the only remaining light is overhead, so looking straight
        /// at the hole would show nothing but black for many seconds.</summary>
        IEnumerator Glide(float rFrom, float rTo, float dur, float ease,
                          Vector3 dir, float rs, System.Func<float, string> captionFor,
                          float tiltFrom = 0f, float tiltTo = 0f)
        {
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / dur);
                float r = Mathf.Lerp(rFrom, rTo, Mathf.Pow(u, ease));
                transform.position = hole.position + dir * (r * rs);
                transform.LookAt(hole.position);
                float tilt = Mathf.Lerp(tiltFrom, tiltTo, Mathf.SmoothStep(0f, 1f, u));
                if (tilt != 0f) transform.Rotate(-tilt, 0f, 0f);
                if (captionFor != null) Caption(captionFor(r));
                yield return null;
            }
        }

        IEnumerator Run()
        {
            IsFalling = true;
            if (controls != null) { controls.SetImmersive(true); controls.suspendCamera = true; }
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);

            savedPos = transform.position;
            savedRot = transform.rotation;
            float rs = hole.lossyScale.x;
            Vector3 dir = (savedPos - hole.position).normalized;
            float r0 = (savedPos - hole.position).magnitude / rs;
            float len;

            // --- Framing: say what this experience is about before moving.
            // Captions are the narration transcript (fall_0..fall_7); the
            // live r readout is an unspoken gray line, like the tour hints.
            len = Narrate(0);
            Caption(Loc.T(
                "만약 블랙홀 안으로 떨어진다면, 무엇을 보게 될까요?\n지금부터 카메라가 자유낙하를 시작합니다.",
                "What would you see if you fell into a black hole?\nThe camera now begins its free fall.",
                "もしブラックホールに落ちたら、何が見えるのでしょうか。\nこれからカメラが自由落下を始めます。",
                "如果掉进黑洞，你会看到什么？\n现在，镜头开始自由落体。"));
            yield return new WaitForSeconds(Mathf.Max(3.4f, len + 0.5f));

            // --- Staged descent: each stage owns one narrated line, and the
            // stage lasts at least as long as its voice clip.
            float mid = Mathf.Clamp(r0 * 0.55f, 4.6f, 8f);
            len = Narrate(1);
            yield return Glide(r0, mid, Mathf.Max(4.5f, len + 0.3f), 1.5f, dir, rs, r => Loc.T(
                "자유낙하가 시작됩니다. 아직은 평범한 우주입니다.",
                "Free fall begins. Space still looks ordinary out here.",
                "自由落下が始まります。まだ、ふつうの宇宙です。",
                "自由落体开始了。这里还是平常的宇宙。") + RLine(r));

            len = Narrate(2);
            yield return Glide(mid, 4.5f, Mathf.Max(3.5f, len + 0.3f), 1.15f, dir, rs, r => Loc.T(
                "원반이 하늘을 뒤덮기 시작합니다. 밖의 시간은 점점 빨라 보입니다.",
                "The disk begins to swallow the sky. Time outside appears to run faster and faster.",
                "円盤が空を覆いはじめます。外の時間はどんどん速く見えます。",
                "吸积盘开始遮蔽天空。外面的时间看起来越来越快。") + RLine(r));

            len = Narrate(3);
            yield return Glide(4.5f, 1.9f, Mathf.Max(3.8f, len + 0.3f), 1f, dir, rs, r => Loc.T(
                "조석력이 몸을 잡아 늘입니다. 그림자가 시야의 절반을 삼켰습니다.",
                "Tidal forces stretch your body. The shadow has swallowed half your view.",
                "潮汐力が体を引き伸ばします。影が視界の半分を呑み込みました。",
                "潮汐力开始拉伸你的身体。阴影已吞没一半视野。") + RLine(r));

            // The camera tilts upward here: the caption says the last light
            // closes in OVERHEAD, so we look up and actually show it instead
            // of staring into an already-black hole for six seconds.
            len = Narrate(4);
            yield return Glide(1.9f, 1.05f, Mathf.Max(3.2f, len + 0.3f), 0.9f, dir, rs, r => Loc.T(
                "마지막 빛의 고리가 머리 위로 좁혀듭니다.",
                "The last ring of light closes in overhead.",
                "最後の光のリングが頭上で狭まっていきます。",
                "最后的光环在头顶收拢。") + RLine(r), 0f, 40f);
            yield return new WaitForSeconds(1.6f); // hold at the brink

            len = Narrate(5);
            Caption(Loc.T(
                "사건의 지평선을 통과했습니다.\n바깥 우주로는 어떤 신호도 보낼 수 없습니다.",
                "We have crossed the event horizon.\nNo signal can ever reach the outside universe again.",
                "事象の地平面を通過しました。\n外の宇宙へは、もうどんな信号も送れません。",
                "我们已越过事件视界。\n再也无法向外面的宇宙发出任何信号。"));
            yield return Glide(1.05f, 0.35f, 1.6f, 1f, dir, rs, null, 40f, 60f);
            // A short beat of true darkness — then the looking-back circle
            // rises while the horizon line finishes, so pure black never
            // outstays its welcome.
            yield return new WaitForSeconds(2.4f);
            EnsureSkyImage();
            skyImage.gameObject.SetActive(true);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = t / 1.1f;
                float size = Mathf.Lerp(30f, 640f, Mathf.SmoothStep(0f, 1f, k));
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                skyImage.color = new Color(1f, 0.98f, 0.92f, k);
                yield return null;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, len - 1.6f - 2.4f - 1.1f));

            // --- Epilogue: turn around. The view "forward" (toward the
            // singularity) really is black — but looking back, the outside
            // universe never disappears: aberration gathers the whole sky
            // into a shrinking, blueshifting circle of light.
            len = Narrate(6);
            Caption(Loc.T(
                "뒤를 돌아보면, 바깥 우주는 사라지지 않습니다.\n온 하늘이 점점 좁아지는 푸른 빛의 원 안으로 모여듭니다.",
                "Looking back, the outside universe never vanishes.\nThe whole sky gathers into a shrinking, blueshifting circle of light.",
                "後ろを振り返ると、外の宇宙は消えていません。\n空全体が、狭まっていく青い光の円の中に集まって見えます。",
                "回头看，外面的宇宙并没有消失。\n整个天空聚成一个越来越小、越来越蓝的光圈。"));
            float lookBack = Mathf.Max(7f, len + 0.4f);
            for (float t = 0f; t < lookBack; t += Time.deltaTime)
            {
                float k = t / lookBack;
                float size = Mathf.Lerp(640f, 70f, Mathf.Pow(k, 1.2f));
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                skyImage.color = Color.Lerp(new Color(1f, 0.98f, 0.92f), new Color(0.45f, 0.65f, 1f), k);
                yield return null;
            }

            // The last point of light dies while physics has its final word —
            // the closing line is never spoken over a totally empty screen.
            len = Narrate(7);
            Caption(Loc.T(
                "이 안에서는 모든 미래의 경로가 중심 특이점을 향합니다.\n여기까지가 물리학이 말할 수 있는 전부입니다.",
                "In here, every future path leads to the central singularity.\nThis is as far as physics can speak.",
                "この中では、あらゆる未来の経路が中心の特異点へ向かいます。\nここから先は、物理学が語れる限界です。",
                "在这里，所有未来的路径都通向中心奇点。\n物理学能讲述的，到此为止。"));
            float die = Mathf.Max(4.5f, len + 0.5f);
            for (float t = 0f; t < die; t += Time.deltaTime)
            {
                float k = t / die;
                float size = Mathf.Lerp(70f, 12f, k);
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                Color c = Color.Lerp(new Color(0.45f, 0.65f, 1f), new Color(0.25f, 0.35f, 0.85f), k);
                c.a = Mathf.Pow(1f - k, 1.3f);
                skyImage.color = c;
                yield return null;
            }
            skyImage.gameObject.SetActive(false);

            Caption(Loc.T(
                "체험이 끝났습니다 — 원래 위치로 돌아갑니다.",
                "The experience is over — returning to where we started.",
                "体験はこれで終わりです — 元の位置に戻ります。",
                "体验结束——回到原来的位置。"));
            yield return new WaitForSeconds(1.8f);

            transform.position = savedPos;
            transform.rotation = savedRot;
            Finish();
        }

        // ---------------- looking-back sky circle ----------------
        void EnsureSkyImage()
        {
            if (skyImage != null) return;
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
            var go = new GameObject("FallIn Sky") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(canvas.transform, false);
            // Behind the caption/buttons (last siblings draw on top anyway
            // because the caption panel was created earlier — keep this first).
            go.transform.SetSiblingIndex(0);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            skyImage = go.AddComponent<Image>();
            skyImage.raycastTarget = false;
            var tex = SkyTexture();
            skyImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        void DestroySkyDisk()
        {
            if (skyImage != null) skyImage.gameObject.SetActive(false);
        }

        /// <summary>Soft "compressed starry sky" disk: a bright core with a
        /// speckle of stars, dying out well inside the quad.</summary>
        Texture2D SkyTexture()
        {
            if (skyTex != null) return skyTex;
            const int dim = 128;
            skyTex = new Texture2D(dim, dim, TextureFormat.RGBA32, false);
            float c = (dim - 1) * 0.5f;
            var rng = new System.Random(7);
            for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float core = Mathf.Exp(-d * d * 3.2f);
                float rim = Mathf.Exp(-Mathf.Abs(d - 0.72f) * 14f) * 0.5f; // bright compressed rim
                float star = (float)rng.NextDouble() < 0.02f && d < 0.75f ? 0.65f : 0f;
                float a = Mathf.Clamp01((core + rim + star) * Mathf.Clamp01((0.85f - d) * 8f));
                skyTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            skyTex.Apply();
            return skyTex;
        }

        // ---------------- UI ----------------
        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                stopButton = BlackHoleUI.MakeButton(canvas.transform, "FallIn Stop", "",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(170f, 44f), Abort);
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
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "FallIn Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(860f, 100f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 21, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 84f));
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
