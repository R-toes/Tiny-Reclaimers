using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrashTD.Editor
{
    /// <summary>
    /// One-click Android setup and build (TRASH TD > Android). "Apply Android Settings" sets the Player Settings the
    /// phone version needs (landscape only, IL2CPP, 64-bit, Android 8+, package name). "Build Android APK" applies
    /// them, builds the scenes in Build Settings and saves the file where the person chooses. Building needs the
    /// "Android Build Support" module of this Unity version (Unity Hub > Installs > Add modules, with its OpenJDK and
    /// Android SDK &amp; NDK Tools).
    /// </summary>
    public static class AndroidSetup
    {
        public const string PackageName = "com.coinkeeper.tinyreclaimerstd";
        public const AndroidSdkVersions MinimumSdk = AndroidSdkVersions.AndroidApiLevel26;

        [MenuItem("TRASH TD/Android/Apply Android Settings")]
        public static void ApplyFromMenu()
        {
            List<string> changes = Apply();
            string message = changes.Count == 0
                ? "The Android settings were already correct."
                : "Changed:\n- " + string.Join("\n- ", changes);
            EditorUtility.DisplayDialog("Android settings", message, "OK");
        }

        /// <summary>Sets the Android Player Settings and returns a line for each value that was changed.</summary>
        public static List<string> Apply()
        {
            var changes = new List<string>();

            // Landscape only: the HUD is laid out for a wide screen, so portrait is switched off.
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                changes.Add("Orientation: auto-rotate between the two landscape sides");
            }
            if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                !PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                changes.Add("Portrait switched off");
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                changes.Add("Scripting backend: IL2CPP");
            }
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                changes.Add("Architecture: ARM64");
            }
            if (PlayerSettings.Android.minSdkVersion != MinimumSdk)
            {
                PlayerSettings.Android.minSdkVersion = MinimumSdk;
                changes.Add("Minimum Android version: 8.0 (API 26)");
            }
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != PackageName)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
                changes.Add("Package name: " + PackageName);
            }
            if (!PlayerSettings.Android.startInFullscreen)
            {
                PlayerSettings.Android.startInFullscreen = true;
                changes.Add("Start in fullscreen");
            }

            // Sustained Performance Mode: the phone holds a clock it can keep up, so the frame rate stays steady
            // instead of starting fast and dropping when it heats up (matters most with the CRT effects on).
            if (SetSustainedPerformanceMode(true)) changes.Add("Sustained Performance Mode: on");

            if (changes.Count > 0) AssetDatabase.SaveAssets();
            return changes;
        }

        /// <summary>
        /// Player Settings > Other Settings > Configuration > Sustained Performance Mode. Unity has no public setter, so
        /// the serialized field is written. Returns true when the value changed.
        /// </summary>
        private static bool SetSustainedPerformanceMode(bool enabled)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;

            var settings = new SerializedObject(assets[0]);
            SerializedProperty property = settings.FindProperty("AndroidEnableSustainedPerformanceMode");
            if (property == null || property.boolValue == enabled) return false;

            property.boolValue = enabled;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        [MenuItem("TRASH TD/Android/Build Android APK...")]
        public static void BuildApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Android Build Support is missing",
                    "Install the Android Build Support module for this Unity version first:\n\n" +
                    "Unity Hub > Installs > (this version) > Add modules > Android Build Support, with\n" +
                    "OpenJDK and Android SDK & NDK Tools ticked. Then run this menu item again.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Save the Android build", Directory.GetCurrentDirectory(), "TinyReclaimersTD", "apk");
            if (string.IsNullOrEmpty(path)) return;

            Apply();
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Android build finished",
                    "Saved " + path + "\n(" + (report.summary.totalSize / (1024 * 1024)) + " MB). Copy it to the phone and open it " +
                    "(allow installing from this source), or run: adb install -r \"" + path + "\"", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Android build failed", "Result: " + report.summary.result + ". See the Console for the errors.", "OK");
            }
        }
    }
}
