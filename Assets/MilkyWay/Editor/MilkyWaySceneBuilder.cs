using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MilkyWay.Editor
{
    /// <summary>
    /// Builds the Milky Way showcase scene: the hybrid galaxy (raymarched
    /// volume + baked starfield), a near-empty intergalactic skybox, a slow
    /// cinematic orbit and ACES + bloom. Same menu-driven pattern as the black
    /// hole builder — the scene is a build artifact, never hand-edited.
    /// </summary>
    public static class MilkyWaySceneBuilder
    {
        const string Root = "Assets/MilkyWay";
        const string ScenePath = Root + "/Scenes/MilkyWayShowcase.unity";

        [MenuItem("Tools/Milky Way/Create Showcase Scene")]
        public static void Build()
        {
            var volumeShader = Shader.Find("MilkyWay/GalaxyVolume");
            var starShader = Shader.Find("MilkyWay/GalaxyStars");
            if (!volumeShader || !starShader)
            {
                Debug.LogError("Milky Way shaders not compiled yet — try again after the import finishes.");
                return;
            }

            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Settings");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MilkyWayShowcase";

            // --- deep space: our own skybox. The black-hole one is unusable
            // here — it hard-codes a Milky-Way band into the sky, and we ARE
            // the Milky Way, as geometry.
            var skyShader = Shader.Find("MilkyWay/DeepSpaceSkybox");
            if (skyShader != null)
            {
                var sky = SaveMaterial("DeepSpaceSkybox", skyShader);
                sky.SetFloat("_StarDensity", 0.35f);
                sky.SetFloat("_GalaxyCount", 0.5f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            // --- the galaxy ---------------------------------------------------
            var volumeMat = SaveMaterial("GalaxyVolume", volumeShader);
            var starMat = SaveMaterial("GalaxyStars", starShader);

            var root = new GameObject("Milky Way");
            root.transform.position = Vector3.zero;

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

            // --- camera: classic three-quarter reveal -------------------------
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 16f, -30f);
            cam.transform.LookAt(Vector3.zero);
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            camGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camGO.AddComponent<AudioListener>();

            var orbit = camGO.AddComponent<BlackHoleEffect.CinematicOrbit>();
            orbit.target = root.transform;
            orbit.orbitSpeed = 0.5f;
            orbit.bobAmplitude = 0.5f;
            orbit.bobPeriod = 46f;

            SetupPostProcessing();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("Milky Way showcase created: " + ScenePath);
        }

        static void SetupPostProcessing()
        {
            const string profilePath = Root + "/Settings/MilkyWayVolume.asset";
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (old) AssetDatabase.DeleteAsset(profilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.threshold.Override(1.1f);   // only the genuinely bright cores bloom
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.66f);
            bloom.highQualityFiltering.Override(true);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.7f);
            vignette.color.Override(Color.black);

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
