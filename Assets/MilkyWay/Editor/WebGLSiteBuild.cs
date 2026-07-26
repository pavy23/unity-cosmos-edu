using UnityEditor;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// WebGL build for the hosted site. Unlike Build Profiles (which share the
    /// global scene list), this builds the five desktop scenes only: the MR
    /// scenes are inert in a browser but would drag the XR sample assets
    /// (hand recordings, demo textures, ~14 MB) into the data file.
    /// Also runnable headless:
    ///   Unity -batchmode -quit -executeMethod MilkyWay.WebGLSiteBuild.Build
    /// </summary>
    public static class WebGLSiteBuild
    {
        /// <summary>Refuse to build a platform that is not the active target.
        ///
        /// BuildPlayer will switch, but the player scripts are compiled with
        /// the defines of whatever target was active — so building Android
        /// straight from the Web target can bake UNITY_WEBGL branches into the
        /// APK (this project has several, e.g. the nebula march-step cap).
        /// Switching here instead would strand the build behind a domain
        /// reload, so we ask the operator to switch and re-run.</summary>
        static bool RequireActiveTarget(BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target) return true;

            // Switch, then stop. The switch queues a script recompile and a
            // domain reload; continuing into BuildPlayer in this same call
            // would build with the assemblies we were loaded from, which is
            // the whole problem. Re-running the menu item after the reload
            // takes the fast path above.
            Debug.Log($"[Build] Switching active target " +
                      $"{EditorUserBuildSettings.activeBuildTarget} -> {target}. " +
                      $"Run this menu item again once scripts finish recompiling.");
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildPipeline.GetBuildTargetGroup(target), target);
            return false;
        }

        static readonly string[] Scenes =
        {
            "Assets/Scenes/TitleScreen.unity",
            "Assets/BlackHoleEffect/Scenes/BlackHoleShowcase.unity",
            "Assets/MilkyWay/Scenes/MilkyWayShowcase.unity",
            "Assets/MilkyWay/Scenes/SolarSystemShowcase.unity",
            "Assets/MilkyWay/Scenes/NebulaShowcase.unity",
        };

        [MenuItem("Tools/Cosmos/Build WebGL (Site, desktop scenes)")]
        public static void Build()
        {
            if (!RequireActiveTarget(BuildTarget.WebGL)) return;
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                target = BuildTarget.WebGL,
                locationPathName = "Builds/WebGL",
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[WebGLSiteBuild] {s.result} — {s.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalMinutes:F1} min");
        }

        const string MRTitleScene = "Assets/Scenes/MRTitle.unity";

        /// <summary>The Quest passthrough APK: all scenes, but booting MRTitle.
        /// IL2CPP/ARM64 and the OpenXR feature set come from the project settings.
        ///
        /// It used to boot TitleScreen like every other platform and hand off to
        /// MRTitle once it detected the headset. On device that detection read
        /// false, and what a visitor got was the desktop picker as a flat sheet
        /// across both eyes — no passthrough, and no hand-ray target to escape
        /// with. A build whose only front door is the MR one cannot fail that
        /// way, so the probe (since made robust) is no longer load bearing here.</summary>
        [MenuItem("Tools/Cosmos/Build Android (Quest APK)")]
        public static void BuildAndroid()
        {
            if (!RequireActiveTarget(BuildTarget.Android)) return;
            var options = new BuildPlayerOptions
            {
                scenes = QuestScenes(),
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/CosmosEdu.apk",
                options = BuildOptions.None,
            };
            // Say which scene boots. It is the whole point of this method's scene
            // handling, it is invisible in the APK, and getting it wrong shipped a
            // headset build stuck on the desktop picker.
            Debug.Log($"[AndroidBuild] boot scene: {options.scenes[0]}");

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[AndroidBuild] {s.result} — {s.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalMinutes:F1} min");
        }

        /// <summary>Every enabled scene, with MRTitle moved to index 0 so the
        /// APK boots the passthrough picker. Errors rather than silently shipping
        /// the desktop boot scene: a Quest build that starts anywhere else is the
        /// bug this exists to prevent.</summary>
        static string[] QuestScenes()
        {
            var enabled = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, sc => sc.enabled), sc => sc.path);
            int at = System.Array.IndexOf(enabled, MRTitleScene);
            if (at < 0)
                throw new UnityEditor.Build.BuildFailedException(
                    MRTitleScene + " is not an enabled scene in Build Settings, so the APK " +
                    "would boot the desktop picker — unusable in a headset. Create it with " +
                    "Tools/Cosmos/Create MR Title Scene (Passthrough) and enable it.");
            if (at > 0)
            {
                System.Array.Copy(enabled, 0, enabled, 1, at);
                enabled[0] = MRTitleScene;
            }
            return enabled;
        }

        /// <summary>The full exhibit (all scenes, MR included — inert without
        /// a headset) for the Windows player. Same slimmed content as the web
        /// build: narration at Vorbis 35%, no splash, no Sentis.</summary>
        [MenuItem("Tools/Cosmos/Build Windows (full exhibit)")]
        public static void BuildWindows()
        {
            if (!RequireActiveTarget(BuildTarget.StandaloneWindows64)) return;
            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(
                    System.Array.FindAll(EditorBuildSettings.scenes, sc => sc.enabled), sc => sc.path),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/Windows/CosmosEdu.exe",
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[WindowsBuild] {s.result} — {s.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalMinutes:F1} min");
        }
    }
}

