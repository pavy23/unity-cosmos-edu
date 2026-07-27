using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using BlackHoleEffect; // Loc

namespace MilkyWay
{
    /// <summary>
    /// The MR orrery: the detailed solar system spawned as a room-scale
    /// exhibit piece (Neptune's orbit ≈ 1.2 m), floating at chest height.
    /// Mirrors the desktop <see cref="SolarSystemStage"/> ownership pattern —
    /// spawn once, keep forever — and adds what MR needs: name tags and a
    /// camera-facing highlight ring that follows an orbiting body.
    ///
    /// The scale-truth sequence that used to live here — the whole rig
    /// shrinking by √30.1 so Neptune's true orbit landed on its friendly-map
    /// one — is gone. Tapping a body for a close-up
    /// (<see cref="SolarSystemMRFocus"/>) is the interaction this exhibit is
    /// built around now, and the two fought over the rig's scale.
    /// </summary>
    public class SolarSystemMRStage : MonoBehaviour
    {
        [Tooltip("Rig-local Neptune orbit is 0.0604; scale 20 puts it at 1.21 m.")]
        public float rigScale = 20f;
        [Tooltip("Ambient orbit/spin speed (desktop stage uses 0.25).")]
        public float ambientMotionScale = 0.25f;
        public MRBodyLabels labels;

        public SolarSystemRig Rig { get; private set; }

        /// <summary>Sun outward. The index into this IS the index into
        /// <see cref="SolarSystemTour"/>'s Facts and NarrationLines and into the
        /// mw_sol_N clips — the focus card reads all three by position, so the
        /// order is a contract, not a convenience.</summary>
        public static readonly string[] BodyKeys =
            { "Sun", "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" };

        XRGrabInteractable grab;
        LineRenderer highlight;
        Material ringMat;
        Transform highlightTarget;

        void Awake()
        {
            Rig = SolarSystemRig.Spawn(transform.position, transform);
            Rig.transform.localScale = Vector3.one * rigScale;
            Rig.motionScale = ambientMotionScale;
            grab = GetComponent<XRGrabInteractable>();
            ringMat = new Material(Shader.Find("Sprites/Default"));
        }

        void Start()
        {
            BuildLabels();
        }

        void BuildLabels()
        {
            if (labels == null) return;
            labels.Init(Rig.transform);
            foreach (var key in BodyKeys)
            {
                var visual = Rig.GetBodyVisual(key);
                if (visual == null) continue;
                string k = key; // capture per body, not the loop variable
                labels.Add(visual, () => BodyName(k), 1.7f, 0.012f);
            }
        }

        public static string BodyName(string key) => key switch
        {
            "Sun"     => Loc.T("태양", "Sun", "太陽", "太阳"),
            "Mercury" => Loc.T("수성", "Mercury", "水星", "水星"),
            "Venus"   => Loc.T("금성", "Venus", "金星", "金星"),
            "Earth"   => Loc.T("지구", "Earth", "地球", "地球"),
            "Mars"    => Loc.T("화성", "Mars", "火星", "火星"),
            "Jupiter" => Loc.T("목성", "Jupiter", "木星", "木星"),
            "Saturn"  => Loc.T("토성", "Saturn", "土星", "土星"),
            "Uranus"  => Loc.T("천왕성", "Uranus", "天王星", "天王星"),
            "Neptune" => Loc.T("해왕성", "Neptune", "海王星", "海王星"),
            _ => key,
        };

        public bool Held => grab != null && grab.isSelected;

        public void SetLabelsVisible(bool on)
        {
            if (labels != null) labels.SetVisible(on);
        }

        public bool LabelsVisible => labels != null && labels.Visible;

        public void SetMotionScale(float s)
        {
            if (Rig != null) Rig.motionScale = s;
        }

        // ---------------- tour highlight ----------------------------------

        /// <summary>Ring a body. The ring follows the body around its orbit
        /// and faces the viewer (a flat ring collapses to a line edge-on at
        /// chest height).</summary>
        public void SetHighlight(string bodyKey)
        {
            highlightTarget = Rig != null ? Rig.GetBodyVisual(bodyKey) : null;
            if (highlightTarget == null) { ClearHighlight(); return; }
            if (highlight == null)
            {
                var go = new GameObject("Tour Highlight (MR)");
                highlight = go.AddComponent<LineRenderer>();
                highlight.positionCount = 72;
                highlight.loop = true;
                highlight.useWorldSpace = true;
                highlight.material = ringMat;
                highlight.startWidth = highlight.endWidth = 1f;
                highlight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            highlight.gameObject.SetActive(true);
        }

        public void ClearHighlight()
        {
            highlightTarget = null;
            if (highlight != null) highlight.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (highlight == null || !highlight.gameObject.activeSelf) return;
            if (highlightTarget == null) { ClearHighlight(); return; }
            var cam = Camera.main;
            if (cam == null) return;

            float scaleRatio = Rig != null ? Rig.transform.lossyScale.x / Mathf.Max(rigScale, 1e-6f) : 1f;
            float radius = Mathf.Max(highlightTarget.lossyScale.x * 2.6f, 0.05f * scaleRatio);
            Vector3 center = highlightTarget.position;
            Vector3 toCam = (cam.transform.position - center).normalized;
            Vector3 upRef = Mathf.Abs(toCam.y) > 0.98f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(upRef, toCam).normalized;
            Vector3 up = Vector3.Cross(toCam, right);
            int n = highlight.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                highlight.SetPosition(i, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            float pulse = 0.6f + 0.3f * Mathf.Sin(Time.time * 3.1f);
            var c = new Color(0.55f, 1.35f, 1.7f, pulse);
            highlight.startColor = c; highlight.endColor = c;
            highlight.widthMultiplier = 0.006f * Mathf.Max(scaleRatio, 0.2f);
        }

        void OnDestroy()
        {
            if (ringMat != null) Destroy(ringMat);
        }
    }
}
