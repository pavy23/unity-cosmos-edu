using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine.XR.OpenXR.Features.Meta;   // TryRequestDisplayRefreshRate — the
                                             // Meta OpenXR assembly only exists on
                                             // Android and in the editor.
#endif

namespace BlackHoleEffect
{
    /// <summary>
    /// The headset's frame policy, in one place. Installs itself only when an
    /// XR display is actually running, so desktop, web and phone builds never
    /// see any of this — that is a hard project rule, not an optimisation.
    ///
    /// What it owns:
    ///  - 72 Hz: ask the runtime for the rate we budget against, instead of
    ///    inheriting whatever it happens to start at.
    ///  - Foveated rendering: the OpenXR feature ships enabled but does nothing
    ///    until someone sets a level on the display. Fixed (non-gazed) FFR is
    ///    a 10–25% fragment saving on scenes this fragment-bound.
    ///  - Volume march caps: the MR scenes march the same volume shaders as
    ///    the desktop showcases, at desktop step counts (64–72). A property
    ///    block caps them per renderer — blocks survive material swaps (the
    ///    nebula gallery swaps specimen materials on one renderer) and never
    ///    dirty the shared .mat assets.
    ///  - Removing AdaptiveResolution: it installs itself on every Android
    ///    device, but its 30 fps floor is a phone policy — on a 72 Hz headset
    ///    it either idles or fights us. The web/phone builds keep it; we are
    ///    not allowed to touch that file, so the headset removes the instance.
    ///
    /// Numbers marked PROVISIONAL are pre-measurement guesses; the metrics HUD
    /// exists so they stop being guesses on the first device run.
    /// </summary>
    public class MRPerformance : MonoBehaviour
    {
        public const float TargetHz = 72f;

        // PROVISIONAL step caps (desktop ships 64 galaxy / 72 nebula). ~30%
        // fewer fragment loop iterations; retune against the HUD on device.
        public const float GalaxyStepCap = 44f;
        public const float NebulaStepCap = 52f;

        /// <summary>What the last Apply pass did — surfaced in the HUD.</summary>
        public static int CappedRenderers { get; private set; }
        public static bool FoveationSet { get; private set; }
        public static bool RefreshRateSet { get; private set; }

        static readonly int StepsId = Shader.PropertyToID("_Steps");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!XrRunning()) return;
            if (FindAnyObjectByType<MRPerformance>() != null) return;

            var go = new GameObject("MR Performance");
            go.AddComponent<MRPerformance>();
            DontDestroyOnLoad(go);
        }

        static bool XrRunning()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays)
                if (d.running) return true;
            return XRSettings.isDeviceActive;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();

            if (Debug.isDebugBuild && FindAnyObjectByType<MRMetricsHUD>() == null)
                new GameObject("MR Metrics HUD").AddComponent<MRMetricsHUD>();
        }

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene s, LoadSceneMode m) => Apply();

        void Apply()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
            {
                if (!display.running) continue;
#if UNITY_ANDROID || UNITY_EDITOR
                RefreshRateSet = display.TryRequestDisplayRefreshRate(TargetHz);
#endif
                // Level is 0..1 across the runtime's quality range; flags None
                // means fixed foveation — no eye tracking dependency.
                display.foveatedRenderingLevel = 1f;
                display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                FoveationSet = true;
            }

            foreach (var adaptive in FindObjectsByType<AdaptiveResolution>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(adaptive.gameObject);

            CapVolumeSteps();
        }

        /// <summary>
        /// Cap raymarch steps on every volume renderer in the scene via
        /// property blocks. Shader-name matching, not material-name: the MR
        /// scenes only ever contain the *MR material copies, and this code
        /// only runs under a headset, so the desktop materials are never in
        /// reach — but the shader is the honest identity either way.
        /// </summary>
        void CapVolumeSteps()
        {
            CappedRenderers = 0;
            var block = new MaterialPropertyBlock();
            foreach (var r in FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null) continue;

                float cap;
                switch (mat.shader.name)
                {
                    case "MilkyWay/GalaxyVolume": cap = GalaxyStepCap; break;
                    case "MilkyWay/NebulaVolume": cap = NebulaStepCap; break;
                    default: continue;
                }
                if (!mat.HasProperty(StepsId) || mat.GetFloat(StepsId) <= cap) continue;

                r.GetPropertyBlock(block);
                block.SetFloat(StepsId, cap);
                r.SetPropertyBlock(block);
                CappedRenderers++;
            }
        }
    }
}
