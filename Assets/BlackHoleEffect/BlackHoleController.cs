using UnityEngine;

namespace BlackHoleEffect
{
    /// <summary>
    /// Educational control panel for the raymarched black hole. Writes to the
    /// sharedMaterial directly (MaterialPropertyBlock overrides are ignored by
    /// the SRP Batcher for UnityPerMaterial properties).
    /// GameObject uniform scale = Schwarzschild radius in metres.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class BlackHoleController : MonoBehaviour
    {
        public enum DiskPreset
        {
            Gargantua,   // 인터스텔라 스타일 — 따뜻한 크림/주황
            RedGiant,    // 저온 강착원반 — 깊은 적색
            BlueQuasar,  // 고온 퀘이사 — 청백색
            Custom
        }

        public enum QualityProfile
        {
            DesktopHigh,    // 220 스텝 — PC/시연용
            QuestOptimized, // 96 스텝 — Quest/MR 기기용
            Custom
        }

        [Header("Presets")]
        public DiskPreset preset = DiskPreset.Gargantua;
        public QualityProfile quality = QualityProfile.DesktopHigh;

        [Header("Accretion Disk")]
        [Range(1.3f, 6f)] public float diskInnerRadius = 2.9f;
        [Range(4f, 14f)] public float diskOuterRadius = 8.6f;
        [Range(0f, 40f)] public float diskBrightness = 9.5f;
        [Range(0.5f, 1.8f)] public float diskTemperature = 1.06f;
        [Range(0f, 3f)] public float flowSpeed = 1.6f;
        [Range(0f, 1f)] public float dopplerBeaming = 0.72f;
        [Range(1f, 14f)] public float turbulenceDetail = 4.2f;
        [Range(0f, 1f)] public float turbulenceContrast = 0.3f;
        public Color diskTint = Color.white;

        [Header("Kerr Spin")]
        [Tooltip("Spin parameter a in units of M (0 = Schwarzschild, 0.998 = near-maximal). The disk inner edge follows the prograde ISCO.")]
        [Range(0f, 0.998f)] public float spin;

        [Header("Background")]
        [Range(0f, 2f)] public float starDensity = 0.12f;
        [Range(0f, 1f)] public float nebulaHaze = 0.15f;

        [Header("Quality")]
        [Range(64, 384)] public int raymarchSteps = 220;

        static readonly int DiskInnerId = Shader.PropertyToID("_DiskInner");
        static readonly int DiskOuterId = Shader.PropertyToID("_DiskOuter");
        static readonly int DiskBrightnessId = Shader.PropertyToID("_DiskBrightness");
        static readonly int DiskTempId = Shader.PropertyToID("_DiskTemp");
        static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        static readonly int DopplerId = Shader.PropertyToID("_Doppler");
        static readonly int DiskDetailId = Shader.PropertyToID("_DiskDetail");
        static readonly int DiskContrastId = Shader.PropertyToID("_DiskContrast");
        static readonly int DiskTintId = Shader.PropertyToID("_DiskTint");
        static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
        static readonly int NebulaId = Shader.PropertyToID("_NebulaIntensity");
        static readonly int StepsId = Shader.PropertyToID("_Steps");
        static readonly int SpinId = Shader.PropertyToID("_Spin");
        static readonly int BinaryOnId = Shader.PropertyToID("_BinaryOn");

        Renderer cachedRenderer;
        DiskPreset appliedPreset = DiskPreset.Custom;
        QualityProfile appliedQuality = QualityProfile.Custom;

        void OnEnable()
        {
            appliedPreset = preset;
            appliedQuality = quality;
            Apply();
        }

        void OnValidate()
        {
            if (preset != appliedPreset)
            {
                ApplyPresetValues(preset);
                appliedPreset = preset;
            }
            if (quality != appliedQuality)
            {
                ApplyQualityValues(quality);
                appliedQuality = quality;
            }
            Apply();
        }

        void ApplyPresetValues(DiskPreset p)
        {
            switch (p)
            {
                case DiskPreset.Gargantua:
                    diskBrightness = 9.5f; diskTemperature = 1.06f; dopplerBeaming = 0.72f;
                    turbulenceDetail = 4.2f; turbulenceContrast = 0.3f;
                    diskTint = Color.white;
                    break;
                case DiskPreset.RedGiant:
                    diskBrightness = 8f; diskTemperature = 0.78f; dopplerBeaming = 0.6f;
                    turbulenceDetail = 5.5f; turbulenceContrast = 0.45f;
                    diskTint = new Color(1f, 0.58f, 0.38f);
                    break;
                case DiskPreset.BlueQuasar:
                    diskBrightness = 10.5f; diskTemperature = 1.5f; dopplerBeaming = 0.8f;
                    turbulenceDetail = 4.2f; turbulenceContrast = 0.25f;
                    diskTint = new Color(0.55f, 0.75f, 1.55f);
                    break;
            }
        }

        void ApplyQualityValues(QualityProfile q)
        {
            switch (q)
            {
                case QualityProfile.DesktopHigh: raymarchSteps = 220; break;
                case QualityProfile.QuestOptimized: raymarchSteps = 96; break;
            }
        }

        public void Apply()
        {
            if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null) return;
            var mat = cachedRenderer.sharedMaterial;
            if (mat == null) return;

            // The quad is expanded far beyond its mesh in the vertex shader;
            // widen the culling bounds so it never gets frustum-culled.
            cachedRenderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 64f);

