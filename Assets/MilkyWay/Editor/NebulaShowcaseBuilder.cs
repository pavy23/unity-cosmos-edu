using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MilkyWay.Editor
{
    /// <summary>
    /// Builds the "Nebulae &amp; Clusters" showcase: the hero objects from
    /// NebulaLibrary placed through space, a slow cinematic orbit, bloom, and a
    /// NebulaController the gallery drives. Menu-driven build artifact, like
    /// every other exhibit scene.
    /// </summary>
    public static class NebulaShowcaseBuilder
    {
        const string Root = "Assets/MilkyWay";
        const string ScenePath = Root + "/Scenes/NebulaShowcase.unity";

        [MenuItem("Tools/Nebula/Create Showcase Scene")]
        public static void Build()
        {
            var nebShader = Shader.Find("MilkyWay/NebulaVolume");
            var starShader = Shader.Find("MilkyWay/GalaxyStars");
            if (!nebShader || !starShader) { Debug.LogError("Nebula/star shaders not compiled yet."); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "NebulaShowcase";

            // A REAL sky: the bundled ESO all-sky panorama as an equirect skybox.
            // The gallery rotates it per specimen so each nebula sits against a
            // different, genuine region of the Milky Way (dimmed so the emission
            // volume still reads on top). Zero extra build cost — already in
            // Resources for the night-sky experience.
            var skyShader = Shader.Find("Skybox/Panoramic");
            if (skyShader != null)
            {
                var sky = SaveMaterial("NebulaSkybox", skyShader);
                var pano = AssetDatabase.LoadAssetAtPath<Texture>(
                    "Assets/MilkyWay/Resources/NightProps/eso_milkyway_panorama.jpg");
                if (pano != null) sky.SetTexture("_MainTex", pano);
                sky.SetFloat("_Mapping", 1f);   // latitude-longitude
                sky.SetFloat("_ImageType", 0f); // 360
                sky.SetColor("_Tint", new Color(0.34f, 0.34f, 0.38f));
                sky.SetFloat("_Exposure", 1f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            var stage = new GameObject("Nebula Stage");
            var controller = stage.AddComponent<NebulaController>();

            for (int i = 0; i < NebulaLibrary.Heroes.Length; i++)
            {
                var h = NebulaLibrary.Heroes[i];
                if (h.form == NebulaLibrary.Form.Nebula)
                    BuildNebula(stage.transform, controller, i, h, nebShader);
                else
                    BuildCluster(stage.transform, controller, i, h, starShader);
            }

            // --- camera --------------------------------------------------------
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 8f, -28f);
            cam.transform.LookAt(Vector3.zero);
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 800f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            camGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camGO.AddComponent<AudioListener>();

            var orbit = camGO.AddComponent<BlackHoleEffect.CinematicOrbit>();
            orbit.target = controller.Root(0);
            orbit.orbitSpeed = 0.4f;
            orbit.bobAmplitude = 0.5f;
            orbit.bobPeriod = 44f;

            var gallery = camGO.AddComponent<NebulaGallery>();
            gallery.controller = controller;
            gallery.orbit = orbit;

            var lifeTour = camGO.AddComponent<NebulaTour>();
            lifeTour.controller = controller;
            lifeTour.gallery = gallery;
            gallery.tour = lifeTour;

            camGO.AddComponent<MilkyWayAudio>();

            SetupPost();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene();
            Selection.activeGameObject = stage;
            Debug.Log("Nebula showcase created: " + ScenePath);
        }

        static void BuildNebula(Transform parent, NebulaController controller, int index,
            NebulaLibrary.Hero h, Shader nebShader)
        {
            var root = new GameObject(h.id);
            root.transform.SetParent(parent, false);
            root.transform.position = h.position;

            // Dark nebulae need something bright behind them to silhouette against.
            if (h.backdrop)
            {
                var bgMat = SaveMaterial(h.id + "_Backdrop", nebShader);
                var backHero = h;
                backHero.type = 0; // emission
                // A pink IC 434-style glow (distinct from Orion's orange) kept
                // dimmer than before, so the dark dust — not the glow — dominates.
                backHero.color1 = new Color(2.3f, 0.4f, 0.72f);
                backHero.color2 = new Color(0.6f, 0.9f, 1.2f);
                backHero.brightness = 1.15f; backHero.radius = h.radius * 1.25f; backHero.density = 0.8f;
                backHero.noiseScale = 0.22f; backHero.filament = 1.5f; backHero.threshold = 0.55f;
                backHero.dust = 1.0f; backHero.stretch = Vector3.zero;
                NebulaLibrary.ApplyMaterial(bgMat, backHero);
                var bg = MakeVolume(root.transform, "Backdrop", bgMat);
                // A contained glow just behind and above — only enough to silhouette
                // the dust against; the dark dust, not the glow, should dominate.
                bg.transform.localPosition = new Vector3(0f, 1.3f, h.radius * 1.1f);
            }

            var mat = SaveMaterial(h.id + "_Neb", nebShader);
            NebulaLibrary.ApplyMaterial(mat, h);
            var vol = MakeVolume(root.transform, "Volume", mat);
            // Non-uniform scale (e.g. the Crab's oval) lives on the volume only, so
            // the sibling star field stays undistorted.
            if (h.stretch != Vector3.zero) vol.transform.localScale = h.stretch;

            if (h.fieldN > 0 || h.brightN > 0)
            {
                var stars = root.AddComponent<NebulaStars>();
                stars.configureOnStart = true;
                stars.cfgBright = h.starBright; stars.cfgField = h.starField;
                stars.cfgBrightN = h.brightN; stars.cfgFieldN = h.fieldN;
                stars.cfgRadius = h.radius; stars.cfgCoreRadius = h.radius * h.coreFrac;
            }

            controller.Add(root.transform, index, h.radius);
        }

        static GameObject MakeVolume(Transform parent, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static void BuildCluster(Transform parent, NebulaController controller, int index,
            NebulaLibrary.Hero h, Shader starShader)
        {
            var starMat = SaveMaterial(h.id + "_Stars", starShader);
            // Lower brightness for globulars so the warm giant colours read instead
            // of clipping to white through bloom; open clusters can stay punchy.
            starMat.SetFloat("_StarBrightness", h.clusterKind == ClusterField.Kind.Globular ? 1.05f : 1.4f);
            starMat.SetFloat("_SizeScale", 1.6f);
            starMat.SetFloat("_NearFade", 0.05f);

            var go = new GameObject(h.id, typeof(MeshFilter), typeof(MeshRenderer), typeof(ClusterField));
            go.transform.SetParent(parent, false);
            go.transform.position = h.position;
            var clu = go.GetComponent<ClusterField>();
            clu.kind = h.clusterKind;
            clu.material = starMat;
            clu.radius = h.clusterRadius;
            clu.starCount = h.clusterStars;

            controller.Add(go.transform, index, h.clusterRadius);
        }

        static void SetupPost()
        {
            var post = new GameObject("Post Processing").AddComponent<Volume>();
            post.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.0f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);
            var tm = profile.Add<Tonemapping>();
            tm.active = true; tm.mode.Override(TonemappingMode.ACES);
            post.profile = profile;
        }

        static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
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
