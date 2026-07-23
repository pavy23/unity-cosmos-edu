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
            var photoShader = Shader.Find("MilkyWay/PhotoBackdrop");
            if (!nebShader || !starShader || !photoShader) { Debug.LogError("Nebula/star/photo shaders not compiled yet."); return; }

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
                Transform root = h.form switch
                {
                    NebulaLibrary.Form.Nebula => BuildNebula(stage.transform, controller, i, h, nebShader),
                    NebulaLibrary.Form.Photo => BuildPhoto(stage.transform, controller, i, h, photoShader),
                    _ => BuildCluster(stage.transform, controller, i, h, starShader),
                };
                // Every nebula gets a real DSS2 wide-field of its own region
                // behind it. Not the photo exhibit (it IS the photo), and not
                // the cluster: the DSS2 plates seam right beside ω Cen, so it
                // keeps the panoramic skybox alone.
                if (h.form == NebulaLibrary.Form.Nebula)
                    AddBackdrop(root, h, photoShader);
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

        static Transform BuildNebula(Transform parent, NebulaController controller, int index,
            NebulaLibrary.Hero h, Shader nebShader)
        {
            var root = new GameObject(h.id);
            root.transform.SetParent(parent, false);
            root.transform.position = h.position;

            var mat = SaveMaterial(h.id + "_Neb", nebShader);
            NebulaLibrary.ApplyMaterial(mat, h);
            var vol = MakeVolume(root.transform, "Volume", mat);
            // Non-uniform scale (e.g. the Crab's oval) lives on the volume only, so
            // the sibling star field stays undistorted.
            if (h.stretch != Vector3.zero) vol.transform.localScale = h.stretch;
            // The dark nebula is a staged diorama (glowing curtain at +z, dust
            // pillar in front of it): face its -z at the parked gallery camera,
            // and feed it the silhouette mask baked from the real photograph.
            if (h.type == 4)
            {
                vol.transform.localRotation = Quaternion.LookRotation(-NebulaLibrary.ViewDir);
                var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Root + "/Textures/DeepSky/mask_" + h.id + ".png");
                if (maskTex != null) mat.SetTexture("_DustMask", maskTex);
                else Debug.LogWarning("Missing dust silhouette mask for " + h.id);
            }

            if (h.fieldN > 0 || h.brightN > 0)
            {
                var stars = root.AddComponent<NebulaStars>();
                stars.configureOnStart = true;
                stars.cfgBright = h.starBright; stars.cfgField = h.starField;
                stars.cfgBrightN = h.brightN; stars.cfgFieldN = h.fieldN;
                stars.cfgRadius = h.radius; stars.cfgCoreRadius = h.radius * h.coreFrac;
            }

            controller.Add(root.transform, index, h.radius);
            return root.transform;
        }

        /// <summary>The Horsehead exhibit: a genuine DSS2 photograph of B33 /
        /// IC 434 on a camera-facing quad filling the parked view — pushed back
        /// behind the specimen position so the twinkling 3D star field placed
        /// in front of it parallaxes against it under the gallery drift.</summary>
        static Transform BuildPhoto(Transform parent, NebulaController controller, int index,
            NebulaLibrary.Hero h, Shader photoShader)
        {
            var root = new GameObject(h.id);
            root.transform.SetParent(parent, false);
            root.transform.position = h.position;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Root + "/Textures/DeepSky/hero_" + h.id + ".jpg");
            if (tex == null) Debug.LogError("Missing hero photo for " + h.id);

            var mat = SaveMaterial(h.id + "_Photo", photoShader);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Exposure", 1.25f);
            // No vignette — the photo runs past the frame edges.
            mat.SetFloat("_VignetteInner", 2.5f);
            mat.SetFloat("_VignetteOuter", 3f);

            float back = h.radius * 1.6f;                       // photo sits behind the stars
            float D = h.radius * h.framing + back;              // its distance from the camera
            // Cover the camera frustum (42° vertical, 16:9) with ~20% margin —
            // enough overscan for the ±1.6° gallery drift.
            float w = 2f * D * 0.6825f * 1.2f;
            var quad = MakeQuad(root.transform, "Photo", mat);
            quad.transform.localScale = new Vector3(w, w * 9f / 16f, 1f);
            // The gallery aims 0.16 radii below the object centre; keep the
            // photo centred on that look point.
            quad.transform.localPosition = -NebulaLibrary.ViewDir * back + Vector3.down * h.radius * 0.16f;
            quad.transform.localRotation = Quaternion.LookRotation(-NebulaLibrary.ViewDir);

            // The 3D layer: near stars floating between camera and photograph.
            if (h.fieldN > 0 || h.brightN > 0)
            {
                var stars = root.AddComponent<NebulaStars>();
                stars.configureOnStart = true;
                stars.cfgBright = h.starBright; stars.cfgField = h.starField;
                stars.cfgBrightN = h.brightN; stars.cfgFieldN = h.fieldN;
                stars.cfgRadius = h.radius * 0.85f;             // all in front of the quad
                stars.cfgCoreRadius = h.radius * h.coreFrac;
            }

            controller.Add(root.transform, index, h.radius);
            return root.transform;
        }

        /// <summary>A dimmed, vignetted DSS2 wide-field of the specimen's real
        /// surroundings, parked behind it and facing the gallery camera. The dark
        /// nebula instead gets a FULL-FRAME curtain: the real photograph with the
        /// pillar inpainted away (the volumetric pillar stands in front of it),
        /// so the exhibit fills the view edge to edge like the astrophotos.</summary>
        static void AddBackdrop(Transform root, NebulaLibrary.Hero h, Shader photoShader)
        {
            bool curtain = h.type == 4;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Root + "/Textures/DeepSky/" + (curtain ? "curtain_" : "bg_") + h.id + ".jpg");
            if (tex == null) { Debug.LogWarning("No DSS2 backdrop for " + h.id); return; }

            float r = h.form == NebulaLibrary.Form.Cluster ? h.clusterRadius : h.radius;
            var mat = SaveMaterial(h.id + "_Bg", photoShader);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Exposure", h.bgDim <= 0f ? 0.55f : h.bgDim);
            mat.SetFloat("_VignetteInner", curtain ? 2.5f : 0.45f);   // no vignette on the curtain
            mat.SetFloat("_VignetteOuter", curtain ? 3f : 0.98f);

            float back = r * (curtain ? 2.2f : 2.4f);            // behind the object
            float D = r * h.framing + back;                      // distance from camera
            float S = 2f * D * 0.6825f * 1.35f;                  // cover width + vignette margin
            var quad = MakeQuad(root, "Deep Sky Backdrop", mat);
            quad.transform.localPosition = -NebulaLibrary.ViewDir * back + Vector3.down * r * 0.16f;
            quad.transform.localRotation = Quaternion.LookRotation(-NebulaLibrary.ViewDir);
            // 16:9 photo: cover the wide frustum, keep the aspect.
            quad.transform.localScale = curtain ? new Vector3(S * 1.1f, S * 1.1f * 9f / 16f, 1f)
                                                : new Vector3(S, S, 1f);
        }

        static GameObject MakeQuad(Transform parent, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
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

        static Transform BuildCluster(Transform parent, NebulaController controller, int index,
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
            return go.transform;
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
