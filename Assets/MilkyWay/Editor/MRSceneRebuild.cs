using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilkyWay.Editor
{
    /// <summary>
    /// Regenerates all five MR scenes in one go, from the menu or from a
    /// request file.
    ///
    /// The scenes are build artifacts — every one of them is authored by a
    /// Create MR Scene menu item and never hand-edited — so any change to a
    /// builder leaves all five stale until someone remembers to re-run them
    /// one at a time. Five menu trips in the right order is exactly the kind
    /// of chore that gets half-done, and a half-done set ships an exhibit
    /// wired for code that no longer exists.
    ///
    /// The request file is the same trick <see cref="AutoBuildTrigger"/> uses,
    /// and for the same reason: driving the Editor by simulated clicks is not
    /// reliable here, but dropping a file and letting Unity notice it on focus
    /// is. Unlike a player build this needs no target switch, so one pass does
    /// the whole job.
    ///
    ///   echo scenes &gt; Temp/cosmos-rebuild.txt
    ///
    /// Temp/ is neither imported as an asset nor committed. The request is
    /// consumed before the first scene is built, so a rebuild that takes the
    /// Editor down cannot loop on restart.
    /// </summary>
    [InitializeOnLoad]
    public static class MRSceneRebuild
    {
        const string RequestPath = "Temp/cosmos-rebuild.txt";

        static MRSceneRebuild()
        {
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
            if (request != "scenes")
            {
                Debug.LogError($"[MRRebuild] Unknown request '{request}' — deleting.");
                File.Delete(RequestPath);
                return;
            }

            File.Delete(RequestPath);
            RebuildAll();
        }

        [MenuItem("Tools/Cosmos/Rebuild All MR Scenes")]
        public static void RebuildAll()
        {
            // Each builder opens a new single scene, which would silently throw
            // away whatever is open. Ask once, here, rather than five times.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[MRRebuild] Cancelled — the open scene has unsaved changes.");
                return;
            }

            // Title last: it is the scene a visitor lands in, so leaving it
            // open is the most useful place to be when the run finishes.
            BlackHoleEffect.Editor.BlackHoleSceneBuilder.BuildMR();
            MRExhibitSceneBuilder.BuildMilkyWayMR();
            MRExhibitSceneBuilder.BuildSolarSystemMR();
            MRExhibitSceneBuilder.BuildNebulaMR();
            MRExhibitSceneBuilder.BuildMRTitle();

            AssetDatabase.SaveAssets();
            Debug.Log("[MRRebuild] All five MR scenes regenerated.");
        }
    }
}
