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
        public float duration = 36f;

        public bool IsPlaying { get; private set; }

        Coroutine routine;
        Vector3 savedCamPos, savedMWPos;
        Quaternion savedCamRot, savedMWRot;
        float savedFov;

        GameObject andromeda;
        Material m31Volume, m31Stars;
        float m31DustInitial = 3.4f;
        Transform mw;

        Text caption, yearline;
        RectTransform captionPanel;
        Button stopButton;
        RectTransform tagMW, tagM31;
        Text tagMWText, tagM31Text;

        // Subtitle == voice (the exhibit-wide convention): these are the
        // exact texts the mw_m31_N clips are generated from. Line 0 carries
        // the 2025 honesty update — Sawala et al. (Nature Astronomy 2025)
        // put the merger at roughly a coin flip within 10 Gyr once the LMC's
        // pull and measurement uncertainty are included, so the encounter is
        // presented as the famous IF, not a certainty.
        public static readonly string[] NarrationLines =
        {
            "안드로메다 은하는 지금도 초속 110km로 우리를 향해 다가오고 있습니다. 정말 충돌할지는, 최신 연구에 따르면 반반입니다. 만약 충돌한다면 — 그 모습을, 50억 년을 1분에 담아 미리 봅니다.",
            "첫 만남 — 두 은하는 스쳐 지나가며, 조석력이 서로의 별들을 길게 뽑아 꼬리를 만듭니다.",
            "서로를 빠져나가려 하지만 — 보이지 않는 암흑물질 헤일로의 중력이 놓아주지 않습니다.",
            "결국 하나가 됩니다 — 밀코메다.\n나선팔은 흩어지고, 거대한 타원은하가 남습니다.",
            "별들 사이는 너무나 넓어서, 별끼리 부딪히는 일은 거의 없습니다.\n태양계는 무사히 — 다만 새 은하의 다른 자리로 옮겨질 것입니다.",
        };
        public static readonly string[] NarrationLinesEn =
        {
            "The Andromeda galaxy is approaching us right now, at 110 kilometres every second. Whether the two will truly collide is, by the latest research, a coin flip. If they do — here is that future, five billion years in one minute.",
            "First pass — the galaxies sweep by each other, and tides draw their stars out into long tails.",
            "They try to escape each other — but the gravity of their invisible dark-matter halos will not let go.",
            "In the end they become one — Milkomeda.\nThe spiral arms dissolve, and a giant elliptical remains.",
            "Space between stars is so vast that almost none of them ever collide.\nThe solar system survives — carried to a new place in a new galaxy.",
        };
        public static readonly string[] NarrationLinesJa =
        {
            "アンドロメダ銀河は今この瞬間も、秒速110kmで私たちに近づいています。本当に衝突するかどうかは、最新の研究では五分五分。もし衝突するなら — その未来を、50億年を1分にして先に見てみましょう。",
            "最初の遭遇 — 二つの銀河はすれ違いざま、潮汐力が互いの星を長い尾に引き出します。",
            "互いに離れようとしますが — 見えないダークマターハローの重力が逃がしません。",
            "ついに一つになります — ミルコメダ。\n渦状腕はほどけ、巨大な楕円銀河が残ります。",
            "星と星のあいだは広大で、星同士がぶつかることはほとんどありません。\n太陽系は無事に — ただ、新しい銀河の別の場所へ運ばれるだけです。",
        };
        public static readonly string[] NarrationLinesZh =
        {
            "仙女座星系此刻正以每秒110公里的速度向我们靠近。它们是否真的会相撞，最新研究认为是五五开。如果相撞——我们把那个未来、五十亿年压缩成一分钟，先看一遍。",
            "第一次交会——两个星系擦肩而过，潮汐力把彼此的恒星拉成长长的尾巴。",
            "它们试图挣脱彼此——但看不见的暗物质晕的引力不肯放手。",
            "最终它们合而为一——银河仙女星系。\n旋臂消散，留下一个巨大的椭圆星系。",
            "恒星之间的空间无比辽阔，几乎不会有恒星相撞。\n太阳系会安然无恙——只是被带到新星系的另一个角落。",
        };

        void CaptionBeat(int i) => Caption(Loc.T(NarrationLines[i], NarrationLinesEn[i], NarrationLinesJa[i], NarrationLinesZh[i]));

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
            UpdateTags(false);
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
            // Beats 1-3 fire mid-encounter; loading their mp3s at that moment
            // (DecompressOnLoad, main thread) stutters the dance.
            NarrationManager.Instance.Preload("mw_m31_0", "mw_m31_1", "mw_m31_2", "mw_m31_3", "mw_m31_4");
            SpawnAndromeda();

            // ---- framing ----------------------------------------------------
            float len = Narrate(0);
            CaptionBeat(0);
            camLook = mw.position;
            for (float t = 0f, dur = Mathf.Max(7f, len + 0.5f); t < dur; t += Time.deltaTime)
            {
                // The infall has already begun during the framing shot — a
                // static pair reads as a screensaver, not a countdown.
                PlaceBodies(0f, Mathf.Lerp(368f, 340f, t / dur), 0f);
                PlaceCamera(340f, Time.deltaTime, -1f);
                UpdateCompanions(1f);
                UpdateTags(true);
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
                PlaceCamera(sep, Time.deltaTime, u);
                Tides(u, sep);
                YearLine(u);
                UpdateTags(u < 0.28f);

                if (stage == 0 && u > 0.30f && NarrationDone)
                {
                    stage = 1; Narrate(1); CaptionBeat(1);
                }
                else if (stage == 1 && u > 0.52f && NarrationDone)
                {
                    stage = 2; Narrate(2); CaptionBeat(2);
                }
                else if (stage == 2 && u > 0.80f && NarrationDone)
                {
                    stage = 3; Narrate(3); CaptionBeat(3);
                }
                yield return null;
            }

            // ---- epilogue: the merged elliptical ------------------------------
            // The faster dance can end before the longer (ja, ko) voice tracks
            // clear their gates — the Milkomeda beat must never be skipped, so
            // catch it up over the merged body instead of dropping it.
            if (stage == 2)
            {
                while (!NarrationDone)
                {
                    PlaceCamera(0f, Time.deltaTime, 1f);
                    yield return null;
                }
                stage = 3; Narrate(3); CaptionBeat(3);
            }
            while (!NarrationDone)
            {
                PlaceCamera(0f, Time.deltaTime, 1f);
                yield return null;
            }
            len = Narrate(4);
            CaptionBeat(4);
            for (float t = 0f, dur = Mathf.Max(9f, len + 0.5f); t < dur; t += Time.deltaTime)
            {
                PlaceCamera(0f, Time.deltaTime, 1f);
                yield return null;
            }

            // ---- time rewinds ------------------------------------------------
            // The hard cut back to today's spiral read as a glitch. Run the
            // whole encounter BACKWARDS in three seconds — the year counter
            // spins down, Milkomeda unmixes, M31 retreats — then glide home.
            HideCaption();
            const float rewindDur = 3.2f;
            for (float t = 0f; t < rewindDur; t += Time.deltaTime)
            {
                float u = 1f - Mathf.SmoothStep(0f, 1f, t / rewindDur);
                float sep = Separation(u);
                theta -= Time.deltaTime * (34f / (sep + 12f)) * 6f; // unwinding fast
                PlaceBodies(u, sep, theta);
                PlaceCamera(sep, Time.deltaTime, u);
                Tides(u, sep);
                YearLine(u);
                yield return null;
            }
            // Glide everything home together — restoring the Milky Way to the
            // origin in one frame while it fills the screen reads as a pop.
            DestroyAndromeda();
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            Vector3 mwFrom = mw.position;
            for (float t = 0f, dur = 1.8f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                mw.position = Vector3.Lerp(mwFrom, savedMWPos, k);
                transform.position = Vector3.Lerp(fromPos, savedCamPos, k);
                transform.rotation = Quaternion.Slerp(fromRot, savedCamRot, k);
                yield return null;
            }
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

        /// <summary>u &lt; 0 = the framing shot. At 340 kpc separation no field
        /// of view holds both galaxies at a readable size, so the opening
        /// stands on ANDROMEDA'S side: M31 big in the foreground with its
        /// companions, and the Milky Way — us — the small disk it is falling
        /// toward. A slow sway keeps the shot alive. The wide two-body orbit
        /// blends in over the first sixth of the encounter.</summary>
        void PlaceCamera(float sep, float dt, float u)
        {
            Vector3 m31Pos = andromeda != null ? andromeda.transform.position : Vector3.zero;
            Vector3 axis = (m31Pos - mw.position).normalized;

            // Beyond M31, looking back through it toward home. The sway is a
            // gentle parallax drift, not a static tripod.
            Vector3 nearOffset = Quaternion.AngleAxis(Mathf.Sin(Time.time * 0.22f) * 9f, Vector3.up)
                               * (axis * 185f + Vector3.up * 58f);
            Vector3 nearWant = m31Pos + nearOffset;

            Vector3 focusWant, want;
            if (u < 0f)
            {
                focusWant = Vector3.Lerp(m31Pos, mw.position, 0.45f);
                want = nearWant;
            }
            else
            {
                float wide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.16f, u));
                // Aim a touch below the pair so the caption panel doesn't
                // swallow whichever galaxy swings through the lower frame.
                Vector3 centre = Vector3.down * (5f + sep * 0.05f);
                focusWant = Vector3.Lerp(Vector3.Lerp(m31Pos, mw.position, 0.45f), centre, wide);
                float dist = Mathf.Clamp(sep * 1.15f + 58f, 85f, 330f);
                camYaw += dt * 2.2f;
                var dir = (Quaternion.AngleAxis(camYaw, Vector3.up)
                         * new Vector3(0f, 0.5f, -1f)).normalized;
                Vector3 wideWant = dir * dist;
                want = Vector3.Lerp(nearWant, wideWant, wide);
            }
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-dt * 1.6f));
            camLook = Vector3.Lerp(camLook, focusWant, 1f - Mathf.Exp(-dt * 2.0f));
            transform.LookAt(camLook);
        }
        float camYaw;
        Vector3 camLook;

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

            // Andromeda fades into the common body at the very end. Emission
            // AND extinction: brightness alone leaves an invisible dust slab
            // that still absorbs — it read as a dark stripe across Milkomeda
            // (and survived zeroing the Milky Way's own dust, because it was
            // never the Milky Way's).
            if (m31Volume != null)
            {
                float keep = 1f - merge;
                m31Volume.SetFloat(BrightnessId, 2.2f * Mathf.Max(keep, 0.001f));
                m31Volume.SetFloat(DustId, m31DustInitial * keep);
                if (m31Stars != null) m31Stars.SetFloat(StarBrightId, 1.15f * keep);
                UpdateCompanions(keep);
            }
            // The survivor takes the elliptical look.
            if (controller.volumeMaterial != null)
            {
                controller.volumeMaterial.SetFloat(BulgeBoostId, Mathf.Lerp(0.9f, 2.4f, merge));
                // Ellipticals have no dust lane — even 0.12 still drew a faint
                // dark stripe across Milkomeda's core. Zero it completely.
                controller.volumeMaterial.SetFloat(DustId, Mathf.Lerp(3.4f, 0f, merge));
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
            // Noticeably bigger, and tilted so the pair never reads as mirrored.
            andromeda.transform.rotation = Quaternion.Euler(24f, 140f, 8f);
            andromeda.transform.localScale = Vector3.one * 1.25f;

            foreach (var mr in andromeda.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.sharedMaterial == null) continue;
                if (mr.sharedMaterial == controller.volumeMaterial)
                    mr.sharedMaterial = m31Volume = new Material(controller.volumeMaterial);
                else if (mr.sharedMaterial == controller.starMaterial)
                    mr.sharedMaterial = m31Stars = new Material(controller.starMaterial);
            }
            // Andromeda per the photographs, not a recolored Milky Way: a
            // dominant warm bulge, tightly wound smooth arms (~7 deg pitch vs
            // our ~12), the famous dark dust ring, and far quieter star
            // formation — M31's disk is redder and calmer than ours.
            if (m31Volume != null)
            {
                m31Volume.SetFloat("_BulgeBoost", 1.9f);
                m31Volume.SetFloat("_PitchTan", 0.13f);
                m31Volume.SetFloat("_ArmWidth", 1.35f);
                m31Volume.SetFloat("_Clumpiness", 0.55f);
                m31Volume.SetFloat(DustId, 5.0f);
                m31Volume.SetFloat("_HiiStrength", 0.35f);
                m31Volume.SetFloat("_YoungStrength", 0.5f);
            }
            if (m31Volume != null && m31Volume.HasProperty(DustId))
                m31DustInitial = m31Volume.GetFloat(DustId);
            SpawnCompanions();

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

        // ---------------- companions: M32 and M110 ----------------
        // The two dwarf ellipticals every photograph of Andromeda carries —
        // instantly identifying, and half of why M31 stops looking like a
        // mirrored Milky Way.

        Transform compM32, compM110;
        Material compMatM32, compMatM110;
        Texture2D compTex;

        void SpawnCompanions()
        {
            if (andromeda == null || compM32 != null) return;
            compTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 30f, dy = (y - 31.5f) / 30f;
                    float g = Mathf.Exp(-(dx * dx + dy * dy) * 4.5f);
                    compTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(g)));
                }
            compTex.Apply();

            compMatM32 = new Material(Shader.Find("Sprites/Default"))
                { mainTexture = compTex, color = new Color(1f, 0.93f, 0.80f, 0.9f), renderQueue = 3060 };
            compMatM110 = new Material(Shader.Find("Sprites/Default"))
                { mainTexture = compTex, color = new Color(0.95f, 0.90f, 0.82f, 0.55f), renderQueue = 3060 };

            compM32 = MakeCompanion("M32", new Vector3(5.5f, -1.6f, 2.2f), new Vector3(1.6f, 1.4f, 1f), compMatM32);
            compM110 = MakeCompanion("M110", new Vector3(-8.5f, 3.4f, -3.5f), new Vector3(3.1f, 1.5f, 1f), compMatM110);
        }

        Transform MakeCompanion(string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(andromeda.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        void UpdateCompanions(float keep)
        {
            // Billboards; they dissolve into the merger with their parent.
            if (compM32 != null)
            {
                compM32.rotation = Quaternion.LookRotation(compM32.position - transform.position);
                compMatM32.color = new Color(1f, 0.93f, 0.80f, 0.9f * keep);
            }
            if (compM110 != null)
            {
                compM110.rotation = Quaternion.LookRotation(compM110.position - transform.position);
                compMatM110.color = new Color(0.95f, 0.90f, 0.82f, 0.55f * keep);
            }
        }

        void DestroyAndromeda()
        {
            if (andromeda != null) Destroy(andromeda);
            if (m31Volume != null) Destroy(m31Volume);
            if (m31Stars != null) Destroy(m31Stars);
            if (compMatM32 != null) Destroy(compMatM32);
            if (compMatM110 != null) Destroy(compMatM110);
            if (compTex != null) Destroy(compTex);
            andromeda = null; m31Volume = null; m31Stars = null;
            compM32 = null; compM110 = null; compMatM32 = null; compMatM110 = null; compTex = null;
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

        /// <summary>Who is who: name tags floating over each galaxy while the
        /// two are still separate bodies. They retire once the first pass
        /// makes the question moot.</summary>
        void UpdateTags(bool on)
        {
            if (tagMW == null)
            {
                if (!on) return;
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                tagMW = BlackHoleUI.MakePanel(canvas.transform, "MW Tag",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(250f, 40f),
                    accentLine: false);
                tagMWText = BlackHoleUI.MakeText(tagMW, "Text", 18, BlackHoleUI.TitleGold, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 30f), FontStyle.Bold);
                tagM31 = BlackHoleUI.MakePanel(canvas.transform, "M31 Tag",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(250f, 40f),
                    accentLine: false);
                tagM31Text = BlackHoleUI.MakeText(tagM31, "Text", 18, BlackHoleUI.TitleGold, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 30f), FontStyle.Bold);
                tagMWText.text = Loc.T("우리은하 — 여기가 우리", "Milky Way — that's us",
                    "天の川銀河 — 私たちはここ", "银河系——我们在这里");
                tagM31Text.text = Loc.T("안드로메다 (M31)", "Andromeda (M31)",
                    "アンドロメダ (M31)", "仙女座星系 (M31)");
            }
            var cam = GetComponent<Camera>();
            PlaceTag(cam, tagMW, on, mw.position + Vector3.up * 14f);
            PlaceTag(cam, tagM31, on && andromeda != null,
                andromeda != null ? andromeda.transform.position + Vector3.up * 26f : Vector3.zero);
        }

        static void PlaceTag(Camera cam, RectTransform tag, bool on, Vector3 world)
        {
            if (tag == null) return;
            if (!on || cam == null) { tag.gameObject.SetActive(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool visible = sp.z > 0f;
            tag.gameObject.SetActive(visible);
            if (visible)
                tag.anchoredPosition = new Vector2(
                    sp.x / Screen.width * 1920f - 960f,
                    sp.y / Screen.height * 1080f - 540f + 10f);
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
