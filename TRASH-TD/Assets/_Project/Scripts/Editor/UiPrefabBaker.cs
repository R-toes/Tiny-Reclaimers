using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.UI;
using Object = UnityEngine.Object;

namespace TrashTD.Editor
{
    /// <summary>
    /// TRASH TD > UI > Bake ... saves a panel that is normally built in code as a prefab, so its layout (positions, sizes,
    /// colours, text) can be edited in Prefab Mode instead of by changing pixel offsets in code and entering Play mode.
    ///
    /// How it works: the panel's class builds the same hierarchy it always did, with a stable name on every element it
    /// needs to find. The bake saves that hierarchy under Resources. At runtime the class instantiates the prefab when it
    /// exists (and builds in code when it does not), then finds its elements by name and attaches everything dynamic:
    /// listeners, sounds, the exact button frame sprites, animated diagrams. So the prefab holds layout only, and:
    /// - rename or delete a named element and the game logs an error and falls back to the code layout;
    /// - delete the prefab (or use the Remove command) to go back to the code layout;
    /// - after changing the layout code, bake again (this replaces the prefab, so edits made to it are lost).
    ///
    /// Sprites made at runtime (the 9-sliced button frames are created in code with a border) cannot be stored in a
    /// prefab; the bake swaps each for the matching sprite asset so the prefab shows the art. A swapped frame has no
    /// 9-slice border, so it looks stretched in Prefab Mode; in the game the exact sprite is applied again.
    /// </summary>
    public static class UiPrefabBaker
    {
        private const string MenuRoot = "TRASH TD/UI/";
        private const string PrefabFolder = "Assets/Resources/UI";

        [MenuItem(MenuRoot + "Bake Settings Panel Prefab", false, 20)]
        private static void BakeSettingsPanel()
        {
            Bake("Settings Panel", AudioSettingsUI.PrefabAssetPath, "SettingsPanel", canvas =>
            {
                GameObject panel = AudioSettingsUI.BuildPanelForPrefab(canvas);
                string missing = null;
                bool ok = AudioSettingsUI.ValidatePanel(panel, out missing);
                return (panel, ok, missing);
            });
        }

        [MenuItem(MenuRoot + "Bake Pause Panel Prefab", false, 22)]
        private static void BakePausePanel()
        {
            Bake("Pause Panel", GameplayHUDUI.PausePanelPrefabAssetPath, "PausePanel", canvas =>
            {
                GameObject panel = GameplayHUDUI.BuildPausePanelForPrefab(canvas);
                bool ok = GameplayHUDUI.ValidatePausePanel(panel, out string missing);
                return (panel, ok, missing);
            });
        }

        [MenuItem(MenuRoot + "Bake Operator Death Notice Prefab", false, 24)]
        private static void BakeOperatorDeathNotice()
        {
            Bake("Operator Death Notice", OperatorDeathNoticeUI.PrefabAssetPath, "OperatorDeathNotice", canvas =>
            {
                GameObject plate = OperatorDeathNoticeUI.BuildPlateForPrefab(canvas);
                bool ok = OperatorDeathNoticeUI.ValidatePlate(plate, out string missing);
                return (plate, ok, missing);
            });
        }

        [MenuItem(MenuRoot + "Bake Discard Confirmation Prefab", false, 26)]
        private static void BakeDiscardConfirmation()
        {
            Bake("Discard Confirmation", GameplayHUDUI.DiscardPanelPrefabAssetPath, "DiscardConfirmationPanel", canvas =>
            {
                GameObject panel = GameplayHUDUI.BuildDiscardPanelForPrefab(canvas);
                bool ok = GameplayHUDUI.ValidateDiscardPanel(panel, out string missing);
                return (panel, ok, missing);
            });
        }

        [MenuItem(MenuRoot + "Bake Cooldown Toast Prefab", false, 28)]
        private static void BakeCooldownToast()
        {
            Bake("Cooldown Toast", GameplayHUDUI.CooldownToastPrefabAssetPath, "CooldownToast", canvas =>
            {
                GameObject toast = GameplayHUDUI.BuildCooldownToastForPrefab(canvas);
                bool ok = GameplayHUDUI.ValidateCooldownToast(toast, out string missing);
                return (toast, ok, missing);
            });
        }

        [MenuItem(MenuRoot + "Remove Baked Operator Death Notice (use code layout)", false, 25)]
        private static void RemoveOperatorDeathNotice() => Remove("Operator Death Notice", OperatorDeathNoticeUI.PrefabAssetPath);

