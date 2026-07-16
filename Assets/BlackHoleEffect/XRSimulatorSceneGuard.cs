using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace BlackHoleEffect
{
    /// <summary>
    /// The XR Interaction Simulator auto-spawns from a GLOBAL editor setting
    /// (Assets/XRI/Settings), so it turns up in every scene — including the
    /// pure-desktop ones (black-hole showcase, Milky Way), where there is no
    /// XR Origin to aim through and its Update throws a NullReferenceException
    /// every frame. One rule, owned here and nowhere else: a scene without an
    /// XROrigin gets no simulator.
    ///
    /// Effectively editor-only: the settings never instantiate the simulator
    /// in a player build, so in players this is a single cheap null-check per
    /// scene load.
    ///
    /// Known limit: the simulator's loader runs once at play start. If a play
    /// session starts in a desktop scene (simulator destroyed) and then loads
    /// the MR scene additively, the simulator will not come back — restart
    /// play mode from the MR scene instead.
    /// </summary>
    static class XRSimulatorSceneGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // Plain runtime object: dies with play mode, no DontSave leak.
            var go = new GameObject("XR Simulator Scene Guard");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            // Start runs after every RuntimeInitializeOnLoadMethod — including
            // the simulator's own spawner, whose order relative to ours is
            // undefined. Checking here instead of in Init closes that race.
            void Start() => Sweep();

            void OnEnable() => SceneManager.sceneLoaded += OnLoaded;
            void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;
            void OnLoaded(Scene s, LoadSceneMode m) => Sweep();

            static void Sweep()
            {
                if (Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>() != null) return;
                var sim = Object.FindAnyObjectByType<XRInteractionSimulator>();
                if (sim != null) Object.Destroy(sim.gameObject);
            }
        }
    }
}
