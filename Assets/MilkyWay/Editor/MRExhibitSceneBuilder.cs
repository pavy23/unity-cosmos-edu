using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilkyWay.Editor
{
    /// <summary>
    /// Builds the MR (passthrough) editions of the galaxy and solar-system
    /// exhibits: the Milky Way as a ~1.1 m grabbable miniature, the solar
    /// system as a room-scale orrery. Same menu-driven pattern as every other
    /// scene here — the scenes are build artifacts, never hand-edited. Both
    /// are registered in the build settings so the MR menus can hop between
    /// the three exhibits by scene name.
    /// </summary>
    public static class MRExhibitSceneBuilder
    {
        const string Root = "Assets/MilkyWay";

        // Visible disk radius is 16 kpc (object units); 0.034 puts the disk
        // at ~1.09 m across, floating at chest height within arm's reach.
        const float GalaxyScale = 0.034f;

        [MenuItem("Tools/Milky Way/Create MR Scene (Passthrough)")]
        public static void BuildMilkyWayMR()
        {
            var volumeShader = Shader.Find("MilkyWay/GalaxyVolume");
            var starShader = Shader.Find("MilkyWay/GalaxyStars");
            if (!volumeShader || !starShader)
            {
                Debug.LogError("Milky Way shaders not compiled yet.");
                return;
            }

            var scene = NewMRScene("MilkyWayMR");
            var cam = BuildXRRig();

            // MR-specific materials: the desktop .mats stay untouched, so MR
            // tuning (and any values a killed play session bakes in) never
            // leaks into the desktop showcase.
            var volumeMat = SaveMaterial("GalaxyVolumeMR", volumeShader);
            var starMat = SaveMaterial("GalaxyStarsMR", starShader);
            // The star shader fades stars within _NearFade WORLD units of the
            // camera (a fly-through guard). At miniature scale that would be
            // a quarter of the galaxy — shrink it with the exhibit.
            starMat.SetFloat("_NearFade", 0.08f * GalaxyScale);
            // At 1/30 scale most stars land under the shader's 1.5-pixel
            // floor and get energy-conserved into dimness; larger points keep
            // the starfield alive at arm's length.
            starMat.SetFloat("_SizeScale", 2.4f);

            var root = new GameObject("Milky Way (MR)");
            // Below eye level and tipped toward the viewer: at eye height the
            // disk is edge-on — a bright bar, no spiral. Tilting the plane
            // shows the face the way a table hologram would.
            root.transform.position = new Vector3(0f, 1.05f, 1.55f);
            root.transform.rotation = Quaternion.Euler(-28f, 0f, 0f);
            root.transform.localScale = Vector3.one * GalaxyScale;

            var volumeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volumeGO.name = "Galaxy Volume";
            volumeGO.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(volumeGO.GetComponent<Collider>());
            var volumeRenderer = volumeGO.GetComponent<MeshRenderer>();
            volumeRenderer.sharedMaterial = volumeMat;
            volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var starsGO = new GameObject("Galaxy Stars");
            starsGO.transform.SetParent(root.transform, false);
            var starField = starsGO.AddComponent<GalaxyStarField>();
            starField.material = starMat;

            var controller = root.AddComponent<MilkyWayController>();
            controller.volumeMaterial = volumeMat;
            controller.starMaterial = starMat;
            controller.volumeRenderer = volumeRenderer;
            controller.Apply(); // AddComponent already ran OnEnable before wiring — the BH lesson

            // Hand grab: move with one hand, scale with two.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 17f; // local kpc → ~0.58 m grab bubble
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackRotation = true;
            grab.trackScale = true;
            var transformer = root.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowTwoHandedScaling = true;

            var labelsGO = new GameObject("MR Labels");
            var labels = labelsGO.AddComponent<MRBodyLabels>();

            var stage = root.AddComponent<MilkyWayMRStage>();
            stage.controller = controller;
            stage.labels = labels;

            var audio = cam.gameObject.AddComponent<MilkyWayAudio>();

            var tour = cam.gameObject.AddComponent<MilkyWayMRTour>();
            tour.controller = controller;
            tour.stage = stage;

            var controls = cam.gameObject.AddComponent<MilkyWayMRControls>();
            controls.stage = stage;
            controls.tour = tour;
            tour.controls = controls;

            SaveAndRegister(scene, Root + "/Scenes/MilkyWayMR.unity");
            Selection.activeGameObject = root;
        }

        [MenuItem("Tools/Solar System/Create MR Scene (Passthrough)")]
        public static void BuildSolarSystemMR()
        {
            if (!Shader.Find("MilkyWay/PlanetSurface") || !Shader.Find("MilkyWay/OrbitLine"))
            {
                Debug.LogError("Solar system shaders not compiled yet.");
                return;
            }

            var scene = NewMRScene("SolarSystemMR");
            var cam = BuildXRRig();

            // The rig spawns at runtime (SolarSystemStage pattern) under this
            // anchor; grabbing the anchor moves the whole orrery, and the rig
            // re-bakes _SunPos + line widths when its root moves or rescales.
            var anchor = new GameObject("Solar System Anchor (MR)");
            anchor.transform.position = new Vector3(0f, 1.1f, 1.5f);

            var sphere = anchor.AddComponent<SphereCollider>();
            sphere.radius = 0.16f; // grab bubble around the sun
            var rb = anchor.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = anchor.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackRotation = true;
            grab.trackScale = true;
            var transformer = anchor.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowTwoHandedScaling = true;

            var labelsGO = new GameObject("MR Labels");
            var labels = labelsGO.AddComponent<MRBodyLabels>();
            // 0.014 shouted: the four inner planets orbit within ~25 cm of the
            // sun and their tags collided into a jumble. Smaller text plus a
            // tighter offset keeps the family legible from a step back.
            labels.baseCharSize = 0.0095f;

            var stage = anchor.AddComponent<SolarSystemMRStage>();
            stage.labels = labels;

            var audio = cam.gameObject.AddComponent<MilkyWayAudio>();

            var tour = cam.gameObject.AddComponent<SolarSystemMRTour>();
            tour.stage = stage;

            var controls = cam.gameObject.AddComponent<SolarSystemMRControls>();
            controls.stage = stage;
            controls.tour = tour;
            tour.controls = controls;

            SaveAndRegister(scene, Root + "/Scenes/SolarSystemMR.unity");
            Selection.activeGameObject = anchor;
        }

        // Hero radius ~7 units; 0.055 puts a specimen at ~0.8 m across,
        // floating at chest height within arm's reach.
        const float NebulaScale = 0.055f;

        [MenuItem("Tools/Nebula/Create MR Scene (Passthrough)")]
        public static void BuildNebulaMR()
        {
            var nebShader = Shader.Find("MilkyWay/NebulaVolume");
            var starShader = Shader.Find("MilkyWay/GalaxyStars");
            var photoShader = Shader.Find("MilkyWay/PhotoBackdrop");
            if (!nebShader || !starShader || !photoShader)
            {
                Debug.LogError("Nebula/star/photo shaders not compiled yet.");
                return;
            }

            var scene = NewMRScene("NebulaMR");
            var cam = BuildXRRig();

            var root = new GameObject("Nebula Stage (MR)");
            root.transform.position = new Vector3(0f, 1.25f, 1.6f);
            root.transform.localScale = Vector3.one * NebulaScale;

            var stage = root.AddComponent<NebulaMRStage>();

            for (int i = 0; i < NebulaLibrary.Heroes.Length; i++)
            {
                var h = NebulaLibrary.Heroes[i];
                var spec = new GameObject(h.id);
                spec.transform.SetParent(root.transform, false);

                if (h.form == NebulaLibrary.Form.Cluster)
                {
                    // ω Cen: the galaxy-miniature star recipe — MR material so
                    // near-fade and point size suit arm's length, desktop .mat
                    // untouched.
                    var clMat = SaveMaterial(h.id + "_StarsMR", starShader);
                    clMat.SetFloat("_StarBrightness", 1.05f);
                    clMat.SetFloat("_SizeScale", 2.0f);
                    clMat.SetFloat("_NearFade", 0.05f * NebulaScale);

                    var clGO = new GameObject("Cluster", typeof(MeshFilter), typeof(MeshRenderer), typeof(ClusterField));
                    clGO.transform.SetParent(spec.transform, false);
                    var clu = clGO.GetComponent<ClusterField>();
                    clu.kind = h.clusterKind;
                    clu.material = clMat;
                    clu.radius = h.clusterRadius;
                    clu.starCount = h.clusterStars;
                }
                else
                {
                    var mat = SaveMaterial(h.id + "_NebMR", nebShader);
                    NebulaLibrary.ApplyMaterial(mat, h);
                    // Two eyes pay for every march step — trim toward the
                    // galaxy volume's proven MR budget.
                    mat.SetFloat("_Steps", 64f);
                    // Passthrough compositing keys on alpha: purely-additive
                    // emission (alpha 0) would vanish against the room, so
                    // every specimen writes its occlusion — it glows AND dims
                    // the room behind it, like the space-window escape rays.
                    mat.SetFloat("_OccludeBg", 1f);

                    var vol = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    vol.name = "Volume";
                    vol.transform.SetParent(spec.transform, false);
                    Object.DestroyImmediate(vol.GetComponent<Collider>());
                    var mr = vol.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    if (h.stretch != Vector3.zero) vol.transform.localScale = h.stretch;

                    if (h.type == 4)
                    {
                        // The Horsehead diorama: silhouette mask into the volume
                        // and the inpainted photographic curtain behind it — a
                        // shadow box hanging in the room. Identity rotation is
                        // right: the viewer spawns on the volume's -z side.
                        var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            Root + "/Textures/DeepSky/mask_" + h.id + ".png");
                        if (maskTex != null) mat.SetTexture("_DustMask", maskTex);

                        var curtainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            Root + "/Textures/DeepSky/curtain_" + h.id + ".jpg");
                        if (curtainTex != null)
                        {
                            var cMat = SaveMaterial(h.id + "_CurtainMR", photoShader);
                            cMat.SetTexture("_MainTex", curtainTex);
                            cMat.SetFloat("_Exposure", 1.0f);
                            // Soft oval vignette: a hard photo edge floating in
                            // a real room reads as a screen, not a nebula.
                            cMat.SetFloat("_VignetteInner", 0.45f);
                            cMat.SetFloat("_VignetteOuter", 0.98f);

                            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                            quad.name = "Curtain";
                            quad.transform.SetParent(spec.transform, false);
                            Object.DestroyImmediate(quad.GetComponent<Collider>());
                            var qmr = quad.GetComponent<MeshRenderer>();
                            qmr.sharedMaterial = cMat;
                            qmr.shadowCastingMode = ShadowCastingMode.Off;
                            float w = h.radius * 4.6f;
                            quad.transform.localPosition = new Vector3(0f, 0f, h.radius * 1.6f);
                            quad.transform.localScale = new Vector3(w, w * 9f / 16f, 1f);
                        }
                    }

                    if (h.fieldN > 0 || h.brightN > 0)
                    {
                        var stars = spec.AddComponent<NebulaStars>();
                        stars.configureOnStart = true;
                        stars.cfgBright = h.starBright;
                        stars.cfgField = h.starField;
                        stars.cfgBrightN = h.brightN;
                        stars.cfgFieldN = h.fieldN;
                        stars.cfgRadius = h.radius;
                        stars.cfgCoreRadius = h.radius * h.coreFrac;
                    }
                }

                stage.specimens.Add(spec);
            }

            // Hand grab: move with one hand, scale with two (the galaxy recipe).
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 8f; // local units → ~0.44 m grab bubble
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackRotation = true;
            grab.trackScale = true;
            var transformer = root.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowTwoHandedScaling = true;

            var audio = cam.gameObject.AddComponent<MilkyWayAudio>();

            var tour = cam.gameObject.AddComponent<NebulaMRTour>();
            tour.stage = stage;

            var controls = cam.gameObject.AddComponent<NebulaMRControls>();
            controls.stage = stage;
            controls.tour = tour;
            tour.controls = controls;

            SaveAndRegister(scene, Root + "/Scenes/NebulaMR.unity");
            Selection.activeGameObject = root;
        }

        /// <summary>The MR front door: passthrough room, the shared world-space
        /// frame carrying language + three exhibit cards (MRTitleScreen), and a
        /// slowly turning galaxy miniature floating above the frame. Every MR
        /// menu's "처음으로" lands here; the desktop title screen hands off to
        /// this scene when it boots inside a headset.</summary>
        [MenuItem("Tools/Cosmos/Create MR Title Scene (Passthrough)")]
        public static void BuildMRTitle()
        {
            var volumeShader = Shader.Find("MilkyWay/GalaxyVolume");
            var starShader = Shader.Find("MilkyWay/GalaxyStars");
            if (!volumeShader || !starShader)
            {
                Debug.LogError("Milky Way shaders not compiled yet.");
                return;
            }

            var scene = NewMRScene("MRTitle");
            var cam = BuildXRRig();

            // Where the frame hangs: straight ahead of the spawn pose, a step
            // back — a poster at the exhibit entrance, not a HUD.
            var anchor = new GameObject("Title Frame Anchor");
            anchor.transform.position = new Vector3(0f, 1.5f, 2.0f);

            // Decor: the MR galaxy miniature, floating above the frame and
            // turning slowly. Same MR materials as the galaxy exhibit, so a
            // killed play session can never leak desktop values.
            var volumeMat = SaveMaterial("GalaxyVolumeMR", volumeShader);
            var starMat = SaveMaterial("GalaxyStarsMR", starShader);
            starMat.SetFloat("_NearFade", 0.08f * GalaxyScale);
            starMat.SetFloat("_SizeScale", 2.4f);

            var decor = new GameObject("Galaxy Decor (MR)");
            decor.transform.position = new Vector3(0f, 2.35f, 2.55f);
            decor.transform.rotation = Quaternion.Euler(-32f, 0f, 0f);
            decor.transform.localScale = Vector3.one * 0.016f; // ~0.51 m disk

            var volumeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volumeGO.name = "Galaxy Volume";
            volumeGO.transform.SetParent(decor.transform, false);
            Object.DestroyImmediate(volumeGO.GetComponent<Collider>());
            var volumeRenderer = volumeGO.GetComponent<MeshRenderer>();
            volumeRenderer.sharedMaterial = volumeMat;
            volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var starsGO = new GameObject("Galaxy Stars");
            starsGO.transform.SetParent(decor.transform, false);
            starsGO.AddComponent<GalaxyStarField>().material = starMat;

            var controller = decor.AddComponent<MilkyWayController>();
            controller.volumeMaterial = volumeMat;
            controller.starMaterial = starMat;
            controller.volumeRenderer = volumeRenderer;
            controller.Apply(); // AddComponent already ran OnEnable before wiring — the BH lesson

            var audio = cam.gameObject.AddComponent<MilkyWayAudio>();

            var screen = cam.gameObject.AddComponent<BlackHoleEffect.MRTitleScreen>();
            screen.frameAnchor = anchor.transform;
            screen.decor = decor.transform;

            SaveAndRegister(scene, "Assets/Scenes/MRTitle.unity");
            Selection.activeGameObject = anchor;
        }

        // ------------------------------------------------------------------
        //  shared scaffolding (the BlackHoleMR recipe)
        // ------------------------------------------------------------------

        static UnityEngine.SceneManagement.Scene NewMRScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);
            RenderSettings.fog = false;
            return scene;
        }

        static Camera BuildXRRig()
        {
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

            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            if (cam.GetComponent<AudioSource>() == null) cam.gameObject.AddComponent<AudioSource>();
            return cam;
        }

        /// <summary>Finds the XRI hands rig whatever sample version is
        /// imported — a hardcoded path silently degrades to a bare camera
        /// after any package upgrade.</summary>
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

        static void SaveAndRegister(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Any(s => s.path == path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("MR exhibit scene created: " + path);
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
    }
}
