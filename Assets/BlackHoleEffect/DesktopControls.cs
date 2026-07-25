using UnityEngine;
using UnityEngine.Rendering.Universal; // camera post-processing toggle
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlackHoleEffect
{
    /// <summary>
    /// Pointer control for the desktop/web showcase (play mode).
    ///
    /// Every feature is a toolbar button (see DesktopToolbar) — the hotkey
    /// layer was removed so the exhibit works in a browser and on a phone,
    /// where keys either collide with the browser or don't exist. What is
    /// left: right-drag or one-finger drag to orbit, wheel or pinch to zoom,
    /// ← → to step a running tour, Esc to leave immersive view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DesktopControls : MonoBehaviour
    {
        public Transform target;
        public BlackHoleController controller;
        public BlackHolePhysicsPanel panel;
        public BlackHoleAnnotations annotations;
        public PhotonLauncher launcher;
        public EinsteinRingDemo einsteinDemo;
        public ObservationComparison comparison;
        public CinematicOrbit autoOrbit;
        public SpaghettificationDemo spaghetti;
        public RelativisticJets jets;
        public GuidedTour tour;
        public BlackHoleAudio audioScape;
        public PerformanceHud hud;
        public LightCurveGraph lightCurve;
        public IntroSequence intro;
        public FallInMode fallIn;
        public GravitationalLensDemo lensDemo;

        [Tooltip("Set by cinematic modes (fall-in) to take over the camera.")]
        public bool suspendCamera;

        [Tooltip("MR: the headset owns the camera pose and the compositor owns the " +
                 "alpha channel. Disables orbit/zoom/reset and post-processing; every " +
                 "other toggle (and the keyboard, handy for editor testing) still works.")]
        public bool xrMode;

        TheoryPanel theory;
        BinaryMergerCinematic binary;

        public TheoryPanel Theory => theory;
        public BinaryMergerCinematic Binary => binary;

        /// <summary>True while any narrated experience owns the stage —
        /// used to block competing cinematics (and the intro autoplay).</summary>
        public bool CinematicBusy =>
            (intro != null && intro.IsPlaying) ||
            (fallIn != null && fallIn.IsFalling) ||
            (binary != null && binary.Running) ||
            (tour != null && tour.Running);

        public void CycleDifficulty()
        {
            if (annotations == null) return;
            annotations.difficulty = (BlackHoleAnnotations.Difficulty)
                (((int)annotations.difficulty + 1) % 3);
            if (panel != null)
            {
                panel.showDilationRow = annotations.difficulty != BlackHoleAnnotations.Difficulty.Elementary;
                panel.RefreshText();
            }
            // 고등(정량) 난이도 = 수식 패널 자동 표시; 그 외에는 숨김 (X로 언제든 토글).
            bool advanced = annotations.difficulty == BlackHoleAnnotations.Difficulty.High;
            if (theory != null) theory.SetVisible(advanced);
            string name = annotations.difficulty switch
            {
                BlackHoleAnnotations.Difficulty.Elementary => Loc.T(
                    "초등 — 쉬운 설명", "Elementary — simple wording",
                    "初級 — やさしい説明", "初级 — 简单说明"),
                BlackHoleAnnotations.Difficulty.High => Loc.T(
                    "고등 — 정량적 설명 (수식 패널 ON)", "Advanced — quantitative (theory panel ON)",
                    "上級 — 定量的な説明（数式パネルON）", "高级 — 定量说明（公式面板开）"),
                _ => Loc.T("중등 — 표준 설명", "Standard — default wording",
                           "標準 — 標準的な説明", "中级 — 标准说明"),
            };
            ShowToast(Loc.T("난이도: ", "Level: ", "難易度: ", "难度: ") + name);
        }

        public void ToggleLanguage()
        {
            Loc.Cycle();
            RefreshLanguage();
            ShowToast(Loc.T("언어: ", "Language: ", "言語: ", "语言: ") + Loc.DisplayName);
        }

        /// <summary>Re-applies the current language to everything that is not
        /// rebuilt per frame — used by K and the startup language picker.</summary>
        public void RefreshLanguage()
        {
            if (tour != null) tour.OnLanguageChanged();
            if (panel != null) panel.RefreshText();
        }

        // ---- one-key preset cycles (1 = disk colors, 2 = mass) ----------

        public void CycleColor()
        {
            if (controller == null) return;
            var next = controller.preset switch
            {
                BlackHoleController.DiskPreset.Gargantua => BlackHoleController.DiskPreset.RedGiant,
                BlackHoleController.DiskPreset.RedGiant => BlackHoleController.DiskPreset.BlueQuasar,
                _ => BlackHoleController.DiskPreset.Gargantua,
            };
            controller.SetPreset(next);
            string name = next switch
            {
                BlackHoleController.DiskPreset.RedGiant => Loc.T(
                    "저온 원반 — 깊은 적색", "Cool disk — deep red", "低温円盤 — 深い赤", "低温吸积盘 — 深红"),
                BlackHoleController.DiskPreset.BlueQuasar => Loc.T(
                    "퀘이사 원반 — 고온 청백색", "Quasar disk — hot blue-white", "クエーサー円盤 — 高温の青白", "类星体盘 — 高温蓝白"),
                _ => Loc.T("가르강튀아 — 따뜻한 크림색", "Gargantua — warm cream", "ガルガンチュア — 温かいクリーム色", "卡冈图雅 — 暖奶油色"),
            };
            ShowToast(Loc.T("원반 색상: ", "Disk colors: ", "円盤の色: ", "吸积盘颜色: ") + name);
        }

        int massIndex = 1; // scene starts as Sagittarius A*

        public void CycleMass()
        {
            if (panel == null) return;
            massIndex = (massIndex + 1) % 3;
            panel.SetMassPreset((BlackHolePhysicsPanel.MassPreset)massIndex);
            ShowToast(Loc.T("질량: ", "Mass: ", "質量: ", "质量: ")
                + Loc.T(panel.massLabel, panel.massLabelEn, panel.massLabelJa, panel.massLabelZh));
        }

        // ---- phenomenon toggles: every ON shows an explanation card ------

        public void ToggleEinstein()
        {
            if (einsteinDemo == null) return;
            einsteinDemo.active = !einsteinDemo.active;
            if (!einsteinDemo.active) { ExplainCard.Hide(); return; }
            ExplainCard.Show(
                Loc.T("아인슈타인 링", "Einstein Ring", "アインシュタインリング", "爱因斯坦环"),
                Loc.T("블랙홀 뒤의 별빛이 중력에 휘어 두 개의 상으로 갈라져 보입니다. 별이 정확히 뒤에 정렬되는 순간, 빛이 사방으로 휘어 완전한 고리가 됩니다.",
                      "Light from a star behind the hole is split into two images. At exact alignment the light bends around every side at once and becomes a complete ring.",
                      "ブラックホールの後ろの星の光は、ふたつの像に分かれて見えます。星が真後ろに整列した瞬間、光が全方向に曲がり、完全なリングになります。",
                      "黑洞后方的星光被分成两个像。当恰好对齐时，光从四面八方弯过来，成为完整的光环。"));
        }

        public void ToggleSpaghetti()
        {
            if (spaghetti == null) return;
            spaghetti.active = !spaghetti.active;
            if (!spaghetti.active) { ExplainCard.Hide(); return; }
            ExplainCard.Show(
                Loc.T("스파게티화 — 조석력", "Spaghettification — Tidal Force", "スパゲッティ化 — 潮汐力", "面条化 — 潮汐力"),
                Loc.T("별의 머리와 발에 걸리는 중력 차이(조석력)가 별을 국수처럼 길게 늘여 찢습니다. 찢긴 물질은 두 갈래로 갈라져, 절반은 빨려 들고 절반은 튕겨 나갑니다.",
                      "The difference in gravity across the star — the tidal force — stretches it like a noodle and tears it apart. The debris splits into two streams: half falls in, half is flung away.",
                      "星の両端にかかる重力の差（潮汐力）が、星を麺のように引き伸ばして引き裂きます。物質はふたつの流れに分かれ、半分は吸い込まれ、半分ははじき出されます。",
                      "恒星两端受到的引力差——潮汐力——把它像面条一样拉长撕裂。碎片分成两股：一半落入黑洞，一半被甩出去。"));
        }

        public void ToggleJets()
        {
            if (jets == null) return;
            jets.active = !jets.active;
            if (!jets.active) { ExplainCard.Hide(); return; }
            ExplainCard.Show(
                Loc.T("상대론적 제트", "Relativistic Jets", "相対論的ジェット", "相对论性喷流"),
                Loc.T("빨려 들던 물질의 일부는 삼켜지지 않습니다. 회전하는 자기장에 감겨 올라가 양극에서 거의 광속으로 뿜어져 나가고, 실제 우주에서는 수천 광년까지 뻗어 나갑니다.",
                      "Not everything falling in gets swallowed. Some matter is wound up by rotating magnetic fields and blasted from the poles at nearly light speed — real jets stretch for thousands of light-years.",
                      "落ち込む物質のすべてが呑み込まれるわけではありません。一部は回転する磁場に巻き上げられ、両極からほぼ光速で噴き出します。実際のジェットは数千光年まで伸びます。",
                      "并非所有下落的物质都被吞掉。一部分被旋转磁场卷起，从两极以接近光速喷出——真实的喷流可延伸数千光年。"));
        }

        public void ToggleLightCurve()
        {
            if (lightCurve == null) return;
            lightCurve.show = !lightCurve.show;
            if (!lightCurve.show) { ExplainCard.Hide(); return; }
            ExplainCard.Show(
                Loc.T("광도 곡선", "Light Curve", "光度曲線", "光变曲线"),
                Loc.T("원반 전체의 밝기를 시간에 따라 기록한 그래프입니다. 실제 망원경이 블랙홀을 '보는' 방법이죠. '스파게티화'로 별을 찢어 보세요 — 밝기가 치솟는 조석파괴사건(TDE)이 그래프에 나타납니다.",
                      "A record of the disk's total brightness over time — this is how real telescopes 'see' black holes. Tear a star apart with 'Spaghettify': a tidal disruption flare will spike the curve.",
                      "円盤全体の明るさを時間で記録したグラフです。実際の望遠鏡はこうやってブラックホールを『見て』います。「スパゲッティ化」で星を裂いてみてください — 潮汐破壊イベントの増光が現れます。",
                      "记录吸积盘总亮度随时间变化的图线——真实望远镜正是这样'看'黑洞的。用「面条化」撕裂恒星：曲线上会出现潮汐瓦解耀发。"));
        }

        public void ToggleLens()
        {
            if (lensDemo == null) return;
            bool wasOn = lensDemo.Active;
            lensDemo.Toggle();
            if (wasOn) { ExplainCard.Hide(); return; }
            ExplainCard.Show(
                Loc.T("중력 렌즈", "Gravitational Lensing", "重力レンズ", "引力透镜"),
                Loc.T("원반을 잠시 꺼서, 배경 별빛이 중력만으로 어떻게 왜곡되는지 봅니다. 밝은 배경 광원이 좌우로 오가며 상이 갈라지고 고리로 이어지는 것을 관찰해 보세요.",
                      "The disk is switched off so you can watch background starlight warp under gravity alone. Watch the bright source sweep by: its image splits, stretches, and closes into rings.",
                      "円盤を一時オフにして、背景の星の光が重力だけでどう歪むかを見ます。明るい光源が左右に動くと、像が分かれ、伸び、リングにつながります。",
                      "暂时关闭吸积盘，只看背景星光如何被引力扭曲。观察明亮光源左右移动时，像被分裂、拉伸、连成光环。"));
        }

        public void CycleComparison()
        {
            if (comparison == null) return;
            bool wasOff = !comparison.show;
            comparison.CycleMode();
            if (!comparison.show) { ExplainCard.Hide(); return; }
            if (wasOff)
                ExplainCard.Show(
                    Loc.T("실제 관측과 비교", "Compare with Real Observations", "実際の観測と比較", "与真实观测对比"),
                    Loc.T("사건의 지평선 망원경(EHT)이 촬영한 실제 블랙홀 사진과 이 시뮬레이션을 나란히 비교합니다. 4를 다시 누르면 대상이 바뀝니다.",
                          "The simulation side by side with the real Event Horizon Telescope images. Press 4 again to switch targets.",
                          "イベント・ホライズン・テレスコープ(EHT)が撮影した実際の写真と、このシミュレーションを並べて比較します。4でターゲットが切り替わります。",
                          "把事件视界望远镜(EHT)拍摄的真实照片与本模拟并排比较。再按4可切换目标。"));
        }

        static readonly float[] SpinPresets = { 0f, 0.5f, 0.9f, 0.998f };

        public void CycleSpin()
        {
            if (controller == null) return;
            // Advance to the next preset above the current value (wraps to 0).
            int next = 0;
            for (int i = 0; i < SpinPresets.Length; i++)
                if (Mathf.Abs(controller.spin - SpinPresets[i]) < 0.01f) { next = (i + 1) % SpinPresets.Length; break; }
            controller.SetSpin(SpinPresets[next]);

            float a = controller.spin;
            if (a < 0.001f)
            {
                ShowToast(Loc.T("스핀 a = 0 — 슈바르츠실트 (비회전)", "Spin a = 0 — Schwarzschild (non-rotating)",
                                "スピン a = 0 — シュヴァルツシルト（非回転）", "自旋 a = 0 — 史瓦西（不旋转）"));
            }
            else
            {
                string aS = a.ToString("0.###");
                string h = BlackHoleController.HorizonRadiusM(a).ToString("0.00");
                string isco = BlackHoleController.IscoRadiusM(a).ToString("0.00");
                ShowToast(Loc.T(
                    "스핀 a = " + aS + " M — 지평선 r₊ = " + h + "M · ISCO " + isco + "M (원반이 안쪽으로!)",
                    "Spin a = " + aS + " M — horizon r₊ = " + h + "M · ISCO " + isco + "M (disk creeps inward!)",
                    "スピン a = " + aS + " M — 地平面 r₊ = " + h + "M · ISCO " + isco + "M（円盤が内側へ！）",
                    "自旋 a = " + aS + " M — 视界 r₊ = " + h + "M · ISCO " + isco + "M（吸积盘向内！）"));
            }
            if (panel != null) panel.RefreshText();
        }

        [Header("Camera")]
        public float orbitSensitivity = 0.25f;
        [Tooltip("Zoom factor per scroll notch (multiplicative).")]
        public float zoomFactor = 0.86f;
        // Symmetric range: looking up at the disk from below is one of the
        // best views (the old -2° floor made downward drag feel broken).
        public Vector2 pitchLimits = new Vector2(-70f, 70f);
        public Vector2 distanceLimits = new Vector2(3.2f, 40f);

        float yaw, pitch, distance;
        Vector3 initialPos;
        Quaternion initialRot;
        bool immersive;

        /// <summary>Full immersion: hides every overlay and label at once (U key).</summary>
        // ---- toolbar entry points (the click-only UI calls these; the guard
        // logic that used to live in the hotkey reader lives here now) --------
        /// <summary>The lens magnifier switches the disk off and owns the
        /// Einstein-ring star. It is a toggle, not a cinematic, so it does not
        /// block a narrated experience — but leaving it on under one narrates
        /// "the glowing disk" over an empty hole. Every experience clears it.</summary>
        void ClearLens() { if (lensDemo != null && lensDemo.Active) lensDemo.Toggle(); }

        public void ToggleTour()
        {
            if (tour == null) return;
            if (tour.Running) tour.StopTour();
            else if (!CinematicBusy) { ClearLens(); tour.StartTour(); }
        }
        public void PlayIntro() { if (intro != null && !CinematicBusy) { ClearLens(); intro.Play(); } }
        public void BeginFallIn() { if (fallIn != null && !CinematicBusy) { ClearLens(); fallIn.Begin(); } }
        public void BeginMerger() { if (binary != null && !CinematicBusy) { ClearLens(); binary.Begin(); } }
        public void ToggleLabels() { if (annotations != null) annotations.showLabels = !annotations.showLabels; }
        public void TogglePanel() { if (panel != null) { panel.show = !panel.show; panel.RefreshText(); } }
        public void ToggleHud() { if (hud != null) hud.show = !hud.show; }
        public void ToggleTheory() { if (theory != null) theory.Toggle(); }
        public void ToggleMute() { if (audioScape != null) audioScape.muted = !audioScape.muted; }
        public void FirePhotons() { if (launcher != null) launcher.ToggleSweep(); }
        public bool TourRunning => tour != null && tour.Running;
        public bool Immersive => immersive;
        public void TourNext() { if (tour != null && tour.Running) tour.Next(); }
        public void TourPrev() { if (tour != null && tour.Running) tour.Prev(); }
        public static void LoadScene(string s) => UnityEngine.SceneManagement.SceneManager.LoadScene(s);

        public void SetImmersive(bool on)
        {
            immersive = on;
            if (panel != null) { panel.show = !on; panel.RefreshText(); }
            if (comparison != null && on) { comparison.show = false; comparison.Refresh(); }
            if (annotations != null) annotations.showLabels = !on;
            if (theory != null && on) theory.SetVisible(false);
            // Cinematics put their skip/stop button in the same corner.
            LanguageSelect.SetVisible(!on);
            // MR's hand menu shares the bottom strip with the captions.
            MRControls.SetVisible(!on);
            // The only way back once the toolbar is hidden.
            if (on) ImmersiveHint.Show(() => SetImmersive(false)); else ImmersiveHint.Hide();
        }

        void Start()
        {
            initialPos = transform.position;
            initialRot = transform.rotation;
            SyncFromTransform();
            // The old keyboard-legend help bar is gone — DesktopToolbar's
            // clickable buttons are the control surface now.

            // Theory (수식) panel lives on the camera, wired from our own refs
            // so the saved scene needs no changes.
            theory = GetComponent<TheoryPanel>();
            if (theory == null) theory = gameObject.AddComponent<TheoryPanel>();
            theory.tour = tour;
            theory.einstein = einsteinDemo;
            theory.spaghetti = spaghetti;
            theory.jets = jets;
            theory.launcher = launcher;
            theory.controller = controller;

            binary = GetComponent<BinaryMergerCinematic>();
            if (binary == null) binary = gameObject.AddComponent<BinaryMergerCinematic>();
            binary.controller = controller;
            binary.controls = this;
            theory.binary = binary;

            SetupPostFX();

            // Always-available language selector in the top-right corner.
            if (Application.isPlaying) LanguageSelect.CreateWidget();

            // (The XR Interaction Simulator is swept scene-wide by
            // XRSimulatorSceneGuard — any scene without an XROrigin loses it.
            // No per-scene guard needed here.)
        }

        /// <summary>Gentle bloom so the HDR disk (and GW rings) actually glow.
        /// Built at runtime — the saved scene stays untouched.</summary>
        void SetupPostFX()
        {
            if (!Application.isPlaying) return;
            // Passthrough composites on the alpha channel, and post-processing
            // overwrites it — enabling this in MR punches the room out of view.
            if (xrMode) return;
            // DontSave objects survive play-mode exit in the editor — sweep
            // strays so volumes never stack across sessions.
            foreach (var v in Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Volume>())
                if (v != null && v.gameObject.name == "BlackHole PostFX")
                    DestroyImmediate(v.gameObject);
            var camData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;

            var go = new GameObject("BlackHole PostFX") { hideFlags = HideFlags.DontSave };
            var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>();
            bloom.intensity.Override(0.7f);
            bloom.threshold.Override(1.05f);  // only HDR content blooms; UI text stays crisp
            bloom.scatter.Override(0.62f);
            vol.profile = profile;
        }

        void SyncFromTransform()
        {
            if (target == null) return;
            Vector3 offset = transform.position - target.position;
            distance = offset.magnitude;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(distance, 0.001f), -1f, 1f)) * Mathf.Rad2Deg;
        }

        void Update()
        {
            // The tour and cinematics narrate themselves — no explain cards.
            ExplainCard.Suppress = (tour != null && tour.Running) || CinematicBusy || immersive;
            ReadHotkeys();
            ReadMouse();
        }

        void ReadMouse()
        {
            if (target == null || suspendCamera || xrMode) return;
            float dx = 0f, dy = 0f, scroll = 0f;
            bool dragging = false, zoomIn = false, zoomOut = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                dragging = mouse.rightButton.isPressed;
                var d = mouse.delta.ReadValue();
                dx = d.x; dy = d.y;
                scroll = mouse.scroll.ReadValue().y;
                // Windows reports ±120 per notch, some devices ±1. Normalize.
                if (Mathf.Abs(scroll) > 10f) scroll /= 120f;
            }
