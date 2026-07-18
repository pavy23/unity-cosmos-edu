using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHoleEffect
{
    /// <summary>
    /// Binary black-hole merger cinematic (F4) — the GW150914 story.
    /// Two lensing centers (superposed-deflection shader path) inspiral on a
    /// Peters-equation orbit a(t) = a_f + (a_0 − a_f)(1 − t/T)^{1/4} while a
    /// procedurally synthesized gravitational-wave chirp tracks 2× the actual
    /// orbital frequency. At contact the horizons merge: flash, ringdown
    /// tone, expanding wave shells, and a final hole with 95% of the total
    /// mass and spin a ≈ 0.69 — the measured GW150914 remnant. Narrated
    /// (Resources/Narration/binary_*), captions = transcript. Play mode only.
    /// </summary>
    public class BinaryMergerCinematic : MonoBehaviour
    {
        public BlackHoleController controller;
        public DesktopControls controls;

        [Tooltip("Companion mass in units of the primary (GW150914 ≈ 29/36).")]
        public float massRatio = 0.8f;
        [Tooltip("Primary mass in M units. Scaled below 1 so the pair starts wide: with 0.6/0.48 the initial separation is ~14x the total mass — the stage right after a GW150914-like signal enters the LIGO band — instead of the last couple of orbits.")]
        public float primaryMass = 0.6f;
        [Tooltip("Initial separation in sim units (GM/c² of a unit mass = 1).")]
        public float startSeparation = 15f;
        [Tooltip("Global time-lapse on the orbital motion: the real final 0.2 s of inspiral is stretched to ~25 s. Kepler scaling ω ∝ a^(-3/2) is preserved.")]
        public float timeLapse = 21f;

        public bool Running { get; private set; }

        // Narration transcripts (captions) — TTS clips generated from these.
        public static readonly string[] Lines =
        {
            "만약 두 개의 블랙홀이 서로 가까이 있다면 어떤 일이 벌어질까요? 두 블랙홀은 서로의 둘레를 돌며, 중력파로 에너지를 잃고, 나선을 그리며 서서히 가까워집니다.",
            "가까워질수록 공전은 빨라지고, 시공간의 잔물결인 중력파는 점점 높은 음으로 울립니다. 이것이 라이고가 들은 '처프'입니다.",
            "병합! 두 지평선이 하나가 되고, 태양 세 개 분량의 질량이 순수한 중력파 에너지로 우주에 방출됩니다.",
            "남은 것은 더 크고, 빠르게 회전하는 하나의 블랙홀입니다. 인류가 처음 들은 이 소리는 GW150914로 불립니다 — 중력파(GW)를 2015년 9월 14일에 검출했다는 뜻입니다.",
        };

        public static readonly string[] LinesEn =
        {
            "What happens when two black holes live side by side? They circle each other, losing energy to gravitational waves, spiraling slowly closer.",
            "As they close in, the orbit speeds up, and the ripples in spacetime — gravitational waves — ring at an ever higher pitch. This is the chirp that LIGO heard.",
            "Merger! The two horizons become one, and three suns' worth of mass is radiated into space as pure gravitational-wave energy.",
            "What remains is a single black hole — larger, and spinning fast. The first sound humanity ever heard is called GW150914 — a Gravitational Wave detected on September 14th, 2015.",
        };

        public static readonly string[] LinesJa =
        {
            "もし、ふたつのブラックホールがすぐ近くにあったら、何が起こるのでしょうか。ふたつは互いの周りを回りながら、重力波でエネルギーを失い、らせんを描いてゆっくりと近づいていきます。",
            "近づくほど公転は速くなり、時空のさざ波である重力波は、どんどん高い音で鳴り響きます。これがLIGOが聴いた「チャープ」です。",
            "合体！ふたつの地平面がひとつになり、太陽3個分の質量が純粋な重力波のエネルギーとして宇宙に放たれます。",
            "残ったのは、より大きく、速く回転するひとつのブラックホール。人類が初めて聴いたこの音は GW150914 と呼ばれます — 2015年9月14日に検出された重力波（GW）という意味です。",
        };

        public static readonly string[] LinesZh =
        {
            "如果两个黑洞彼此靠得很近，会发生什么？它们互相绕转，因引力波失去能量，沿着螺旋轨道慢慢靠近。",
            "越靠近，公转越快，时空的涟漪——引力波——发出的音调也越来越高。这就是LIGO听到的“啁啾”声。",
            "并合！两个视界合而为一，相当于三个太阳的质量化作纯粹的引力波能量释放到宇宙中。",
            "留下的是一个更大、快速旋转的黑洞。人类第一次听到的这个声音被命名为 GW150914——意思是2015年9月14日探测到的引力波（GW）。",
        };

        static readonly int BinaryOnId = Shader.PropertyToID("_BinaryOn");
        static readonly int Hole1PosId = Shader.PropertyToID("_Hole1Pos");
        static readonly int Hole2PosId = Shader.PropertyToID("_Hole2Pos");
        static readonly int Hole1MassId = Shader.PropertyToID("_Hole1Mass");
        static readonly int Hole2MassId = Shader.PropertyToID("_Hole2Mass");

        Material mat;
        Text caption;
        RectTransform captionPanel;
        Image flash;
        Button stopButton;
        Coroutine routine;

        // Exploration state saved by Run, restored on finish OR abort.
        float savedSpin, savedInner;
        float savedBrightness, savedStar, savedNebula;
        Material sky;                          // RenderSettings.skybox — kept in sync with the quad's star density
        float savedSkyStar, savedSkyNebula;
        Vector3 savedScale;
        LineRenderer[] rings;
        Material ringMat;

        // Orbit state (sim units, GM = c = 1, primary M = 1).
        float separation, theta, mTotal;

        // Chirp synth state (audio thread reads, main thread writes).
        AudioSource chirpSource;
        volatile float chirpFreq = 40f, chirpAmp;
        volatile bool ringdown;
        double synthPhase;
        float ringAmp;

        const int SampleRate = 48000;

        /// <summary>MR only: swaps the room for a starfield while we run, so the
        /// gas-free holes have something to lens. Null on desktop.</summary>
        MRSpaceWindow spaceWindow;

        public void Begin()
        {
            if (!Application.isPlaying || Running || controller == null) return;
            var r = controller.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            mat = r.sharedMaterial;
            spaceWindow = FindAnyObjectByType<MRSpaceWindow>();
            routine = StartCoroutine(Run());
        }

        void Update()
        {
            if (!Running) return;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Abort();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Abort();
#endif
        }

        /// <summary>Stop button / Esc: kills the cinematic and restores the
        /// exploration state exactly as the normal ending would.</summary>
        public void Abort()
        {
            if (!Running) return;
            if (routine != null) StopCoroutine(routine);
            NarrationManager.Instance.Stop();
            if (chirpSource != null) chirpSource.Stop();
            ringdown = false;
            DestroyRings();
            if (flash != null) flash.color = Color.clear;
            controller.transform.localScale = savedScale;
            controller.spin = savedSpin;
            controller.diskInnerRadius = savedInner;
            controller.diskBrightness = savedBrightness;
            controller.starDensity = savedStar;
            controller.nebulaHaze = savedNebula;
            controller.Apply();
            RestoreSky();
            if (mat != null) mat.SetFloat(BinaryOnId, 0f);
            // No fade on abort: the user asked to be out, and leaving this
            // undone would strand the headset in a starfield with passthrough
            // switched off in the material asset.
            if (spaceWindow != null) spaceWindow.CloseNow();
            Finish();
        }

        void Finish()
        {
            HideCaption();
            ShowStop(false);
            if (controls != null) controls.SetImmersive(false);
            Running = false;
        }

        void RestoreSky()
        {
            if (sky != null && sky.HasProperty("_StarDensity"))
            {
                sky.SetFloat("_StarDensity", savedSkyStar);
                sky.SetFloat("_NebulaIntensity", savedSkyNebula);
            }
        }

        /// <summary>
        /// Leaving play mode mid-cinematic kills the coroutine where it stands,
        /// and Finish/Abort never run. The controller rewrites the hole's material
        /// in edit mode ([ExecuteAlways]), so that one heals itself — but the
        /// skybox is a shared asset with no such owner, and the boost would stay
        /// baked in the .mat. Worse, it compounds: the next run reads the boosted
        /// value as the exploration one to restore to.
        /// </summary>
        void OnDisable()
        {
            if (!Running) return;
            RestoreSky();
            if (mat != null) mat.SetFloat(BinaryOnId, 0f);
        }

        void DestroyRings()
        {
            if (rings != null)
                foreach (var r in rings)
                    if (r != null) Destroy(r.gameObject);
            rings = null;
            if (ringMat != null) { Destroy(ringMat); ringMat = null; }
        }

        void ShowStop(bool on)
        {
            if (stopButton == null)
            {
                if (!on) return;
                stopButton = BlackHoleUI.MakeCinematicButton(GetComponent<Camera>(), "Merger Stop", Abort);
            }
            stopButton.gameObject.SetActive(on);
            if (on)
                stopButton.GetComponentInChildren<Text>().text =
                    Loc.T("중단 ■", "Stop ■", "中止 ■", "停止 ■");
        }

        IEnumerator Run()
        {
            Running = true;
            if (controls != null) controls.SetImmersive(true);
            ShowStop(true);

            // In MR, trade the room for space before anything else: the disk is
            // about to disperse, and a bare hole against passthrough is invisible.
            if (spaceWindow != null) yield return spaceWindow.Open(1.2f);

            // --- save the exploration state -------------------------------
            savedSpin = controller.spin;
            savedInner = controller.diskInnerRadius;
            savedScale = controller.transform.localScale;
            savedBrightness = controller.diskBrightness;
            savedStar = controller.starDensity;
            savedNebula = controller.nebulaHaze;
            // The skybox (behind the raymarch quad) has its OWN star density;
            // the quad's starfield is drawn to match it. When we enrich the
            // quad's field for the gas-free lensing we must enrich the skybox
            // by the SAME amount, or the quad's rectangle shows as a brighter
            // square patch when zoomed out.
            sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_StarDensity"))
            {
                savedSkyStar = sky.GetFloat("_StarDensity");
                savedSkyNebula = sky.GetFloat("_NebulaIntensity");
            }

            controller.spin = 0f;                      // superposition path is Schwarzschild
            mTotal = primaryMass * (1f + massRatio);
            separation = startSeparation;
            theta = 0f;

            // GW150914 was gas-free (no accretion disk — no electromagnetic
            // counterpart). The disk disperses over the first ~1.8 s of the
            // inspiral, leaving two BARE black holes that lens the starfield as
            // they spiral together — two clearly distinct objects merging,
            // which is both the accurate picture and the intuitive one. The
            // starfield is enriched so the lensing reads clearly.
            controller.diskInnerRadius = 6f;
            controller.Apply();
            mat.SetFloat(Hole1MassId, primaryMass);
            mat.SetFloat(Hole2MassId, primaryMass * massRatio);
            mat.SetFloat(BinaryOnId, 1f);

            EnsureChirp();
            if (chirpSource.clip != null) chirpSource.Play();
            EnsureFlash();   // created up front so it can swell during the final approach

            // --- Phase 1: slow inspiral while the story is told -----------
            Caption(0);
            float len0 = NarrationManager.Instance.Play("binary_0");
            float phase1 = Mathf.Max(8f, len0 + 0.5f);

            // --- Phase 2: the chirp — Peters decay a ∝ (1 − t/T)^{1/4} ----
            // Total inspiral spans phase 1 + 2 so the decay is continuous.
            float mergeSep = 2.02f * mTotal * 1.06f;   // horizons touch
            float phase2 = 14f;                        // narration starts partway in
            float T = phase1 + phase2;
            bool narrated2 = false;

            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                if (!narrated2 && t >= phase1)
                {
                    Caption(1);
                    NarrationManager.Instance.Play("binary_1");
                    narrated2 = true;
                }
                // Disperse the gas: fade the disk (and its minidisks) out over
                // the first 1.8 s and enrich the starfield so the two bare holes
                // lens it clearly. The skybox is enriched by the SAME amount so
                // the quad never shows as a brighter square patch.
                float g = Mathf.Clamp01(t / 1.8f);
                controller.diskBrightness = savedBrightness * (1f - g);
                controller.starDensity = Mathf.Lerp(savedStar, 0.42f, g);
                controller.nebulaHaze = Mathf.Lerp(savedNebula, 0.28f, g);
                controller.Apply();
                if (sky != null && sky.HasProperty("_StarDensity"))
                {
                    sky.SetFloat("_StarDensity", controller.starDensity);
                    sky.SetFloat("_NebulaIntensity", controller.nebulaHaze);
                }

                float u = Mathf.Clamp01(t / T);
                separation = mergeSep + (startSeparation - mergeSep) * Mathf.Pow(1f - u, 0.25f);
                StepOrbit(Time.deltaTime);
                PulseWaves(u, theta);

                // Pre-merger flash swell: over the last 0.3 s of the inspiral
                // the flash brightens to fully opaque, so the fast, tight final
                // approach — the smallest pre-merger moment — is already hidden
                // BEFORE the flip. The view never lingers on the small size; it
                // goes straight to the flash and then the final remnant. (Keyed
                // to time, not separation: the Peters decay makes separation
                // collapse sub-frame at the very end.)
                if (flash != null)
                    flash.color = new Color(0.9f, 0.94f, 1f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(T - 0.3f, T, t)));

                if (separation <= mergeSep * 1.01f) break;
                yield return null;
            }

            // --- Merger ----------------------------------------------------
            ringdown = true;                            // chirp → damped ringdown tone
            mat.SetFloat(BinaryOnId, 0f);
            SendHaptics(1f, 0.3f);                      // the strain peak, felt in the hands

            // The remnant is its FINAL size from the moment of merging — a
            // merged black hole does not inflate afterwards. The apparent
            // shadow jump (two-hole contact blob → single remnant) happens in
            // the one frame the fully-opaque flash covers, so no growth is ever
            // seen. finalMass = 95% of the total (5% radiated as GW); spin
            // settles to ≈0.69 (the measured GW150914 remnant).
            float finalMass = 0.95f * mTotal;
            Vector3 finalScale = savedScale * finalMass;
            controller.transform.localScale = finalScale;

            Caption(2);
            float len2 = NarrationManager.Instance.Play("binary_2");

            // Gravitational waves: expanding wavefronts, each deformed by the
            // + polarization pattern of a merger — radius ∝ 1 + ε·cos(2φ − ψ)
            // with the quadrupole pattern rotating on, thinning as they expand.
            // 8 fronts at a short launch interval (below) so they read as a
            // continuous wave train instead of a few discrete rings popping in.
            rings = new LineRenderer[8];
            ringMat = new Material(Shader.Find("BlackHole/PhotonTrail"));
            ringMat.SetColor("_Tint", new Color(1.5f, 2.0f, 3.3f, 1f));
            ringMat.SetFloat("_HeadBoost", 0f);
            ringMat.SetFloat("_TailFade", 0f);
            ringMat.SetFloat("_PulseAmount", 0f);
            const int RingSegs = 96;
            for (int i = 0; i < rings.Length; i++)
            {
                var go = new GameObject("GW Ring");
                go.transform.position = controller.transform.position;
                rings[i] = go.AddComponent<LineRenderer>();
                rings[i].material = ringMat;
                rings[i].loop = true;
                rings[i].positionCount = RingSegs;
                // Rounds the loop-closure joint — without corner vertices the
                // wide stroke shows a notch where the polygon closes.
                rings[i].numCornerVertices = 6;
                rings[i].useWorldSpace = true;
                rings[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[i].startColor = rings[i].endColor = Color.clear;
            }

            float mergeDur = Mathf.Max(4.5f, len2 + 0.5f);
            const float spinRampDur = 1.6f;  // settle into the Kerr remnant
            Vector3 center = controller.transform.position;
            // Scale is already at finalScale (set above) and never changes here.
            for (float t = 0f; t < mergeDur; t += Time.deltaTime)
            {
                // Ramp the spin up (Schwarzschild → Kerr a≈0.69) once the flash
                // has revealed the remnant, so the shadow shape settles
                // continuously instead of snapping. This is a shape change, not
                // a size change — the hole never appears to grow.
                if (t > 0.55f)
                    controller.SetSpin(Mathf.SmoothStep(0f, 0.69f, Mathf.Clamp01((t - 0.55f) / spinRampDur)));

                if (flash != null)
                    // Merger flash: fully opaque for the flip frame so the
                    // topology change AND the fast pre-merger orbital motion are
                    // completely hidden (no "cut"), then eases out (smoothstep,
                    // not a linear ramp) so the reveal is gradual rather than a
                    // hard wipe.
                    flash.color = new Color(0.9f, 0.94f, 1f,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.55f - t) / 0.5f)));

                // Camera-facing wavefronts: gravitational waves radiate in
                // every direction, so the rings are drawn as the spherical
                // wavefront's cross-section toward the viewer (also keeps
                // them from being half-hidden behind the opaque hole quad).
                var gwCam = Camera.main;
                Vector3 toCam = gwCam != null ? (gwCam.transform.position - center).normalized : Vector3.up;
                Vector3 upRef = Mathf.Abs(toCam.y) > 0.98f ? Vector3.right : Vector3.up;
                Vector3 axR = Vector3.Cross(upRef, toCam).normalized;
                Vector3 axU = Vector3.Cross(toCam, axR);
                Vector3 lift = toCam * (1.1f * controller.transform.localScale.x);

                // ψ spins the quadrupole pattern — the visual echo of the
                // ringdown; each front launches 0.45 s after the previous.
                float psi = t * 5.2f;
                for (int i = 0; i < rings.Length; i++)
                {
                    float ts = t - i * 0.28f;
                    float ks = Mathf.Clamp01(ts / 3.6f);
                    if (ts < 0f || ks >= 1f) { rings[i].startColor = rings[i].endColor = Color.clear; continue; }
                    float radius = Mathf.Lerp(1.3f, 26f, Mathf.Pow(ks, 0.85f)) * savedScale.x;
                    float eps = 0.11f * (1f - ks);              // deformation dies down
                    // Fade each front IN over the first sliver of its life (no
                    // pop at launch) and back out as it expands.
                    float fade = Mathf.Pow(1f - ks, 1.7f) * Mathf.SmoothStep(0f, 0.1f, ks);
                    rings[i].widthMultiplier = Mathf.Lerp(0.5f, 0.1f, ks) * savedScale.x;
                    rings[i].startColor = rings[i].endColor = new Color(1f, 1f, 1f, fade);
                    for (int sgm = 0; sgm < RingSegs; sgm++)
                    {
                        float angR = sgm / (float)RingSegs * Mathf.PI * 2f;
                        float rr = radius * (1f + eps * Mathf.Cos(2f * angR - psi));
                        rings[i].SetPosition(sgm,
                            center + (axR * Mathf.Cos(angR) + axU * Mathf.Sin(angR)) * rr + lift);
                    }
                }
                yield return null;
            }
            if (flash != null) flash.color = Color.clear;
            DestroyRings();

            // Spin already ramped to ≈0.69 during the ringdown loop; pin the
            // exact value in case the loop ended a hair early.
            controller.SetSpin(0.69f);

            // --- Aftermath: return to the explorable state — a disk settles
            // back in around the spinning remnant while the starfield relaxes.
            Caption(3);
            float len3 = NarrationManager.Instance.Play("binary_3");
            float after = Mathf.Max(8f, len3 + 1f);
            controller.diskInnerRadius = savedInner;
            for (float t = 0f; t < after; t += Time.deltaTime)
            {
                float g = Mathf.SmoothStep(0f, 1f, t / after);
                controller.diskBrightness = savedBrightness * g;
                controller.starDensity = Mathf.Lerp(0.42f, savedStar, g);
                controller.nebulaHaze = Mathf.Lerp(0.28f, savedNebula, g);
                controller.Apply();
                if (sky != null && sky.HasProperty("_StarDensity"))
                {
                    sky.SetFloat("_StarDensity", controller.starDensity);
                    sky.SetFloat("_NebulaIntensity", controller.nebulaHaze);
                }
                yield return null;
            }

            // --- restore exploration state ---------------------------------
            chirpSource.Stop();
            ringdown = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                controller.transform.localScale = Vector3.Lerp(finalScale, savedScale, t / 1.2f);
                yield return null;
            }
            controller.transform.localScale = savedScale;
            controller.spin = savedSpin;
            controller.diskInnerRadius = savedInner;
            controller.diskBrightness = savedBrightness;
            controller.starDensity = savedStar;
            controller.nebulaHaze = savedNebula;
            controller.Apply();
            RestoreSky();
            mat.SetFloat(BinaryOnId, 0f);

            // Hand the room back only once the disk has settled again, so the
            // last thing seen in space is the remnant with its disk, not a cut.
            if (spaceWindow != null) yield return spaceWindow.Close(1.2f);

            Finish();
        }

        /// <summary>
        /// Gravitational waves you can feel: the controllers pulse twice per orbit
        /// (the quadrupole radiates at 2×the orbital frequency) and grow toward the
        /// merger. Inherited from the old MR-only merger demo — on desktop there is
        /// no haptic device and every call is a no-op.
        /// </summary>
        void PulseWaves(float u, float orbitPhase)
        {
            if (Mathf.Sin(orbitPhase * 2f) <= 0.6f) return;
            float amplitude = Mathf.Clamp01(0.08f + Mathf.Pow(u, 2.5f));
            SendHaptics(amplitude, 0.04f);
        }

        static void SendHaptics(float amplitude, float duration)
        {
            SendTo(UnityEngine.XR.XRNode.LeftHand, amplitude, duration);
            SendTo(UnityEngine.XR.XRNode.RightHand, amplitude, duration);
        }

        static void SendTo(UnityEngine.XR.XRNode node, float amplitude, float duration)
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0, amplitude, duration);
        }

        void StepOrbit(float dt)
        {
            // Kepler in sim units: ω² = M_total / a³, played back at a global
            // time-lapse so months of inspiral fit into the cinematic.
            float omega = Mathf.Sqrt(mTotal / Mathf.Max(separation * separation * separation, 1f)) * timeLapse;
            theta += omega * dt;

            // Each hole orbits the barycenter at a distance ∝ the OTHER mass.
            float f1 = massRatio / (1f + massRatio);   // primary's fraction of a
            float f2 = 1f / (1f + massRatio);          // companion's fraction
            Vector3 dir = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
            Vector3 c1 = -dir * (separation * f1);
            Vector3 c2 = dir * (separation * f2);
            mat.SetVector(Hole1PosId, c1);
            mat.SetVector(Hole2PosId, c2);

            // GW frequency = 2 × orbital frequency, mapped into audio range;
            // amplitude grows as the pair tightens (∝ 1/a).
            chirpFreq = Mathf.Clamp(omega * 18f, 25f, 700f);
            chirpAmp = Mathf.Clamp01(1.6f / separation) * 0.55f;
        }

        // ---------------- chirp / ringdown synthesizer ----------------
        void EnsureChirp()
        {
            if (chirpSource != null) return;
            chirpSource = gameObject.AddComponent<AudioSource>();
            chirpSource.spatialBlend = 0f;
            chirpSource.volume = 0.8f;
            // WebGL can't run the procedural callback — no chirp there.
            if (!ProceduralAudio.Supported) return;
            chirpSource.clip = AudioClip.Create("GWChirp", SampleRate, 1, SampleRate, true, OnChirpRead);
            chirpSource.loop = true;
        }

        void OnChirpRead(float[] data)
        {
            double dt = 1.0 / SampleRate;
            for (int i = 0; i < data.Length; i++)
            {
                if (ringdown)
                {
                    // Quasinormal ringing of the remnant: fixed tone, fast decay.
                    if (ringAmp <= 0.0001f) { data[i] = 0f; continue; }
                    synthPhase += 2.0 * System.Math.PI * 240.0 * dt;
                    ringAmp *= 1f - 1.8f * (float)dt;
                    data[i] = (float)System.Math.Sin(synthPhase) * ringAmp * 0.6f;
                }
                else
                {
                    synthPhase += 2.0 * System.Math.PI * chirpFreq * dt;
                    float s = (float)System.Math.Sin(synthPhase);
                    // soft second harmonic for body
                    s += 0.35f * (float)System.Math.Sin(synthPhase * 2.0);
                    data[i] = s * chirpAmp * 0.5f;
                    ringAmp = Mathf.Max(ringAmp, chirpAmp); // seed the ringdown level
                }
            }
        }

        // ---------------- UI ----------------
        void Caption(int i)
        {
            if (caption == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                captionPanel = BlackHoleUI.MakePanel(canvas.transform, "Merger Caption",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(900f, 110f));
                caption = BlackHoleUI.MakeText(captionPanel, "Text", 21, BlackHoleUI.TextPrimary, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 94f));
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            captionPanel.gameObject.SetActive(true);
            caption.text = Loc.T(Lines[i], LinesEn[i], LinesJa[i], LinesZh[i]);
        }

        void HideCaption()
        {
            if (captionPanel != null) captionPanel.gameObject.SetActive(false);
        }

        void EnsureFlash()
        {
            if (flash != null) return;
            // Must cover the whole view: the flash is what hides the flip from two
            // holes to one, and anything it fails to cover shows the jump.
            flash = BlackHoleUI.MakeFullViewOverlay(GetComponent<Camera>(), "Merger Flash");
        }
    }
}