        [MenuItem(MenuRoot + "Remove Baked Discard Confirmation (use code layout)", false, 27)]
        private static void RemoveDiscardConfirmation() => Remove("Discard Confirmation", GameplayHUDUI.DiscardPanelPrefabAssetPath);

        [MenuItem(MenuRoot + "Remove Baked Cooldown Toast (use code layout)", false, 29)]
        private static void RemoveCooldownToast() => Remove("Cooldown Toast", GameplayHUDUI.CooldownToastPrefabAssetPath);

        [MenuItem(MenuRoot + "Remove Baked Settings Panel (use code layout)", false, 21)]
        private static void RemoveSettingsPanel() => Remove("Settings Panel", AudioSettingsUI.PrefabAssetPath);

        [MenuItem(MenuRoot + "Remove Baked Pause Panel (use code layout)", false, 23)]
        private static void RemovePausePanel() => Remove("Pause Panel", GameplayHUDUI.PausePanelPrefabAssetPath);

        private static void Bake(string title, string path, string prefabName, Func<Transform, (GameObject panel, bool ok, string missing)> build)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Bake " + title, "Exit Play mode first.", "OK");
                return;
            }

            if (File.Exists(path) && !EditorUtility.DisplayDialog("Bake " + title,
                    "A baked " + title.ToLowerInvariant() + " already exists. Baking again rebuilds it from the code layout and replaces it, so any edits made to the prefab are lost.\n\nBake again?",
                    "Bake again", "Cancel"))
            {
                return;
            }

            // A scratch canvas, like the one the menu and HUD put the panel on
            var canvasObject = new GameObject("BakeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            GameObject builtPanel = null;
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                var (panel, ok, missing) = build(canvasObject.transform);
                builtPanel = panel;
                if (!ok)
                {
                    Debug.LogError("[UI] " + title + " not baked: the layout is missing " + missing);
                    return;
                }

                int swapped = ReplaceRuntimeSprites(panel);

                EnsureFolder(PrefabFolder, "Prefabs");
                panel.name = prefabName;
                panel.transform.SetParent(null, false);
                PrefabUtility.SaveAsPrefabAsset(panel, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Object asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                Debug.Log("[UI] Baked the " + title.ToLowerInvariant() + " to " + path + (swapped > 0 ? " (" + swapped + " runtime sprite(s) swapped for sprite assets)" : string.Empty) +
                          ". Open it to edit the layout; the game uses it from now on.");
            }
            finally
            {
                // The scratch objects must not outlive the bake. The panel was unparented to save it, so destroying the canvas
                // would leave it in the open scene, still registered in UGUI's static list of enabled Selectables; with
                // domain reload off that list then gets out of step and every later button throws IndexOutOfRangeException
                // in Selectable.OnEnable.
                if (builtPanel != null) Object.DestroyImmediate(builtPanel);
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Remove(string title, string path)
        {
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Remove Baked " + title, "There is no baked " + title.ToLowerInvariant() + ".", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Baked " + title,
                    "Delete the baked prefab? The game goes back to building the " + title.ToLowerInvariant() + " in code.", "Delete", "Cancel"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(path);
            Debug.Log("[UI] Removed " + path + ": the " + title.ToLowerInvariant() + " is built in code again.");
        }

        /// <summary>
        /// A prefab can only hold sprites that are assets. Swaps every Image sprite created at runtime (Sprite.Create) for the
        /// sprite asset with the same rect on the same texture, and clears it (with a warning) when there is none.
        /// </summary>
        private static int ReplaceRuntimeSprites(GameObject root)
        {
            int swapped = 0;
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                Sprite sprite = image.sprite;
                if (sprite == null || AssetDatabase.Contains(sprite)) continue;

                Sprite match = null;
                string texturePath = sprite.texture != null ? AssetDatabase.GetAssetPath(sprite.texture) : null;
                if (!string.IsNullOrEmpty(texturePath))
                {
                    foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(texturePath))
                    {
                        if (candidate is Sprite asset && asset.rect == sprite.rect)
                        {
                            match = asset;
                            break;
                        }
                    }
                }

                if (match == null) Debug.LogWarning("[UI] No sprite asset matches the runtime sprite on '" + image.name + "'; it is left empty in the prefab.");
                image.sprite = match;
                swapped++;
            }
            return swapped;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
