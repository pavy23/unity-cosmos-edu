using System.IO;
using UnityEditor;
using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// Runs a build without anyone touching the menu.
    ///
    /// Driving the Editor by simulated mouse clicks turned out to be unusable
    /// here (the menu bar clips when the window is narrow, the coordinate space
    /// is DPI-scaled, and a stray click can land in whatever window happens to
    /// be in front). This does the same job through the asset pipeline instead:
    /// drop a one-word request file, let Unity notice it on focus, and the
    /// build runs itself.
    ///
    /// Two-phase by design. A target switch queues a domain reload, and the
    /// assemblies we are running from die with it — so the first pass only
    /// switches and leaves the request in place, and the pass that comes back
    /// after the reload finds the target already correct and builds.
    ///
    ///   echo webgl   &gt; Temp/cosmos-autobuild.txt
    ///   echo android &gt; Temp/cosmos-autobuild.txt
    ///   echo windows &gt; Temp/cosmos-autobuild.txt
    ///
    /// The file lives in Temp/ so it is neither imported as an asset nor
    /// committed. It is deleted before the build starts, so a build that
    /// crashes the Editor cannot loop on restart.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoBuildTrigger
    {
        const string RequestPath = "Temp/cosmos-autobuild.txt";

        static AutoBuildTrigger()
        {
            // Never during play mode, and never mid-compile: defer to the first
            // idle tick so BuildPlayer sees a settled editor.
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Check;   // try again once settled
                return;
            }

            var request = File.ReadAllText(RequestPath).Trim().ToLowerInvariant();
            BuildTarget target;
            switch (request)
            {
                case "webgl":   target = BuildTarget.WebGL; break;
                case "android": target = BuildTarget.Android; break;
                case "windows": target = BuildTarget.StandaloneWindows64; break;
                default:
                    Debug.LogError($"[AutoBuild] Unknown request '{request}' — deleting.");
                    File.Delete(RequestPath);
                    return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                // Phase 1: switch only. Leave the request file so the reload
                // that follows brings us back here with the target correct.
                Debug.Log($"[AutoBuild] Switching {EditorUserBuildSettings.activeBuildTarget} -> {target}; " +
                          "build resumes after the domain reload.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildPipeline.GetBuildTargetGroup(target), target);
                return;
            }

            // Phase 2: the target is right. Consume the request first — if this
            // build takes down the Editor we must not run it again on restart.
            File.Delete(RequestPath);
            Debug.Log($"[AutoBuild] Building {target}…");

            switch (target)
            {
                case BuildTarget.WebGL:              WebGLSiteBuild.Build(); break;
                case BuildTarget.Android:            WebGLSiteBuild.BuildAndroid(); break;
                case BuildTarget.StandaloneWindows64: WebGLSiteBuild.BuildWindows(); break;
            }
        }
    }
}
