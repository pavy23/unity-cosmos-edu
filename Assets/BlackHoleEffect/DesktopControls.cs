using UnityEngine;
using UnityEngine.Rendering.Universal; // camera post-processing toggle
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlackHoleEffect
{
    /// <summary>
    /// Keyboard + mouse control for the desktop showcase (play mode).
    ///
    ///   우클릭 드래그  카메라 궤도 회전      휠           줌
    ///   1             원반 색 순환          2            질량 순환
    ///   Space         광자 발사/지우기 토글
    ///   E             아인슈타인 링         A / D        데모 별 이동
    ///   L             라벨                  I            물리 패널
    ///   O             관측사진 비교         H            도움말
    ///   R             카메라 리셋 (자동 궤도 재개)
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

        TheoryPanel theory;
        BinaryMergerCinematic binary;

        /// <summary>True while any narrated experience owns the stage —
        /// used to block competing cinematics (and the intro autoplay).</summary>
        public bool CinematicBusy =>
            (intro != null && intro.IsPlaying) ||
            (fallIn != null && fallIn.IsFalling) ||
            (binary != null && binary.Running) ||
            (tour != null && tour.Running);

        void CycleDifficulty()
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

        void ToggleLanguage()
        {
            Loc.Cycle();
            UpdateHelpText();
            if (tour != null) tour.OnLanguageChanged();
            if (panel != null) panel.RefreshText();
            ShowToast(Loc.T("언어: ", "Language: ", "言語: ", "语言: ") + Loc.DisplayName);
        }

        // ---- one-key preset cycles (1 = disk colors, 2 = mass) ----------

        void CycleColor()
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

        void CycleMass()
        {
            if (panel == null) return;
            massIndex = (massIndex + 1) % 3;
            panel.SetMassPreset((BlackHolePhysicsPanel.MassPreset)massIndex);
            ShowToast(Loc.T("질량: ", "Mass: ", "質量: ", "质量: ")
                + Loc.T(panel.massLabel, panel.massLabelEn, panel.massLabelJa, panel.massLabelZh));
        }

        // ---- phenomenon toggles: every ON shows an explanation card ------

        void ToggleEinstein()
        {
            if (einsteinDemo == null) return;
            einsteinDemo.active = !einsteinDemo.active;
            if (!einsteinDemo.active) return;
            ExplainCard.Show(
                Loc.T("아인슈타인 링", "Einstein Ring", "アインシュタインリング", "爱因斯坦环"),
                Loc.T("블랙홀 뒤의 별빛이 중력에 휘어 두 개의 상으로 갈라져 보입니다. 별이 정확히 뒤에 정렬되는 순간, 빛이 사방으로 휘어 완전한 고리가 됩니다. A/D로 별을 직접 움직여 보세요.",
                      "Light from a star behind the hole is split into two images. At exact alignment the light bends around every side at once and becomes a complete ring. Move the star yourself with A/D.",
                      "ブラックホールの後ろの星の光は、ふたつの像に分かれて見えます。星が真後ろに整列した瞬間、光が全方向に曲がり、完全なリングになります。A/Dで星を動かせます。",
                      "黑洞后方的星光被分成两个像。当恰好对齐时，光从四面八方弯过来，成为完整的光环。可用A/D亲自移动星星。"));
        }

        void ToggleSpaghetti()
        {
            if (spaghetti == null) return;
            spaghetti.active = !spaghetti.active;
            if (!spaghetti.active) return;
            ExplainCard.Show(
                Loc.T("스파게티화 — 조석력", "Spaghettification — Tidal Force", "スパゲッティ化 — 潮汐力", "面条化 — 潮汐力"),
                Loc.T("별의 머리와 발에 걸리는 중력 차이(조석력)가 별을 국수처럼 길게 늘여 찢습니다. 찢긴 물질은 두 갈래로 갈라져, 절반은 빨려 들고 절반은 튕겨 나갑니다.",
                      "The difference in gravity across the star — the tidal force — stretches it like a noodle and tears it apart. The debris splits into two streams: half falls in, half is flung away.",
                      "星の両端にかかる重力の差（潮汐力）が、星を麺のように引き伸ばして引き裂きます。物質はふたつの流れに分かれ、半分は吸い込まれ、半分ははじき出されます。",
                      "恒星两端受到的引力差——潮汐力——把它像面条一样拉长撕裂。碎片分成两股：一半落入黑洞，一半被甩出去。"));
        }

        void ToggleJets()
        {
            if (jets == null) return;
            jets.active = !jets.active;
            if (!jets.active) return;
            ExplainCard.Show(
                Loc.T("상대론적 제트", "Relativistic Jets", "相対論的ジェット", "相对论性喷流"),
                Loc.T("빨려 들던 물질의 일부는 삼켜지지 않습니다. 회전하는 자기장에 감겨 올라가 양극에서 거의 광속으로 뿜어져 나가고, 실제 우주에서는 수천 광년까지 뻗어 나갑니다.",
                      "Not everything falling in gets swallowed. Some matter is wound up by rotating magnetic fields and blasted from the poles at nearly light speed — real jets stretch for thousands of light-years.",
                      "落ち込む物質のすべてが呑み込まれるわけではありません。一部は回転する磁場に巻き上げられ、両極からほぼ光速で噴き出します。実際のジェットは数千光年まで伸びます。",
                      "并非所有下落的物质都被吞掉。一部分被旋转磁场卷起，从两极以接近光速喷出——真实的喷流可延伸数千光年。"));
        }

        void ToggleLightCurve()
        {
            if (lightCurve == null) return;
            lightCurve.show = !lightCurve.show;
            if (!lightCurve.show) return;
            ExplainCard.Show(
                Loc.T("광도 곡선", "Light Curve", "光度曲線", "光变曲线"),
                Loc.T("원반 전체의 밝기를 시간에 따라 기록한 그래프입니다. 실제 망원경이 블랙홀을 '보는' 방법이죠. T로 별을 찢어 보세요 — 밝기가 치솟는 조석파괴사건(TDE)이 그래프에 나타납니다.",
                      "A record of the disk's total brightness over time — this is how real telescopes 'see' black holes. Try tearing a star apart (T): a tidal disruption flare will spike the curve.",
                      "円盤全体の明るさを時間で記録したグラフです。実際の望遠鏡はこうやってブラックホールを『見て』います。Tで星を裂いてみてください — 潮汐破壊イベントの増光が現れます。",
                      "记录吸积盘总亮度随时间变化的图线——真实望远镜正是这样'看'黑洞的。试试按T撕裂恒星：曲线上会出现潮汐瓦解耀发。"));
        }

        void ToggleLens()
        {
            if (lensDemo == null) return;
            bool wasOn = lensDemo.Active;
            lensDemo.Toggle();
            if (wasOn) return;
            ExplainCard.Show(
                Loc.T("중력 렌즈", "Gravitational Lensing", "重力レンズ", "引力透镜"),
                Loc.T("원반을 잠시 꺼서, 배경 별빛이 중력만으로 어떻게 왜곡되는지 봅니다. 밝은 배경 광원이 좌우로 오가며 상이 갈라지고 고리로 이어지는 것을 관찰해 보세요.",
                      "The disk is switched off so you can watch background starlight warp under gravity alone. Watch the bright source sweep by: its image splits, stretches, and closes into rings.",
                      "円盤を一時オフにして、背景の星の光が重力だけでどう歪むかを見ます。明るい光源が左右に動くと、像が分かれ、伸び、リングにつながります。",
                      "暂时关闭吸积盘，只看背景星光如何被引力扭曲。观察明亮光源左右移动时，像被分裂、拉伸、连成光环。"));
        }

        void CycleComparison()
        {
            if (comparison == null) return;
            bool wasOff = !comparison.show;
            comparison.CycleMode();
            if (wasOff && comparison.show)
                ExplainCard.Show(
                    Loc.T("실제 관측과 비교", "Compare with Real Observations", "実際の観測と比較", "与真实观测对比"),
                    Loc.T("사건의 지평선 망원경(EHT)이 촬영한 실제 블랙홀 사진과 이 시뮬레이션을 나란히 비교합니다. O를 다시 누르면 대상이 바뀝니다.",
                          "The simulation side by side with the real Event Horizon Telescope images. Press O again to switch targets.",
                          "イベント・ホライズン・テレスコープ(EHT)が撮影した実際の写真と、このシミュレーションを並べて比較します。Oでターゲットが切り替わります。",
                          "把事件视界望远镜(EHT)拍摄的真实照片与本模拟并排比较。再按O可切换目标。"));
        }

        static readonly float[] SpinPresets = { 0f, 0.5f, 0.9f, 0.998f };

        void CycleSpin()
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
        UnityEngine.UI.Text help;
        GameObject helpBar;
        bool showHelp = true;
        bool immersive;

        /// <summary>Full immersion: hides every overlay and label at once (U key).</summary>
        public void SetImmersive(bool on)
        {
            immersive = on;
            showHelp = !on;
            if (panel != null) { panel.show = !on; panel.RefreshText(); }
            if (comparison != null && on) { comparison.show = false; comparison.Refresh(); }
            if (annotations != null) annotations.showLabels = !on;
            if (theory != null && on) theory.SetVisible(false);
        }

        void Start()
        {
            initialPos = transform.position;
            initialRot = transform.rotation;
            SyncFromTransform();
            BuildHelp();

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
        }

        /// <summary>Gentle bloom so the HDR disk (and GW rings) actually glow.
        /// Built at runtime — the saved scene stays untouched.</summary>
        void SetupPostFX()
        {
            if (!Application.isPlaying) return;
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
            if (helpBar != null) helpBar.SetActive(showHelp && (tour == null || !tour.Running));
        }

        void ReadMouse()
        {
            if (target == null || suspendCamera) return;
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
            var kb = Keyboard.current;
            if (kb != null)
            {
                zoomIn = kb.wKey.isPressed;
                zoomOut = kb.sKey.isPressed;
            }
#else
            dragging = Input.GetMouseButton(1);
            dx = Input.GetAxis("Mouse X") * 12f;
            dy = Input.GetAxis("Mouse Y") * 12f;
            scroll = Input.mouseScrollDelta.y;
            zoomIn = Input.GetKey(KeyCode.W);
            zoomOut = Input.GetKey(KeyCode.S);
#endif
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

        void ReadHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) CycleColor();
            if (kb.digit2Key.wasPressedThisFrame) CycleMass();
            if (kb.eKey.wasPressedThisFrame) ToggleEinstein();
            if (kb.lKey.wasPressedThisFrame && annotations != null) annotations.showLabels = !annotations.showLabels;
            if (kb.iKey.wasPressedThisFrame && panel != null) { panel.show = !panel.show; panel.RefreshText(); }
            if (kb.oKey.wasPressedThisFrame) CycleComparison();
            if (kb.f1Key.wasPressedThisFrame && hud != null) hud.show = !hud.show;
            if (kb.f2Key.wasPressedThisFrame) CycleDifficulty();
            if (kb.f3Key.wasPressedThisFrame) ToggleLens();
            if (kb.f4Key.wasPressedThisFrame && (binary == null || !binary.Running)) CycleSpin();
            // One cinematic at a time — F5/F6/F7 are ignored while another runs.
            if (!CinematicBusy)
            {
                if (kb.f7Key.wasPressedThisFrame && binary != null) binary.Begin();
                if (kb.f5Key.wasPressedThisFrame && intro != null) intro.Play();
                if (kb.f6Key.wasPressedThisFrame && fallIn != null) fallIn.Begin();
            }
            if (kb.vKey.wasPressedThisFrame) ToggleLightCurve();
            if (kb.f12Key.wasPressedThisFrame) Snapshot();
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
            if (kb.uKey.wasPressedThisFrame) SetImmersive(!immersive);
            if (kb.rKey.wasPressedThisFrame) ResetCamera();
            if (kb.tKey.wasPressedThisFrame) ToggleSpaghetti();
            if (kb.jKey.wasPressedThisFrame) ToggleJets();
            if (kb.mKey.wasPressedThisFrame && audioScape != null) audioScape.muted = !audioScape.muted;
            if (kb.xKey.wasPressedThisFrame && theory != null) theory.Toggle();
            if (kb.kKey.wasPressedThisFrame) ToggleLanguage();
            if (tour != null)
            {
                if (kb.gKey.wasPressedThisFrame) { if (tour.Running) tour.StopTour(); else tour.StartTour(); }
                if (kb.nKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) tour.Next();
                if (kb.bKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) tour.Prev();
            }
            if (einsteinDemo != null && einsteinDemo.active)
            {
                if (kb.aKey.isPressed) einsteinDemo.Nudge(-12f * Time.deltaTime);
                if (kb.dKey.isPressed) einsteinDemo.Nudge(12f * Time.deltaTime);
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) CycleColor();
            if (Input.GetKeyDown(KeyCode.Alpha2)) CycleMass();
            if (Input.GetKeyDown(KeyCode.E)) ToggleEinstein();
            if (Input.GetKeyDown(KeyCode.L) && annotations != null) annotations.showLabels = !annotations.showLabels;
            if (Input.GetKeyDown(KeyCode.I) && panel != null) { panel.show = !panel.show; panel.RefreshText(); }
            if (Input.GetKeyDown(KeyCode.O)) CycleComparison();
            if (Input.GetKeyDown(KeyCode.F1) && hud != null) hud.show = !hud.show;
            if (Input.GetKeyDown(KeyCode.F2)) CycleDifficulty();
            if (Input.GetKeyDown(KeyCode.F3)) ToggleLens();
            if (Input.GetKeyDown(KeyCode.F4) && (binary == null || !binary.Running)) CycleSpin();
            if (!CinematicBusy)
            {
                if (Input.GetKeyDown(KeyCode.F7) && binary != null) binary.Begin();
                if (Input.GetKeyDown(KeyCode.F5) && intro != null) intro.Play();
                if (Input.GetKeyDown(KeyCode.F6) && fallIn != null) fallIn.Begin();
            }
            if (Input.GetKeyDown(KeyCode.V)) ToggleLightCurve();
            if (Input.GetKeyDown(KeyCode.F12)) Snapshot();
            if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.U)) SetImmersive(!immersive);
            if (Input.GetKeyDown(KeyCode.R)) ResetCamera();
            if (Input.GetKeyDown(KeyCode.T)) ToggleSpaghetti();
            if (Input.GetKeyDown(KeyCode.J)) ToggleJets();
            if (Input.GetKeyDown(KeyCode.M) && audioScape != null) audioScape.muted = !audioScape.muted;
            if (Input.GetKeyDown(KeyCode.X) && theory != null) theory.Toggle();
            if (Input.GetKeyDown(KeyCode.K)) ToggleLanguage();
            if (tour != null)
            {
                if (Input.GetKeyDown(KeyCode.G)) { if (tour.Running) tour.StopTour(); else tour.StartTour(); }
                if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) tour.Next();
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftArrow)) tour.Prev();
            }
            if (einsteinDemo != null && einsteinDemo.active)
            {
                if (Input.GetKey(KeyCode.A)) einsteinDemo.Nudge(-12f * Time.deltaTime);
                if (Input.GetKey(KeyCode.D)) einsteinDemo.Nudge(12f * Time.deltaTime);
            }
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

        void ResetCamera()
        {
            transform.position = initialPos;
            transform.rotation = initialRot;
            if (autoOrbit != null) autoOrbit.enabled = true;
        }

        void BuildHelp()
        {
            var canvas = BlackHoleUI.EnsureCanvas(GetComponent<Camera>());
            // Two compact rows + wrap; the bar height then hugs whatever the
            // current language actually needs (see UpdateHelpText).
            var bar = BlackHoleUI.MakePanel(canvas.transform, "Help Bar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1600f, 62f),
                accentLine: false);
            helpBar = bar.gameObject;

            help = BlackHoleUI.MakeText(bar, "Help Text", 15, BlackHoleUI.TextSecondary, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 54f));
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
            UpdateHelpText();
        }

        static string Key(string k) => "<color=#FFC46E>" + k + "</color> ";
        static string Cat(string s) => "<color=#6F7A8C><b>" + s + "</b></color>   ";

        void UpdateHelpText()
        {
            if (help == null) return;
            // Four category rows: controls / experiences / black-hole setup /
            // phenomenon toggles — matching how the features are actually used.
            help.text = Loc.T(
                Cat("조작") + Key("우클릭") + "회전  " + Key("휠·W/S") + "줌  " + Key("R") + "리셋  " + Key("U") + "몰입  "
                    + Key("M") + "소리  " + Key("K") + "언어  " + Key("F12") + "스냅샷  " + Key("F1") + "성능  " + Key("H") + "도움말\n"
                + Cat("체험") + Key("G") + "가이드 투어(N/B)  " + Key("F5") + "블랙홀 탄생  " + Key("F6") + "낙하 체험  " + Key("F7") + "블랙홀 병합\n"
                + Cat("블랙홀") + Key("1") + "원반 색상  " + Key("2") + "질량  " + Key("F4") + "스핀  " + Key("F2") + "설명 난이도  "
                    + Key("L") + "라벨  " + Key("I") + "정보 패널  " + Key("X") + "수식  " + Key("O") + "관측사진\n"
                + Cat("현상") + Key("Space") + "광자 발사/지우기  " + Key("E") + "아인슈타인 링(A/D)  " + Key("T") + "스파게티화  "
                    + Key("J") + "제트  " + Key("F3") + "렌즈  " + Key("V") + "광도곡선",

                Cat("Controls") + Key("RMB") + "orbit  " + Key("Wheel·W/S") + "zoom  " + Key("R") + "reset  " + Key("U") + "immersive  "
                    + Key("M") + "sound  " + Key("K") + "language  " + Key("F12") + "snapshot  " + Key("F1") + "perf  " + Key("H") + "help\n"
                + Cat("Experiences") + Key("G") + "guided tour(N/B)  " + Key("F5") + "birth of a hole  " + Key("F6") + "fall in  " + Key("F7") + "merger\n"
                + Cat("Black hole") + Key("1") + "disk colors  " + Key("2") + "mass  " + Key("F4") + "spin  " + Key("F2") + "level  "
                    + Key("L") + "labels  " + Key("I") + "info panel  " + Key("X") + "math  " + Key("O") + "EHT photo\n"
                + Cat("Phenomena") + Key("Space") + "photons fire/clear  " + Key("E") + "Einstein ring(A/D)  " + Key("T") + "spaghettify  "
                    + Key("J") + "jets  " + Key("F3") + "lens  " + Key("V") + "light curve",

                Cat("操作") + Key("右ドラッグ") + "回転  " + Key("ホイール·W/S") + "ズーム  " + Key("R") + "リセット  " + Key("U") + "没入  "
                    + Key("M") + "音  " + Key("K") + "言語  " + Key("F12") + "撮影  " + Key("F1") + "性能  " + Key("H") + "ヘルプ\n"
                + Cat("体験") + Key("G") + "ガイドツアー(N/B)  " + Key("F5") + "誕生  " + Key("F6") + "落下体験  " + Key("F7") + "合体\n"
                + Cat("ブラックホール") + Key("1") + "円盤の色  " + Key("2") + "質量  " + Key("F4") + "スピン  " + Key("F2") + "難易度  "
                    + Key("L") + "ラベル  " + Key("I") + "パネル  " + Key("X") + "数式  " + Key("O") + "観測写真\n"
                + Cat("現象") + Key("Space") + "光子 発射/消去  " + Key("E") + "アインシュタインリング(A/D)  " + Key("T") + "スパゲッティ化  "
                    + Key("J") + "ジェット  " + Key("F3") + "レンズ  " + Key("V") + "光度曲線",

                Cat("操作") + Key("右键") + "旋转  " + Key("滚轮·W/S") + "缩放  " + Key("R") + "重置  " + Key("U") + "沉浸  "
                    + Key("M") + "声音  " + Key("K") + "语言  " + Key("F12") + "截图  " + Key("F1") + "性能  " + Key("H") + "帮助\n"
                + Cat("体验") + Key("G") + "导览(N/B)  " + Key("F5") + "黑洞诞生  " + Key("F6") + "坠落体验  " + Key("F7") + "黑洞并合\n"
                + Cat("黑洞") + Key("1") + "盘颜色  " + Key("2") + "质量  " + Key("F4") + "自旋  " + Key("F2") + "难度  "
                    + Key("L") + "标签  " + Key("I") + "面板  " + Key("X") + "公式  " + Key("O") + "观测照片\n"
                + Cat("现象") + Key("Space") + "光子 发射/清除  " + Key("E") + "爱因斯坦环(A/D)  " + Key("T") + "面条化  "
                    + Key("J") + "喷流  " + Key("F3") + "透镜  " + Key("V") + "光变曲线");

            // Size the bar to the language: preferredHeight accounts for the
            // wrapped line count at the current rect width.
            if (helpBar != null)
            {
                var barRt = helpBar.GetComponent<RectTransform>();
                float h = Mathf.Max(help.preferredHeight, 24f);
                help.rectTransform.sizeDelta = new Vector2(help.rectTransform.sizeDelta.x, h);
                barRt.sizeDelta = new Vector2(barRt.sizeDelta.x, h + 20f);
            }
        }
    }
}
