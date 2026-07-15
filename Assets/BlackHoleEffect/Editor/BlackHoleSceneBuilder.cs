using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace BlackHoleEffect.Editor
{
    /// <summary>
    /// Builds the realistic (geodesic-raymarched) black hole showcase scene:
    /// one billboard quad carrying the raymarch shader, a matching procedural
    /// starfield skybox, and ACES + bloom post-processing.
    /// </summary>
    public static class BlackHoleSceneBuilder
    {
        const string Root = "Assets/BlackHoleEffect";
        const string ScenePath = Root + "/Scenes/BlackHoleShowcase.unity";
        const string SessionKey = "BlackHoleShowcase_v4_raymarch_built";

        [InitializeOnLoadMethod]
        static void BuildOnceAfterImport()
        {
            EditorApplication.delayCall += () =>
            {
                // Auto-build only when the showcase scene doesn't exist yet;
                // rebuilding on every editor session would wipe manual tweaks.
                if (!EditorApplication.isPlayingOrWillChangePlaymode
                    && !SessionState.GetBool(SessionKey, false)
                    && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    SessionState.SetBool(SessionKey, true);
                    Build();
                }
            };
        }

        [MenuItem("Tools/Black Hole/Create Showcase Scene (Realistic)")]
        public static void Build()
        {
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Settings");

            var holeShader = Shader.Find("BlackHole/RaymarchedBlackHole");
            var skyShader = Shader.Find("BlackHole/StarfieldSkybox");
            if (!holeShader || !skyShader)
            {
                Debug.LogError("Black hole shaders not imported yet. After compilation, run Tools > Black Hole > Create Showcase Scene (Realistic).");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BlackHoleShowcase";

            // --- Sky and ambient -------------------------------------------------
            var skyMat = SaveMaterial("StarfieldSkybox", skyShader);
            skyMat.SetFloat("_StarDensity", 0.12f);
            skyMat.SetFloat("_NebulaIntensity", 0.15f);
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            // --- Camera ----------------------------------------------------------
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 1.5f, -13f);
            cam.transform.LookAt(new Vector3(0f, 0.1f, 0f));
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            var camData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            // --- Black hole ------------------------------------------------------
            // Uniform scale = Schwarzschild radius in metres. The quad billboards
            // and sizes itself in the vertex shader, so the mesh is just a carrier.
            var holeMat = SaveMaterial("BlackHoleRaymarch", holeShader);
            var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hole.name = "Gargantua (Raymarched Black Hole)";
            hole.transform.position = Vector3.zero;
            hole.transform.rotation = Quaternion.Euler(2f, 0f, -4f); // slight disk tilt
            hole.transform.localScale = Vector3.one * 0.5f;          // Rs = 0.5 m
            hole.GetComponent<MeshRenderer>().sharedMaterial = holeMat;
            Object.DestroyImmediate(hole.GetComponent<Collider>());
            hole.AddComponent<BlackHoleController>();
            hole.AddComponent<BlackHoleAnnotations>();
            var einstein = hole.AddComponent<EinsteinRingDemo>();
            var spaghetti = hole.AddComponent<SpaghettificationDemo>();
            spaghetti.blackHole = hole.transform;
            var jets = hole.AddComponent<RelativisticJets>();

            var orbit = cameraGO.AddComponent<CinematicOrbit>();
            orbit.target = hole.transform;

            // --- Educational tools -----------------------------------------------
            var launcherGO = new GameObject("Photon Launcher (Space = Fire Sweep)");
            var launcher = launcherGO.AddComponent<PhotonLauncher>();
            launcher.blackHole = hole.transform;

            var panelGO = new GameObject("Physics Panel");
            panelGO.transform.SetParent(cameraGO.transform, false);
            var panel = panelGO.AddComponent<BlackHolePhysicsPanel>();
            panel.controller = hole.GetComponent<BlackHoleController>();

            var comparisonGO = new GameObject("Observation Comparison");
            comparisonGO.transform.SetParent(cameraGO.transform, false);
            var comparison = comparisonGO.AddComponent<ObservationComparison>();
            comparison.observationImage = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/EHT_M87.jpg");

            var tour = cameraGO.AddComponent<GuidedTour>();
            tour.annotations = hole.GetComponent<BlackHoleAnnotations>();
            tour.panel = panel;
            tour.einsteinDemo = einstein;
            tour.launcher = launcher;
            tour.spaghetti = spaghetti;
            tour.jets = jets;
            tour.comparison = comparison;

            cameraGO.AddComponent<AudioListener>();
            cameraGO.AddComponent<AudioSource>();
            var audioScape = cameraGO.AddComponent<BlackHoleAudio>();
            var hud = cameraGO.AddComponent<PerformanceHud>();
            hud.controller = hole.GetComponent<BlackHoleController>();

            var lightCurve = cameraGO.AddComponent<LightCurveGraph>();
            lightCurve.controller = hole.GetComponent<BlackHoleController>();
            lightCurve.spaghetti = spaghetti;

            var intro = cameraGO.AddComponent<IntroSequence>();
            intro.holeRenderer = hole.GetComponent<MeshRenderer>();
            intro.controller = hole.GetComponent<BlackHoleController>();

            var fallIn = cameraGO.AddComponent<FallInMode>();
            fallIn.hole = hole.transform;
            fallIn.orbit = orbit;

            var lensDemo = cameraGO.AddComponent<GravitationalLensDemo>();
            lensDemo.controller = hole.GetComponent<BlackHoleController>();
            lensDemo.einstein = einstein;

            var controls = cameraGO.AddComponent<DesktopControls>();
            controls.target = hole.transform;
            controls.controller = hole.GetComponent<BlackHoleController>();
            controls.panel = panel;
            controls.annotations = hole.GetComponent<BlackHoleAnnotations>();
            controls.launcher = launcher;
            controls.einsteinDemo = einstein;
            controls.comparison = comparison;
            controls.autoOrbit = orbit;
            controls.spaghetti = spaghetti;
            controls.jets = jets;
            controls.tour = tour;
            controls.audioScape = audioScape;
            controls.hud = hud;
            controls.lightCurve = lightCurve;
            controls.intro = intro;
            controls.fallIn = fallIn;
            controls.lensDemo = lensDemo;
            intro.controls = controls;
            fallIn.controls = controls;

            SetupPostProcessing();
            SetDisplayDefaults();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = hole;
            Debug.Log("Realistic black hole showcase created: " + ScenePath);
        }

        [MenuItem("Tools/Black Hole/Set Game View 16:9 + 1080p Defaults")]
        public static void SetDisplayDefaults()
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            if (!TrySetGameViewAspect("Full HD")) TrySetGameViewAspect("16:9");
        }

        /// <summary>Selects a fixed aspect in the Game view via reflection
        /// (there is no public API). Fails quietly if internals changed.</summary>
        static bool TrySetGameViewAspect(string label)
        {
            try
            {
                const System.Reflection.BindingFlags Any =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleType.GetProperty("instance", Any).GetValue(null);
                var groupType = sizesType.GetProperty("currentGroupType", Any).GetValue(instance);
                var group = sizesType.GetMethod("GetGroup", Any).Invoke(instance, new[] { groupType });
                int total = (int)group.GetType().GetMethod("GetTotalCount", Any).Invoke(group, null);

                int idx = -1;
                for (int i = 0; i < total; i++)
                {
                    var size = group.GetType().GetMethod("GetGameViewSize", Any).Invoke(group, new object[] { i });
                    var text = (string)size.GetType().GetProperty("baseText", Any).GetValue(size);
                    if (text != null && text.Contains(label)) { idx = i; break; }
                }
                if (idx < 0) { Debug.LogWarning("Game view size '" + label + "' not found."); return false; }

                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gvType, false, null, false);
                gvType.GetProperty("selectedSizeIndex", Any).SetValue(gv, idx);
                gv.Repaint();
                Debug.Log("Game view size set to " + label);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Could not set Game view size: " + e.Message);
                return false;
            }
        }

        /// <summary>Finds the XRI hands rig whatever sample version is imported —
        /// the folder is named after the package version at import time, so a
        /// hardcoded path silently degrades to a bare camera after any upgrade.</summary>
        static GameObject FindHandsRigPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("\"XR Origin Hands\" t:GameObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Samples/XR Interaction Toolkit/") && path.EndsWith(".prefab"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        [MenuItem("Tools/Black Hole/Create MR Scene (Passthrough)")]
        public static void BuildMR()
        {
            var holeShader = Shader.Find("BlackHole/RaymarchedBlackHole");
            if (!holeShader)
            {
                Debug.LogError("Black hole shader not imported yet.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "BlackHoleMR";
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);
            RenderSettings.fog = false;

            // --- XR rig (hands + controllers) ------------------------------------
            Camera cam;
            var rigPrefab = FindHandsRigPrefab();
            if (rigPrefab != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                rig.transform.position = Vector3.zero;
                cam = rig.GetComponentInChildren<Camera>();
            }
            else
            {
                Debug.LogWarning("Hands rig prefab not found; creating plain camera. Import XRI 'Hands Interaction Demo' sample for full interaction.");
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
            }

            // Passthrough: clear to transparent black, no post (post can stomp
            // the alpha channel the compositor uses for passthrough).
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;

            if (Object.FindFirstObjectByType<ARSession>() == null)
                new GameObject("AR Session", typeof(ARSession));
            if (cam.GetComponent<ARCameraManager>() == null)
                cam.gameObject.AddComponent<ARCameraManager>();

            // --- Room-scale black hole (Rs = 12 cm, disk ~2 m wide) --------------
            var holeMat = SaveMaterial("BlackHoleRaymarchMR", holeShader);
            holeMat.SetFloat("_MRMode", 1f);
            // No depth write + transparent queue so two holes (binary merger)
            // composite instead of clipping each other's billboards.
            holeMat.SetFloat("_ZWrite", 0f);
            holeMat.renderQueue = 2900;

            var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hole.name = "Gargantua (MR)";
            hole.transform.position = new Vector3(0f, 1.3f, 1.6f);
            hole.transform.rotation = Quaternion.Euler(2f, 0f, -4f);
            hole.transform.localScale = Vector3.one * 0.12f;
            hole.GetComponent<MeshRenderer>().sharedMaterial = holeMat;
            Object.DestroyImmediate(hole.GetComponent<Collider>());

            var ctrl = hole.AddComponent<BlackHoleController>();
            ctrl.quality = BlackHoleController.QualityProfile.QuestOptimized;
            ctrl.raymarchSteps = 96;
            // AddComponent already ran OnEnable with the desktop defaults and wrote
            // them to the shared material asset. Assigning fields from code does not
            // fire OnValidate, so without this the .mat keeps 220 steps and the
            // Quest budget silently reads as if it were never set.
            ctrl.Apply();
            var annotations = hole.AddComponent<BlackHoleAnnotations>();

            // --- Hand grab: move with one hand, scale with two -------------------
            var sphere = hole.AddComponent<SphereCollider>();
            sphere.radius = 3f; // ~3 Rs grab bubble around the shadow
            var rb = hole.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = hole.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackRotation = true;
            grab.trackScale = true;
            var transformer = hole.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowTwoHandedScaling = true;

            // --- Feed the beast: throwable star-balls on a floating shelf --------
            // Real little stars (StarSurface: limb darkening + convection
            // granulation), one per spectral type. Corona pass is disabled in
            // MR: its additive blend saturates the alpha channel the
            // passthrough compositor reads, which would punch black halos
            // into the room view.
            var flare = hole.AddComponent<MatterFlare>();
            var starShader = Shader.Find("BlackHole/StarSurface");
            var starBallMats = new Material[3];
            var starColors = new[]
            {
                new Color(2.6f, 2.2f, 1.4f),  // G-type: sun-like yellow-white
                new Color(2.6f, 1.4f, 0.55f), // K-type: orange
                new Color(2.2f, 0.75f, 0.35f) // M-type: red dwarf
            };
            for (int i = 0; i < 3; i++)
            {
                starBallMats[i] = SaveMaterial("ThrowableStar" + (char)('A' + i), starShader);
                starBallMats[i].SetColor("_StarColor", starColors[i]);
                starBallMats[i].SetFloat("_Granulation", 0.5f);
                starBallMats[i].SetFloat("_GranScale", 9f - i * 1.5f); // cooler = chunkier cells
                starBallMats[i].SetFloat("_SpotStrength", 0.2f + 0.12f * i);
                starBallMats[i].SetFloat("_RimBoost", 0.4f);
                starBallMats[i].SetFloat("_CoronaBoost", 0f);
            }
            for (int i = 0; i < 3; i++)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Star Ball " + (i + 1);
                // Within arm's reach (~0.6 m). At the old 0.85 m they measured
                // 1.25 m from the eye — grabbable only by ray, which makes
                // "pick up a star and throw it in" a pointing exercise.
                ball.transform.position = new Vector3(-0.2f + 0.2f * i, 1.05f, 0.42f);
                ball.transform.localScale = Vector3.one * 0.06f;
                ball.GetComponent<MeshRenderer>().sharedMaterial = starBallMats[i];
                var ballRb = ball.AddComponent<Rigidbody>();
                ballRb.useGravity = false;
                ballRb.linearDamping = 0f;
                var ballGrab = ball.AddComponent<XRGrabInteractable>();
                ballGrab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                ballGrab.throwOnDetach = true;
                var matter = ball.AddComponent<FallingMatter>();
                matter.hole = hole.transform;
                matter.flare = flare;
            }

            // --- Educational layer -----------------------------------------------
            // Same components as the desktop showcase. Their UI needs no MR port:
            // BlackHoleUI hangs the shared canvas in the room when it sees an XR
            // rig, so the panels frame the real hole instead of the screen.
            var einstein = hole.AddComponent<EinsteinRingDemo>();
            var spaghetti = hole.AddComponent<SpaghettificationDemo>();
            spaghetti.blackHole = hole.transform;
            var jets = hole.AddComponent<RelativisticJets>();

            var launcherGO = new GameObject("Photon Launcher");
            var launcher = launcherGO.AddComponent<PhotonLauncher>();
            launcher.blackHole = hole.transform;

            var panelGO = new GameObject("Physics Panel");
            panelGO.transform.SetParent(cam.transform, false);
            var panel = panelGO.AddComponent<BlackHolePhysicsPanel>();
            panel.controller = ctrl;

            var comparisonGO = new GameObject("Observation Comparison");
            comparisonGO.transform.SetParent(cam.transform, false);
            var comparison = comparisonGO.AddComponent<ObservationComparison>();
            comparison.observationImage = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/EHT_M87.jpg");

            var tour = cam.gameObject.AddComponent<GuidedTour>();
            tour.annotations = annotations;
            tour.panel = panel;
            tour.einsteinDemo = einstein;
            tour.launcher = launcher;
            tour.spaghetti = spaghetti;
            tour.jets = jets;
            tour.comparison = comparison;

            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            if (cam.GetComponent<AudioSource>() == null) cam.gameObject.AddComponent<AudioSource>();
            var audioScape = cam.gameObject.AddComponent<BlackHoleAudio>();

            // Fall-in rides the XR rig and leaves the looking to the passenger.
            var fallIn = cam.gameObject.AddComponent<FallInMode>();
            fallIn.hole = hole.transform;

            // Birth of the hole. Its star and blast are sized off the hole's Rs,
            // so the room-scale hole gets a room-scale supernova.
            var intro = cam.gameObject.AddComponent<IntroSequence>();
            intro.holeRenderer = hole.GetComponent<MeshRenderer>();
            intro.controller = ctrl;

            // Drives every toggle and attaches the theory panel; xrMode keeps its
            // hands off the tracked camera pose and the passthrough alpha.
            var controls = cam.gameObject.AddComponent<DesktopControls>();
            controls.xrMode = true;
            controls.target = hole.transform;
            controls.controller = ctrl;
            controls.panel = panel;
            controls.annotations = annotations;
            controls.launcher = launcher;
            controls.einsteinDemo = einstein;
            controls.comparison = comparison;
            controls.spaghetti = spaghetti;
            controls.jets = jets;
            controls.tour = tour;
            controls.audioScape = audioScape;
            controls.fallIn = fallIn;
            fallIn.controls = controls;
            controls.intro = intro;
            intro.controls = controls;

            var mrControls = cam.gameObject.AddComponent<MRControls>();
            mrControls.controls = controls;
            mrControls.hole = hole.transform;

            // The merger (BinaryMergerCinematic, attached by DesktopControls) is
            // gas-free, and a gas-free hole has nothing to lens against
            // passthrough — so it borrows the room for the duration.
            var space = cam.gameObject.AddComponent<MRSpaceWindow>();
            space.controller = ctrl;
            space.viewer = cam;
            space.starfield = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/StarfieldSkybox.mat");
            space.passthroughMRMode = 1f;

            // --- Palm-summoned miniature ------------------------------------------
            var palmMat = SaveMaterial("BlackHoleRaymarchPalm", holeShader);
            palmMat.SetFloat("_MRMode", 1f);
            palmMat.SetFloat("_ZWrite", 0f);
            palmMat.renderQueue = 2901;
            palmMat.SetFloat("_Steps", 64f);
            palmMat.SetFloat("_ViewExtent", 12f);
            var palmGO = new GameObject("Palm Summon");
            var palm = palmGO.AddComponent<PalmMiniBlackHole>();
            palm.holeMaterial = palmMat;
            palm.xrOrigin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();

            EditorSceneManager.SaveScene(scene, Root + "/Scenes/BlackHoleMR.unity");
            Selection.activeGameObject = hole;
            Debug.Log("MR passthrough black hole scene created: " + Root + "/Scenes/BlackHoleMR.unity");
        }

        static void SetupPostProcessing()
        {
            const string profilePath = Root + "/Settings/BlackHoleVolume.asset";
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (old) AssetDatabase.DeleteAsset(profilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.95f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(true);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.7f);
            vignette.color.Override(Color.black);

            var chroma = profile.Add<ChromaticAberration>();
            chroma.active = true;
            chroma.intensity.Override(0.05f);

            var grain = profile.Add<FilmGrain>();
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);
            grain.response.Override(0.7f);

            var colorAdjust = profile.Add<ColorAdjustments>();
            colorAdjust.active = true;
            colorAdjust.contrast.Override(8f);
            colorAdjust.saturation.Override(6f);

            AssetDatabase.CreateAsset(profile, profilePath);

            var go = new GameObject("Post Processing (ACES + Bloom)");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
        }

        static Material SaveMaterial(string name, Shader shader)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.shader = shader; return existing; }
            var mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string parent, string child)
        {
            var p = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