#else
            dragging = Input.GetMouseButton(1);
            dx = Input.GetAxis("Mouse X") * 12f;
            dy = Input.GetAxis("Mouse Y") * 12f;
            scroll = Input.mouseScrollDelta.y;
#endif
            // Mobile browsers: one-finger drag orbits, two-finger pinch zooms.
            if (TouchOrbit.Dragging)
            {
                dragging = true;
                dx += TouchOrbit.DragDelta.x; dy += TouchOrbit.DragDelta.y;
            }
            scroll += TouchOrbit.PinchNotches;
            bool zooming = !Mathf.Approximately(scroll, 0f) || zoomIn || zoomOut;
            if (!dragging && !zooming) return;

            // The cinematic orbit is never paused: each input frame re-syncs
            // from the transform (which the orbit advanced last LateUpdate)
            // and layers the user's deltas on top — the view keeps drifting
            // even while dragging.
            SyncFromTransform();

            if (dragging)
            {
                yaw += dx * orbitSensitivity;
                pitch = Mathf.Clamp(pitch + dy * orbitSensitivity, pitchLimits.x, pitchLimits.y);
            }

            // Multiplicative zoom: each notch scales distance by zoomFactor,
            // so it feels equally fast whether near or far. W/S = smooth zoom.
            if (!Mathf.Approximately(scroll, 0f))
                distance *= Mathf.Pow(zoomFactor, scroll);
            if (zoomIn) distance *= 1f - 1.4f * Time.deltaTime;
            if (zoomOut) distance *= 1f + 1.4f * Time.deltaTime;
            // The near limit scales with the hole (2.4 Rs) instead of being a
            // fixed world distance — otherwise small mass presets keep the
            // camera tens of Rs away and the observer-clock dilation never
            // visibly drops. At 2.4 Rs the clock reads ×0.76.
            float minDist = target != null ? Mathf.Max(2.4f * target.lossyScale.x, 0.35f) : distanceLimits.x;
            distance = Mathf.Clamp(distance, minDist, distanceLimits.y);

            float pr = pitch * Mathf.Deg2Rad, yr = yaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), Mathf.Cos(yr) * Mathf.Cos(pr));
            transform.position = target.position + dir * distance;
            transform.LookAt(target.position + Vector3.up * 0.1f);
        }

        /// <summary>
        /// True while a narrated cinematic owns the shared visual state. The
        /// presets and phenomenon demos write the same values the cinematics
        /// animate (disk brightness, hole scale, spin) with no owner — a toggle
        /// mid-fade captures a half-faded value as its "base" and restores the
        /// wrong thing. The tour is deliberately NOT included: it invites preset
        /// changes in its own hints, and resets its demos on every step.
        /// </summary>
        bool VisualsOwnedByCinematic =>
            (intro != null && intro.IsPlaying) ||
            (fallIn != null && fallIn.IsFalling) ||
            (binary != null && binary.Running);

        /// <summary>Every feature is a toolbar button now — the only keys left
        /// are the arrows, kept as a convenience for stepping a running tour
        /// (they don't clash with browser shortcuts). Everything else moved to
        /// <see cref="DesktopToolbar"/> so WebGL never fights the browser.</summary>
        void ReadHotkeys()
        {
            // Esc leaves immersive view — the only way back once the toolbar (and
            // its toggle) is hidden.
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && immersive) { SetImmersive(false); return; }
#else
            if (Input.GetKeyDown(KeyCode.Escape) && immersive) { SetImmersive(false); return; }
