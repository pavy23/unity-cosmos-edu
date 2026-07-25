using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BlackHoleEffect
{
    /// <summary>
    /// Keeps the exhibits playable on weak GPUs without giving up sharpness on
    /// strong ones.
    ///
    /// Every headline scene here is fragment bound — the black hole, the
    /// galaxy and the nebulae are all raymarched per pixel — so cost scales
    /// with the pixel count, and a phone at devicePixelRatio 3 asks for nine
    /// times the pixels of DPR 1. Capping the canvas resolution outright was
    /// the first answer and it cost visible sharpness everywhere, including on
    /// hardware that could afford it.
    ///
    /// Instead the canvas stays native and URP's render scale moves: the 3D
    /// pass renders smaller and is upscaled, only on devices that measurably
    /// cannot keep up. Steps are coarse and hysteretic (a long window before
    /// stepping down, a longer one before stepping back up) so the resolution
    /// never visibly pumps during a camera move.
    ///
    /// Web and mobile only — a desktop player has the headroom, and changing
    /// the pipeline asset in the editor would dirty a shared asset.
    /// </summary>
    public class AdaptiveResolution : MonoBehaviour
    {
        const float MinScale = 0.6f;
        const float MaxScale = 1.0f;
        const float Step = 0.1f;

        // 30 fps floor, 50 fps ceiling. The gap is deliberately wide: stepping
        // whenever the frame time crossed a single threshold would oscillate.
        const float SlowFrame = 1f / 30f;
        const float FastFrame = 1f / 50f;

        const float DownWindow = 1.5f;   // sustained slow before dropping
        const float UpWindow = 5f;       // sustained fast before restoring
        const float Warmup = 3f;         // ignore load/compile hitches

        float smoothed = 1f / 60f;
        float slowFor, fastFor, aliveFor;
        UniversalRenderPipelineAsset urp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            bool wanted = true;
#else
            bool wanted = Application.isMobilePlatform;
#endif
            if (!wanted) return;
            var go = new GameObject("Adaptive Resolution");
            go.AddComponent<AdaptiveResolution>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            // Quality-level override wins over the graphics default — this
            // project ships per-platform "Project Configuration" presets, so
            // the asset actually in use comes from QualitySettings.
            urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                  ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) { enabled = false; return; }
            urp.renderScale = MaxScale;   // always start honest
        }

        void OnDestroy()
        {
            // The pipeline asset is shared: never leave it scaled down for the
            // next session (the StarfieldSkybox lesson — restore what you write).
            if (urp != null) urp.renderScale = MaxScale;
        }

        void Update()
        {
            if (urp == null) return;
            aliveFor += Time.unscaledDeltaTime;
            if (aliveFor < Warmup) return;

            // Exponential smoothing: a single long frame (scene load, shader
            // compile, GC) must not move the resolution.
            smoothed = Mathf.Lerp(smoothed, Time.unscaledDeltaTime, 0.05f);

            if (smoothed > SlowFrame) { slowFor += Time.unscaledDeltaTime; fastFor = 0f; }
            else if (smoothed < FastFrame) { fastFor += Time.unscaledDeltaTime; slowFor = 0f; }
            else { slowFor = 0f; fastFor = 0f; }

            if (slowFor >= DownWindow && urp.renderScale > MinScale + 0.001f)
            {
                urp.renderScale = Mathf.Max(MinScale, urp.renderScale - Step);
                slowFor = 0f;
            }
            else if (fastFor >= UpWindow && urp.renderScale < MaxScale - 0.001f)
            {
                urp.renderScale = Mathf.Min(MaxScale, urp.renderScale + Step);
                fastFor = 0f;
            }
        }
    }
}
