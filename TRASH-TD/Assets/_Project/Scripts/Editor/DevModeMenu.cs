using UnityEditor;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Development;
using TrashTD.Systems;

namespace TrashTD.Editor
{
    /// <summary>
    /// TRASH TD → Dev Mode toggles developer tools in the Editor (see <see cref="DevMode"/>).
    /// TRASH TD → Reset Player Progress makes this machine a first-time player again (tutorial, stage unlocks, stars).
    /// TRASH TD → Open Save Folder shows where the JSON save lives (see <see cref="SaveSystem"/>).
    /// </summary>
    public static class DevModeMenu
    {
        private const string DevModeMenuPath = "TRASH TD/Dev Mode";

        [MenuItem(DevModeMenuPath, false, 0)]
        private static void ToggleDevMode()
        {
            bool enabled = !DevMode.IsEnabled;
            EditorPrefs.SetBool(DevMode.EditorPrefKey, enabled);
            Debug.Log(enabled
                ? "[DevMode] ON: QA Sandbox and the developer picker are available."
                : "[DevMode] OFF: player mode. Dev tools are hidden; re-enter the menu or press Play to see it.");
        }

        [MenuItem(DevModeMenuPath, true)]
        private static bool ToggleDevModeValidate()
        {
            Menu.SetChecked(DevModeMenuPath, DevMode.IsEnabled);
            return true;
        }

        [MenuItem("TRASH TD/Reset Player Progress", false, 1)]
        private static void ResetPlayerProgress()
        {
            if (!EditorUtility.DisplayDialog("Reset Player Progress",
                    "Delete the save file on this machine (tutorial completion, stage clears and stars, enemies met in Intel)?\nAudio and graphics settings are kept.",
                    "Reset", "Cancel"))
                return;

            PlayerProgress.ResetAll();
            Debug.Log("[DevMode] Player progress reset: the save file was deleted; tutorial, stage unlocks and Intel enemy files start over (turn Dev Mode off to see the locks).");
        }

        [MenuItem("TRASH TD/Open Save Folder", false, 2)]
        private static void OpenSaveFolder()
        {
            string path = SaveSystem.SaveFilePath;
            if (System.IO.File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                return;
            }

            // No save yet: open the folder it will be written to
            string folder = SaveSystem.SaveFolder;
            System.IO.Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder + System.IO.Path.DirectorySeparatorChar);
            Debug.Log("[Save] No save file yet. It will be " + path);
        }
    }
}
