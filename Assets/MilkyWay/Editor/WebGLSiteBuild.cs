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
            Debug.LogError(
                $"[Build] Active target is {EditorUserBuildSettings.activeBuildTarget}, " +
                $"not {target}. Switch in File > Build Profiles (or pass " +
                $"-buildTarget on the command line), let scripts recompile, " +
                $"then run this menu item again — platform #ifs are compiled " +
                $"against the ACTIVE target, not the one passed to BuildPlayer.");
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

        /// <summary>The Quest passthrough APK: all scenes (TitleScreen boots,
        /// detects the HMD and hands off to MRTitle). IL2CPP/ARM64 and the
        /// OpenXR feature set come from the project settings.</summary>
        [MenuItem("Tools/Cosmos/Build Android (Quest APK)")]
        public static void BuildAndroid()
        {
            if (!RequireActiveTarget(BuildTarget.Android)) return;
            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(
                    System.Array.FindAll(EditorBuildSettings.scenes, sc => sc.enabled), sc => sc.path),
                target = BuildTarget.Android,
                locationPathName = "Builds/Android/CosmosEdu.apk",
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[AndroidBuild] {s.result} — {s.totalSize / (1024f * 1024f):F1} MB, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalMinutes:F1} min");
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

