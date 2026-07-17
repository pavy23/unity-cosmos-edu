using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlackHoleEffect;

namespace MilkyWay
{
    /// <summary>
    /// The Andromeda encounter (F3): a five-billion-year timelapse in under a
    /// minute. M31 falls in, the two galaxies swing past each other twice —
    /// raising tidal tails on each pass — and settle into one elliptical:
    /// Milkomeda. The choreography follows the published simulations' story
    /// beats (first pass, swing-out, return, coalescence) as authored
    /// keyframes, not an N-body solve.
    ///
    /// The tides are shader work: each galaxy's volume inverse-warps its
    /// density samples along the line to its companion while the star mesh
    /// forward-warps the same amount, and at coalescence the arms switch off
    /// (_ArmStrength) while the stars phase-mix (_Scramble) into the ellipsoid.
    ///
    /// Both galaxies animate on SHARED material state for the Milky Way and
    /// instanced materials for Andromeda — everything is restored on finish,
    /// abort, and OnDisable (the killed-mid-play lesson from the black-hole
    /// exhibit: shared .mat assets remember what you leave in them).
    /// </summary>
    public class AndromedaCollision : MonoBehaviour
    {
        public MilkyWayController controller;
        public CinematicOrbit orbit;

        [Tooltip("Seconds for the whole five-billion-year story.")]
        public float duration = 52f;

        public bool IsPlaying { get; private set; }

        Coroutine routine;
        Vector3 savedCamPos, savedMWPos;
        Quaternion savedCamRot, savedMWRot;
        float savedFov;

        GameObject andromeda;
        Material m31Volume, m31Stars;
        Transform mw;

        Text caption, yearline;
        RectTransform captionPanel;
        Button stopButton;

        static readonly int TidalDirId = Shader.PropertyToID("_TidalDir");
        static readonly int TidalAmountId = Shader.PropertyToID("_TidalAmount");
        static readonly int ArmStrengthId = Shader.PropertyToID("_ArmStrength");
        static readonly int ScrambleId = Shader.PropertyToID("_Scramble");
        static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        static readonly int BulgeBoostId = Shader.PropertyToID("_BulgeBoost");
        static readonly int DustId = Shader.PropertyToID("_DustStrength");
        static readonly int StarBrightId = Shader.PropertyToID("_StarBrightness");

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
            transform.position = savedCamPos;
            transform.rotation = savedCamRot;
            Finish();
        }

