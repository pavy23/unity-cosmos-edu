using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MilkyWay.Editor
{
    /// <summary>
    /// Builds the solar-system exhibit scene: the photoreal rig on its own
    /// stage (spawned at runtime by SolarSystemStage — the rig's meshes are
    /// generated, so the scene file stays lean), the planet tour, the scale
    /// truth, and ambient controls. The skybox is the BLACK-HOLE exhibit's
    /// StarfieldSkybox on purpose: its hard-coded Milky-Way band — wrong for
    /// the galaxy scene, where we ARE the geometry — is exactly the sky you
    /// see standing next to the Sun. Same menu-driven pattern as the other
    /// exhibits: the scene is a build artifact, never hand-edited.
    /// </summary>
    public static class SolarSystemSceneBuilder
    {
        const string Root = "Assets/MilkyWay";
        const string ScenePath = Root + "/Scenes/SolarSystemShowcase.unity";

        const string PanoramaPath = Root + "/Resources/NightProps/eso_milkyway_panorama.jpg";

        [MenuItem("Tools/Solar System/Create Showcase Scene")]
        public static void Build()
        {
            var skyShader = Shader.Find("Skybox/Panoramic");
            if (!skyShader)
            {
                Debug.LogError("Skybox/Panoramic shader not found.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "SolarSystemShowcase";

            // A REAL sky: standing next to the Sun, the background IS the Milky
            // Way. The bundled ESO all-sky panorama (already in Resources for the
            // night-sky experience — zero extra build cost) becomes an equirect
            // skybox, dimmed a little and rotated so the bright galactic bulge sits
            // off to one side rather than washing out the planets on display.
            var sky = SaveMaterial("SolarSkybox", skyShader);
            var pano = AssetDatabase.LoadAssetAtPath<Texture>(PanoramaPath);
            if (pano != null) sky.SetTexture("_MainTex", pano);
            sky.SetFloat("_Mapping", 1f);       // latitude-longitude layout
            sky.SetFloat("_ImageType", 0f);     // 360°
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.54f)); // dim so planets read
            sky.SetFloat("_Exposure", 1.0f);
            sky.SetFloat("_Rotation", 210f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            var stageGO = new GameObject("Solar System Stage");
            var stage = stageGO.AddComponent<SolarSystemStage>();

            // --- camera: overview of the whole display map ---------------------
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 55f, -125f);
            cam.transform.LookAt(Vector3.zero);
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            camGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camGO.AddComponent<AudioListener>();

            var orbit = camGO.AddComponent<BlackHoleEffect.CinematicOrbit>();
            orbit.target = stageGO.transform;
            orbit.orbitSpeed = 0.4f;
            orbit.bobAmplitude = 1.2f;
            orbit.bobPeriod = 52f;

            var tour = camGO.AddComponent<SolarSystemTour>();
            tour.orbit = orbit;
            tour.stage = stage;
            tour.toggleKeyLabel = "F1";

            var truth = camGO.AddComponent<ScaleTruth>();
            truth.orbit = orbit;
            truth.stage = stage;

            var audio = camGO.AddComponent<MilkyWayAudio>();

            var controls = camGO.AddComponent<SolarSystemControls>();
            controls.orbit = orbit;
            controls.tour = tour;
            controls.scaleTruth = truth;
            controls.audioScape = audio;
            controls.stage = stage;

            var toolbar = camGO.AddComponent<SolarToolbar>();
            toolbar.controls = controls;

            SetupPostProcessing();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = stageGO;
            Debug.Log("Solar system showcase created: " + ScenePath);
        }

        static Material SaveMaterial(string name, Shader shader)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old) AssetDatabase.DeleteAsset(path);
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void SetupPostProcessing()
        {
            const string profilePath = Root + "/Settings/SolarVolume.asset";
            // Reuse the existing (correct) profile — recreating it empties it
            // (the VolumeProfile Add<T>/CreateAsset trap). See MilkyWaySceneBuilder.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var bloom = profile.Add<Bloom>();
                bloom.active = true;
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(0.85f);
                bloom.scatter.Override(0.6f);
                bloom.highQualityFiltering.Override(true);
                var tonemapping = profile.Add<Tonemapping>();
                tonemapping.active = true;
                tonemapping.mode.Override(TonemappingMode.ACES);
                var vignette = profile.Add<Vignette>();
                vignette.active = true;
                vignette.intensity.Override(0.22f);
                vignette.smoothness.Override(0.7f);
                vignette.color.Override(Color.black);
                AssetDatabase.CreateAsset(profile, profilePath);
                foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
                AssetDatabase.SaveAssets();
            }

            var volumeGO = new GameObject("Post Processing");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;
        }
    }
}
