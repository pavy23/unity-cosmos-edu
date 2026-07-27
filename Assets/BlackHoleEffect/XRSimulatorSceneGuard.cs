#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace BlackHoleEffect
{
    /// <summary>
    /// Owns when the XR Interaction Simulator exists, because XRI's own answer
    /// is "always" and that answer breaks the headset.
    ///
    /// XRI ships a global auto-spawn (Edit > Project Settings > XR Plug-in
    /// Management > XR Interaction Toolkit). Its loader checks nothing except
    /// "am I in the editor" before instantiating the prefab, and the prefab's
    /// SimulatedDeviceLifecycleManager then, in OnEnable:
    ///
    ///   * removes every real XRHMD from the Input System, and subscribes to
    ///     onDeviceChange so any HMD that shows up *later* is removed too, and
    ///   * stops every running XRHandSubsystem.
    ///
    /// Over Quest Link the real headset registers around 12 s into play mode —
    /// straight into that onDeviceChange hook. The symptom is a session that
    /// looks correctly configured and does not track: the view is presented to
    /// the headset, the camera never moves, and the world-space UI hangs in
    /// front of your face.
    ///
    /// None of that is repairable after the fact, which is why this used to be
    /// a cleanup pass and no longer is. OnDisable removes the simulator's own
    /// devices but never restores the real HMD it deleted, and OnDestroy never
    /// restarts the hand subsystem it stopped. By the time HmdActive() is true
    /// — the only honest "a headset is here" signal — the damage is done.
    ///
    /// So the auto-spawn is off in XRDeviceSimulatorSettings and the decision
    /// moves here, where it is made once, before anything is touched, and errs
    /// toward leaving the runtime alone: the simulator is created only in a
    /// session that has no headset display at all. A false "no headset" would
    /// cost a desktop tester their mouse-driven rig for one play session; a
    /// false "headset" costs nothing.
    ///
    /// Editor-only in the strict sense — the whole file compiles out of player
    /// builds, where the simulator has no business existing anyway.
    /// </summary>
    static class XRSimulatorSceneGuard
    {
        /// <summary>
        /// "XR Interaction Simulator.prefab" from the XRI sample of the same
        /// name. Fixed by the package, so it survives a reimport of the sample;
        /// <see cref="Prefab"/> falls back to a search if it ever does not.
        /// </summary>
        const string PrefabGuid = "58d0a4ac86f2348deb02f3880c71378e";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // Plain runtime object: dies with play mode, no DontSave leak.
            var go = new GameObject("XR Simulator Scene Guard");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        static GameObject Prefab()
        {
            var path = AssetDatabase.GUIDToAssetPath(PrefabGuid);
            var prefab = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            // The sample was reimported under a different GUID, or moved. Ask
            // for the component rather than the name — the name is the sample
            // author's, the component is the contract.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.GetComponent<XRInteractionSimulator>() != null)
                    return candidate;
            }

            return null;
        }

        class Runner : MonoBehaviour
        {
            bool warned;

            // Start runs after every RuntimeInitializeOnLoadMethod, so a scene
            // that authored its own simulator has it by now.
            void Start() => Sync();

            void OnEnable() => SceneManager.sceneLoaded += OnLoaded;
            void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;
            void OnLoaded(Scene s, LoadSceneMode m) => Sync();

            // Re-run per scene load, because the second half of the condition
            // moves: the pure-desktop showcases have no XROrigin for the
            // simulator to aim through, and its Update throws a
            // NullReferenceException every frame in one.
            void Sync()
            {
                var wanted = !XRRuntime.HmdPresent()
                             && Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>() != null;
                var sim = Object.FindAnyObjectByType<XRInteractionSimulator>();

                if (wanted)
                {
                    if (sim == null) Spawn();
                    return;
                }

                // Nothing legitimate keeps a simulator in this scene, so it goes
                // whoever made it — a hand-placed one costs exactly as much as
                // ours. Unlike the headset case this is free either way: it only
                // ever survives to here in a session with no real device behind
                // it, or in a scene with no rig for it to drive.
                if (sim != null) Destroy(sim.gameObject);
            }

            void Spawn()
            {
                var prefab = Prefab();
                if (prefab == null)
                {
                    if (warned) return;
                    warned = true;
                    Debug.LogWarning("XR Interaction Simulator prefab not found — desktop play " +
                                     "has no simulated rig. Import it from Window > Package Manager > " +
                                     "XR Interaction Toolkit > Samples > XR Interaction Simulator.");
                    return;
                }

                var instance = Instantiate(prefab);
                instance.name = prefab.name;   // strip "(Clone)"
                DontDestroyOnLoad(instance);
            }
        }
    }
}
#endif