        void Finish()
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = savedFov;
            RestoreMilkyWay();
            DestroyAndromeda();
            HideCaption();
            ShowStop(false);
            if (orbit != null) orbit.enabled = true;
            IsPlaying = false;
        }

        /// <summary>Leaving play mode mid-encounter skips Finish — and the tide
        /// would stay baked into the SHARED galaxy materials. Same self-heal as
        /// the black-hole cinematics.</summary>
        void OnDisable()
        {
            if (!IsPlaying) return;
            RestoreMilkyWay();
            DestroyAndromeda();
        }

        void RestoreMilkyWay()
        {
            if (mw != null)
            {
                mw.position = savedMWPos;
                mw.rotation = savedMWRot;
            }
            if (controller.volumeMaterial != null)
            {
                controller.volumeMaterial.SetFloat(TidalAmountId, 0f);
                controller.volumeMaterial.SetFloat(ArmStrengthId, 1f);
            }
            if (controller.starMaterial != null)
            {
                controller.starMaterial.SetFloat(TidalAmountId, 0f);
                controller.starMaterial.SetFloat(ScrambleId, 0f);
            }
            controller.Apply(); // brightness/bulge/dust back from the fields
        }

        // A beat may only fire once its predecessor's voice has finished —
        // thresholds alone would cut re-recorded longer lines mid-sentence.
        float narrEnd;
        bool NarrationDone => Time.time >= narrEnd;
        float Narrate(int i)
        {
            float len = NarrationManager.Instance.Play("mw_m31_" + i);
            narrEnd = Time.time + len + 0.4f;
            return len;
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            var cam = GetComponent<Camera>();
            savedCamPos = transform.position;
            savedCamRot = transform.rotation;
            savedFov = cam != null ? cam.fieldOfView : 38f;
            mw = controller.transform;
            savedMWPos = mw.position;
            savedMWRot = mw.rotation;
            if (orbit != null) orbit.enabled = false;
            ShowStop(true);
            SpawnAndromeda();

            // ---- framing ----------------------------------------------------
            float len = Narrate(0);
            Caption(Loc.T(
                "안드로메다 은하는 지금도 초속 110km로 우리를 향해 다가오고 있습니다.\n시간을 빠르게 돌려, 약 50억 년을 1분에 봅니다.",
                "The Andromeda galaxy is approaching us right now, at 110 km every second.\nLet's run time forward — five billion years in one minute.",
                "アンドロメダ銀河は今この瞬間も、秒速110kmで私たちに近づいています。\n時間を早送りして、約50億年を1分で見てみましょう。",
                "仙女座星系此刻正以每秒110公里的速度向我们靠近。\n让我们快进时间——用一分钟看完约50亿年。"));
            for (float t = 0f, dur = Mathf.Max(7f, len + 0.5f); t < dur; t += Time.deltaTime)
            {
                PlaceBodies(0f);
                PlaceCamera(0f, Time.deltaTime);
                yield return null;
            }

            // ---- the encounter ----------------------------------------------
            int stage = 0;
            float theta = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / duration);

                float sep = Separation(u);
                // Orbit sweep: faster when close (a nod to Kepler), integrated.
                theta += Time.deltaTime * (34f / (sep + 12f));
                PlaceBodies(u, sep, theta);
                PlaceCamera(sep, Time.deltaTime);
                Tides(u, sep);
                YearLine(u);

                if (stage == 0 && u > 0.30f && NarrationDone)
                {
                    stage = 1; Narrate(1);
                    Caption(Loc.T(
                        "첫 만남 — 두 은하는 스쳐 지나가며, 조석력이 서로의 별들을 길게 뽑아 꼬리를 만듭니다.",
                        "First pass — the galaxies sweep by each other, and tides draw their stars out into long tails.",
                        "最初の遭遇 — 二つの銀河はすれ違いざま、潮汐力が互いの星を長い尾に引き出します。",
                        "第一次交会——两个星系擦肩而过，潮汐力把彼此的恒星拉成长长的尾巴。"));
                }
                else if (stage == 1 && u > 0.52f && NarrationDone)
                {
                    stage = 2; Narrate(2);
                    Caption(Loc.T(
                        "서로를 빠져나가려 하지만 — 보이지 않는 암흑물질 헤일로의 중력이 놓아주지 않습니다.",
                        "They try to escape each other — but the gravity of their invisible dark-matter halos will not let go.",
                        "互いに離れようとしますが — 見えないダークマターハローの重力が逃がしません。",
                        "它们试图挣脱彼此——但看不见的暗物质晕的引力不肯放手。"));
                }
                else if (stage == 2 && u > 0.80f && NarrationDone)
                {
                    stage = 3; Narrate(3);
                    Caption(Loc.T(
                        "결국 하나가 됩니다 — 밀코메다.\n나선팔은 흩어지고, 거대한 타원은하가 남습니다.",
                        "In the end they become one — Milkomeda.\nThe spiral arms dissolve, and a giant elliptical remains.",
                        "ついに一つになります — ミルコメダ。\n渦状腕はほどけ、巨大な楕円銀河が残ります。",
                        "最终它们合而为一——银河仙女星系。\n旋臂消散，留下一个巨大的椭圆星系。"));
                }
                yield return null;
            }

            // ---- epilogue: the merged elliptical ------------------------------
            while (!NarrationDone)
            {
                PlaceCamera(0f, Time.deltaTime);
                yield return null;
            }
            len = Narrate(4);
            Caption(Loc.T(
                "별들 사이는 너무나 넓어서, 별끼리 부딪히는 일은 거의 없습니다.\n태양계는 무사히 — 다만 새 은하의 다른 자리로 옮겨질 것입니다.",
                "Space between stars is so vast that almost none of them ever collide.\nThe solar system survives — carried to a new place in a new galaxy.",
                "星と星のあいだは広大で、星同士がぶつかることはほとんどありません。\n太陽系は無事に — ただ、新しい銀河の別の場所へ運ばれるだけです。",
                "恒星之间的空间无比辽阔，几乎不会有恒星相撞。\n太阳系会安然无恙——只是被带到新星系的另一个角落。"));
            for (float t = 0f, dur = Mathf.Max(9f, len + 0.5f); t < dur; t += Time.deltaTime)
            {
                PlaceCamera(0f, Time.deltaTime);
                yield return null;
            }

            // Time rewinds: the exhibit returns to today's Milky Way.
            transform.position = savedCamPos;
            transform.rotation = savedCamRot;
            Finish();
        }

        // ---------------- choreography ----------------

        /// <summary>Separation (kpc) over normalized time: infall, first pass,
        /// swing-out, return, coalescence — the simulations' story beats as
        /// piecewise keyframes.</summary>
        static float Separation(float u)
        {
            if (u < 0.30f) return Mathf.Lerp(340f, 30f, Mathf.Pow(u / 0.30f, 1.6f));
            if (u < 0.42f) return Mathf.Lerp(30f, 22f, (u - 0.30f) / 0.12f);       // first pass
            if (u < 0.60f) return Mathf.Lerp(22f, 95f, Mathf.Sin((u - 0.42f) / 0.18f * 1.5708f)); // swing out
            if (u < 0.80f) return Mathf.Lerp(95f, 20f, Mathf.Pow((u - 0.60f) / 0.20f, 1.3f));     // fall back
            return Mathf.Lerp(20f, 0f, (u - 0.80f) / 0.20f);                        // coalesce
        }

        void PlaceBodies(float u, float sep = 340f, float theta = 0f)
        {
            var dir = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
            // Slight inclination on the approach so M31 comes down out of the sky.
            float lift = Mathf.Lerp(0.35f, 0f, Mathf.Clamp01(u / 0.4f));
            Vector3 offset = (dir + Vector3.up * lift).normalized * sep;

            mw.position = -offset * 0.5f;
            if (andromeda != null)
                andromeda.transform.position = offset * 0.5f;
        }

        void PlaceCamera(float sep, float dt)
        {
            // Wide enough to hold both bodies; drifts slowly so the dance reads.
            float dist = Mathf.Clamp(sep * 1.15f + 58f, 85f, 330f);
            camYaw += dt * 2.2f;
            var dir = (Quaternion.AngleAxis(camYaw, Vector3.up)
                     * new Vector3(0f, 0.5f, -1f)).normalized;
            Vector3 want = dir * dist;
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-dt * 1.6f));
            transform.LookAt(Vector3.zero);
        }
        float camYaw;

        void Tides(float u, float sep)
        {
            // Tidal stretch grows as separation shrinks, saturating near contact.
            float tide = Mathf.Clamp01(30f / (sep + 8f)) * 0.85f;
            // Coalescence: arms off, stars phase-mix, bulge swells — elliptical.
            float merge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 0.97f, u));
            tide *= 1f - merge; // the merged body relaxes

            ApplyEncounter(controller.volumeMaterial, controller.starMaterial,
                mw, andromeda != null ? andromeda.transform.position : Vector3.zero, tide, merge);
            if (andromeda != null)
                ApplyEncounter(m31Volume, m31Stars, andromeda.transform, mw.position, tide, merge);

            // Andromeda fades into the common body at the very end.
            if (m31Volume != null)
            {
                float keep = 1f - merge;
                m31Volume.SetFloat(BrightnessId, 2.2f * Mathf.Max(keep, 0.001f));
                if (m31Stars != null) m31Stars.SetFloat(StarBrightId, 1.15f * keep);
            }
            // The survivor takes the elliptical look.
            if (controller.volumeMaterial != null)
            {
                controller.volumeMaterial.SetFloat(BulgeBoostId, Mathf.Lerp(0.9f, 2.4f, merge));
                controller.volumeMaterial.SetFloat(DustId, Mathf.Lerp(3.4f, 0.6f, merge));
            }
        }

        static void ApplyEncounter(Material volume, Material stars, Transform body,
            Vector3 companionWorld, float tide, float merge)
        {
            Vector3 dirLocal = body.InverseTransformDirection(
                (companionWorld - body.position).normalized);
            if (volume != null)
            {
                volume.SetVector(TidalDirId, dirLocal);
                volume.SetFloat(TidalAmountId, tide);
                volume.SetFloat(ArmStrengthId, 1f - merge);
            }
            if (stars != null)
            {
                stars.SetVector(TidalDirId, dirLocal);
                stars.SetFloat(TidalAmountId, tide);
                stars.SetFloat(ScrambleId, merge);
            }
        }

        // ---------------- Andromeda ----------------

        void SpawnAndromeda()
        {
            if (andromeda != null) return;
            andromeda = Instantiate(controller.gameObject);
            andromeda.name = "Andromeda (M31)";
            // The clone's controller would keep writing the SHARED materials.
            Destroy(andromeda.GetComponent<MilkyWayController>());
            // A little bigger, and tilted so the pair never reads as mirrored.
            andromeda.transform.rotation = Quaternion.Euler(24f, 140f, 8f);
            andromeda.transform.localScale = Vector3.one * 1.15f;

            foreach (var mr in andromeda.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.sharedMaterial == null) continue;
                if (mr.sharedMaterial == controller.volumeMaterial)
                    mr.sharedMaterial = m31Volume = new Material(controller.volumeMaterial);
                else if (mr.sharedMaterial == controller.starMaterial)
                    mr.sharedMaterial = m31Stars = new Material(controller.starMaterial);
            }
            // The clone's star field rebuilt its own mesh in OnEnable; give the
            // freshly built child the instanced material too.
            var sf = andromeda.GetComponentInChildren<GalaxyStarField>();
            if (sf != null && m31Stars != null)
            {
                sf.material = m31Stars;
                foreach (var mr in andromeda.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.name == "Galaxy Stars Mesh") mr.sharedMaterial = m31Stars;
            }
        }

        void DestroyAndromeda()
        {
            if (andromeda != null) Destroy(andromeda);
            if (m31Volume != null) Destroy(m31Volume);
            if (m31Stars != null) Destroy(m31Stars);
            andromeda = null; m31Volume = null; m31Stars = null;
        }

        // ---------------- UI ----------------

        float yearTimer;

        void YearLine(float u)
        {
            if (yearline == null) return;
            // 4 Hz: rebuilding the string every frame is the physics panel's
            // old garbage-per-frame mistake all over again.
            yearTimer -= Time.deltaTime;
            if (yearTimer > 0f) return;
            yearTimer = 0.25f;
            // ~5 Gyr to full coalescence in the current best simulations.
            float gyr = u * 5f;
            yearline.text = "<color=#9AA3B5>+" + (gyr * 10f).ToString("0.0")
                + Loc.T("억 년", " hundred-million yr", "億年", "亿年") + "</color>";
        }

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "M31 Stop", Abort);
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
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "M31 Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(900f, 118f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 21, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(860f, 84f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
                yearline = BlackHoleUI.MakeText(captionPanel, "Years", 15, BlackHoleUI.TextSecondary, TextAnchor.LowerCenter,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(400f, 20f));
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
