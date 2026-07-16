using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// Parameter hub for the hybrid galaxy — one inspector that drives both
    /// halves (the raymarched volume and the baked starfield) so they stay in
    /// visual agreement. Writes to the sharedMaterials directly, the same
    /// SRP-batcher reason as BlackHoleController.
    ///
    /// Units everywhere: 1 unit = 1 kpc. The Sun sits at 8.2.
    /// </summary>
    [ExecuteAlways]
    public class MilkyWayController : MonoBehaviour
    {
        [Header("Wiring (set by the scene builder)")]
        public Material volumeMaterial;
        public Material starMaterial;
        public Renderer volumeRenderer;

        [Header("Light")]
        [Range(0f, 8f)] public float brightness = 2.2f;
        [Range(0f, 4f)] public float bulgeBoost = 0.9f;
        [Range(0f, 3f)] public float hiiStrength = 1.0f;
        [Range(0f, 3f)] public float youngStrength = 1.0f;
        [Range(0f, 6f)] public float starBrightness = 1.15f;

        [Header("Structure")]
        [Range(0f, 6f)] public float dustStrength = 3.4f;
        [Range(0.2f, 2.5f)] public float armWidth = 0.9f;
        [Range(0f, 1.5f)] public float clumpiness = 0.85f;

        [Header("Quality")]
        [Range(24, 160)] public int marchSteps = 80;

        /// <summary>The Sun's galactocentric radius in kpc — the anchor for the
        /// zoom journey and the night-sky view.</summary>
        public const float SunRadiusKpc = 8.2f;

        /// <summary>Where the Sun sits in the galaxy's object space. By the
        /// shader's convention the Sun-centre line is +x (the bar leans 27°
        /// away from it).</summary>
        public Vector3 SunPositionLocal => new Vector3(SunRadiusKpc, 0.02f, 0f);
        public Vector3 SunPositionWorld => transform.TransformPoint(SunPositionLocal);

        static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        static readonly int BulgeBoostId = Shader.PropertyToID("_BulgeBoost");
        static readonly int HiiId = Shader.PropertyToID("_HiiStrength");
        static readonly int YoungId = Shader.PropertyToID("_YoungStrength");
        static readonly int DustId = Shader.PropertyToID("_DustStrength");
        static readonly int ArmWidthId = Shader.PropertyToID("_ArmWidth");
        static readonly int ClumpId = Shader.PropertyToID("_Clumpiness");
        static readonly int StepsId = Shader.PropertyToID("_Steps");
        static readonly int StarBrightId = Shader.PropertyToID("_StarBrightness");

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            if (volumeMaterial != null)
            {
                volumeMaterial.SetFloat(BrightnessId, brightness);
                volumeMaterial.SetFloat(BulgeBoostId, bulgeBoost);
                volumeMaterial.SetFloat(HiiId, hiiStrength);
                volumeMaterial.SetFloat(YoungId, youngStrength);
                volumeMaterial.SetFloat(DustId, dustStrength);
                volumeMaterial.SetFloat(ArmWidthId, armWidth);
                volumeMaterial.SetFloat(ClumpId, clumpiness);
                volumeMaterial.SetFloat(StepsId, marchSteps);
            }
            if (starMaterial != null)
                starMaterial.SetFloat(StarBrightId, starBrightness);

            // The volume cube inflates itself in the vertex shader far past its
            // 1x1 mesh — widen the culling bounds or it vanishes at the frustum
            // edge (the BlackHoleController lesson).
            if (volumeRenderer != null)
                volumeRenderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 40f);
        }
    }
}