#endif
            if (tour == null || !tour.Running) return;
#if ENABLE_INPUT_SYSTEM
            if (kb == null) return;
            if (kb.rightArrowKey.wasPressedThisFrame) tour.Next();
            if (kb.leftArrowKey.wasPressedThisFrame) tour.Prev();
#else
            if (Input.GetKeyDown(KeyCode.RightArrow)) tour.Next();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) tour.Prev();
#endif
        }

        UnityEngine.UI.Text toast;
        Coroutine toastRoutine;

        void Snapshot()
        {
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Snapshots"));
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "blackhole_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            ShowToast(Loc.T("스냅샷 저장됨 — ", "Snapshot saved — ", "スナップショット保存 — ", "截图已保存 — ")
                      + "Snapshots/" + System.IO.Path.GetFileName(file));
        }

        void ShowToast(string message)
        {
            if (toast == null)
            {
                var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
                var bar = BlackHoleUI.MakePanel(canvas.transform, "Toast",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(620f, 46f),
                    accentLine: false);
                toast = BlackHoleUI.MakeText(bar, "Text", 17, BlackHoleUI.TitleGold, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 38f));
            }
            toast.text = message;
            toast.transform.parent.gameObject.SetActive(true);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToast());
        }

        System.Collections.IEnumerator HideToast()
        {
            yield return new WaitForSeconds(2.5f);
            if (toast != null) toast.transform.parent.gameObject.SetActive(false);
        }

        public void ResetCamera()
        {
            // In MR the pose comes from head tracking; writing it here would be
            // overwritten next frame at best, and fight the tracking at worst.
            if (xrMode) return;
            transform.position = initialPos;
            transform.rotation = initialRot;
            if (autoOrbit != null) autoOrbit.enabled = true;
        }


    }
}