            // With spin, the disk genuinely reaches inward to the prograde
            // ISCO (in Rs units: ISCO(M units)/2). Never larger than the
            // user's configured inner radius.
            float effectiveInner = Mathf.Min(diskInnerRadius, Mathf.Max(0.7f, IscoRadiusM(spin) * 0.5f));
            mat.SetFloat(SpinId, spin);
            mat.SetFloat(DiskInnerId, effectiveInner);
            mat.SetFloat(DiskOuterId, Mathf.Max(diskOuterRadius, effectiveInner + 0.5f));
            mat.SetFloat(DiskBrightnessId, diskBrightness);
            mat.SetFloat(DiskTempId, diskTemperature);
            mat.SetFloat(FlowSpeedId, flowSpeed);
            mat.SetFloat(DopplerId, dopplerBeaming);
            mat.SetFloat(DiskDetailId, turbulenceDetail);
            mat.SetFloat(DiskContrastId, turbulenceContrast);
            mat.SetColor(DiskTintId, diskTint);
            mat.SetFloat(StarDensityId, starDensity);
            mat.SetFloat(NebulaId, nebulaHaze);
            mat.SetFloat(StepsId, raymarchSteps);

            // Binary mode is owned by BinaryMergerCinematic at runtime; make
            // sure an interrupted play session never leaves it baked into the
            // material asset.
            if (!Application.isPlaying) mat.SetFloat(BinaryOnId, 0f);
        }

        /// <summary>Runtime API for UI buttons in the educational app.</summary>
        public void SetPreset(DiskPreset p)
        {
            preset = p;
            ApplyPresetValues(p);
            appliedPreset = p;
            Apply();
        }

        public void SetQuality(QualityProfile q)
        {
            quality = q;
            ApplyQualityValues(q);
            appliedQuality = q;
            Apply();
        }

        public void SetSpin(float a)
        {
            spin = Mathf.Clamp(a, 0f, 0.998f);
            Apply();
        }

        /// <summary>Outer (event) horizon radius r+ in M units:
        /// r+ = M + √(M² − a²). 2M at a = 0, → 1M as a → 1.</summary>
        public static float HorizonRadiusM(float a)
        {
            return 1f + Mathf.Sqrt(Mathf.Max(1f - a * a, 0f));
        }

        /// <summary>Prograde ISCO radius in M units (Bardeen–Press–Teukolsky).
        /// 6M at a = 0, → ~1.24M at a = 0.998.</summary>
        public static float IscoRadiusM(float a)
        {
            a = Mathf.Clamp(a, 0f, 0.9999f);
            float z1 = 1f + Mathf.Pow(1f - a * a, 1f / 3f)
                     * (Mathf.Pow(1f + a, 1f / 3f) + Mathf.Pow(1f - a, 1f / 3f));
            float z2 = Mathf.Sqrt(3f * a * a + z1 * z1);
            return 3f + z2 - Mathf.Sqrt(Mathf.Max((3f - z1) * (3f + z1 + 2f * z2), 0f));
        }
    }
}
