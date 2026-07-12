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
        IEnumerator Glide(float rFrom, float rTo, float dur, float ease,
                          Vector3 dir, float rs, System.Func<float, string> captionFor)
        {
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.Pow(Mathf.Clamp01(t / dur), ease);
                float r = Mathf.Lerp(rFrom, rTo, k);
                transform.position = hole.position + dir * (r * rs);
                transform.LookAt(hole.position);
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

            // --- Framing: say what this experience is about before moving.
            Caption(Loc.T(
                "만약 블랙홀 안으로 떨어진다면, 무엇을 보게 될까요?\n지금부터 카메라가 자유낙하를 시작합니다.",
                "What would you see if you fell into a black hole?\nThe camera now begins its free fall.",
                "もしブラックホールに落ちたら、何が見えるのでしょうか。\nこれからカメラが自由落下を始めます。",
                "如果掉进黑洞，你会看到什么？\n现在，镜头开始自由落体。"));
            yield return new WaitForSeconds(3.4f);

            // --- Staged descent: each caption owns its stage, so the text
            // and the visuals can never drift apart.
            yield return Glide(r0, 4.5f, fallDuration * 0.5f, 1.7f, dir, rs, r =>
            {
                string rTxt = r.ToString("0.0");
                return r > 8f
                ? Loc.T(
                    "자유낙하 시작 —  r = " + rTxt + " Rs\n아직은 평범한 우주입니다.",
                    "Free fall begins —  r = " + rTxt + " Rs\nSpace still looks ordinary out here.",
                    "自由落下開始 —  r = " + rTxt + " Rs\nまだ、ふつうの宇宙です。",
                    "自由落体开始 —  r = " + rTxt + " Rs\n这里还是平常的宇宙。")
                : Loc.T(
                    "r = " + rTxt + " Rs —  원반이 하늘을 뒤덮기 시작합니다.\n밖의 시간은 점점 빨라 보입니다.",
                    "r = " + rTxt + " Rs —  the disk begins to swallow the sky.\nTime outside appears to run faster and faster.",
                    "r = " + rTxt + " Rs —  円盤が空を覆いはじめます。\n外の時間はどんどん速く見えます。",
                    "r = " + rTxt + " Rs —  吸积盘开始遮蔽天空。\n外面的时间看起来越来越快。");
            });

            yield return Glide(4.5f, 1.9f, fallDuration * 0.27f, 1f, dir, rs, r => { string rTxt = r.ToString("0.0"); return Loc.T(
                "r = " + rTxt + " Rs —  조석력이 몸을 잡아 늘입니다.\n그림자가 시야의 절반을 삼켰습니다.",
                "r = " + rTxt + " Rs —  tidal forces stretch your body.\nThe shadow has swallowed half your view.",
                "r = " + rTxt + " Rs —  潮汐力が体を引き伸ばします。\n影が視界の半分を呑み込みました。",
                "r = " + rTxt + " Rs —  潮汐力开始拉伸你的身体。\n阴影已吞没一半视野。"); });

            yield return Glide(1.9f, 1.05f, fallDuration * 0.23f, 0.9f, dir, rs, r => { string rTxt = r.ToString("0.0"); return Loc.T(
                "r = " + rTxt + " Rs —  마지막 빛의 고리가 머리 위로 좁혀듭니다.",
                "r = " + rTxt + " Rs —  the last ring of light closes in overhead.",
                "r = " + rTxt + " Rs —  最後の光のリングが頭上で狭まっていきます。",
                "r = " + rTxt + " Rs —  最后的光环在头顶收拢。"); });
            yield return new WaitForSeconds(1.6f); // hold at the brink

            Caption(Loc.T(
                "사건의 지평선 통과.\n바깥 우주로는 어떤 신호도 보낼 수 없습니다.",
                "Event horizon crossed.\nNo signal can ever reach the outside universe again.",
                "事象の地平面を通過。\n外の宇宙へは、もうどんな信号も送れません。",
                "已越过事件视界。\n再也无法向外面的宇宙发出任何信号。"));
            yield return Glide(1.05f, 0.35f, 1.6f, 1f, dir, rs, null);
            yield return new WaitForSeconds(2.6f); // let the darkness land

            // --- Epilogue: turn around. The view "forward" (toward the
            // singularity) really is black — but looking back, the outside
            // universe never disappears: aberration gathers the whole sky
            // into a shrinking, blueshifting circle of light.
            Caption(Loc.T(
                "뒤를 돌아보면 — 바깥 우주는 사라지지 않습니다.\n온 하늘이 점점 좁아지는 푸른 빛의 원 안으로 모여듭니다.",
                "Looking back — the outside universe never vanishes.\nThe whole sky gathers into a shrinking, blueshifting circle of light.",
                "後ろを振り返ると — 外の宇宙は消えていません。\n空全体が、狭まっていく青い光の円の中に集まって見えます。",
                "回头看——外面的宇宙并没有消失。\n整个天空聚成一个越来越小、越来越蓝的光圈。"));
            // Drawn as a screen-space image: the camera sits inside the
            // raymarch quad here, so world-space props would be occluded.
            EnsureSkyImage();
            skyImage.gameObject.SetActive(true);
            const float lookBack = 7f;
            for (float t = 0f; t < lookBack; t += Time.deltaTime)
            {
                float k = t / lookBack;
                float size = Mathf.Lerp(640f, 16f, Mathf.Pow(k, 1.35f));
                skyImage.rectTransform.sizeDelta = new Vector2(size, size);
                // White → blueshifted, fading out only at the very end.
                Color c = Color.Lerp(new Color(1f, 0.98f, 0.92f), new Color(0.45f, 0.65f, 1f), k);
                c.a = Mathf.Clamp01((1f - k) * 8f);
                skyImage.color = c;
                yield return null;
            }
            skyImage.gameObject.SetActive(false);

            Caption(Loc.T(
                "이 안에서는 모든 미래의 경로가 중심 특이점을 향합니다.\n— 여기까지가 물리학이 말할 수 있는 전부입니다.",
                "In here, every future path leads to the central singularity.\n— This is as far as physics can speak.",
                "この中では、あらゆる未来の経路が中心の特異点へ向かいます。\n— ここから先は、物理学が語れる限界です。",
                "在这里，所有未来的路径都通向中心奇点。\n— 物理学能讲述的，到此为止。"));
            yield return new WaitForSeconds(4f);

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
