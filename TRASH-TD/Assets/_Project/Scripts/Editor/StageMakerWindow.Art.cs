using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    // Adding a stage's map art and bounds art. Art that does not fit the grid is refused (StageArtRules), either when a sprite
    // is picked or dropped on a slot, or before a PNG file is copied into the project by "Import PNG".
    public sealed partial class StageMakerWindow
    {
        private const string AllowLargerArtPrefKey = "TrashTD.StageMaker.AllowLargerArt";
        private const string ArtFolderRoot = "Assets/_Project/Art/Levels";

        private bool allowLargerArt;
        private bool artFoldout = true;

        private void DrawArtPanel()
        {
            artFoldout = EditorGUILayout.Foldout(artFoldout, "ART", true, EditorStyles.foldoutHeader);
            if (!artFoldout) return;

            if (!StageMakerLogic.HasGrid(stage))
            {
                EditorGUILayout.HelpBox("Create the grid first: the art has to fit it.", MessageType.Info);
                return;
            }

            int tile = StageArtRules.TilePixels(stage);
            Vector2Int exact = StageArtRules.ExactMapPixels(stage);
            EditorGUILayout.LabelField(
                "A " + stage.gridWidth + "x" + stage.gridHeight + " grid of " + tile + " px tiles needs stage art of exactly " + exact.x + "x" + exact.y + " px. Art that does not fit is refused.",
                hintStyle);

            EditorGUI.BeginChangeCheck();
            allowLargerArt = EditorGUILayout.ToggleLeft(
                new GUIContent("Allow art larger than the grid",
                    "For maps whose artwork has extra rows or columns around the grid (placed with the offset below). The art must still cover every grid tile."),
                allowLargerArt);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(AllowLargerArtPrefKey, allowLargerArt);

            GUILayout.Space(4f);
            DrawMapArtSection(tile);
            GUILayout.Space(8f);
            DrawBoundsArtSection(tile);
        }

        private static Vector2Int SpritePixels(Sprite sprite)
        {
            Rect rect = sprite.rect;
            return new Vector2Int(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
        }

        // ============================
        // Stage (map) art
        // ============================

        private void DrawMapArtSection(int tile)
        {
            EditorGUI.BeginChangeCheck();
            var picked = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Stage art", "The map artwork drawn over the grid (StageData.mapVisualSprite)."), stage.mapVisualSprite, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck()) AssignMapArt(picked);

            if (stage.mapVisualSprite != null)
            {
                Vector2Int size = SpritePixels(stage.mapVisualSprite);
                ArtCheck check = StageArtRules.CheckMapArt(size.x, size.y, tile, stage.gridWidth, stage.gridHeight, stage.visualTileOffset, true);
                EditorGUILayout.HelpBox(size.x + "x" + size.y + " px. " + check.message, check.ok ? MessageType.Info : MessageType.Warning);

                if (size.x != stage.gridWidth * tile || size.y != stage.gridHeight * tile)
                    DrawOffsetFields("Offset (tiles)", "Where the art's bottom-left corner sits relative to the grid's bottom-left tile.",
                        stage.visualTileOffset, newOffset => stage.visualTileOffset = newOffset, null);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Import PNG...", "Pick a PNG file. It is size-checked first and only copied into the project if it fits."))) ImportArt(false);
            using (new EditorGUI.DisabledScope(stage.mapVisualSprite == null))
            {
                if (GUILayout.Button("Show in project", GUILayout.Width(110f))) EditorGUIUtility.PingObject(stage.mapVisualSprite);
                if (GUILayout.Button("Remove", GUILayout.Width(64f))) AssignMapArt(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void AssignMapArt(Sprite picked)
        {
            if (picked == stage.mapVisualSprite) return;

            if (picked == null)
            {
                RecordForEdit("Remove stage art");
                stage.mapVisualSprite = null;
                FinishEdit();
                return;
            }

            int tile = StageArtRules.TilePixels(stage);
            Vector2Int size = SpritePixels(picked);
            Vector2 offset = stage.visualTileOffset;
            if (size.x == stage.gridWidth * tile && size.y == stage.gridHeight * tile) offset = Vector2.zero; // an exact fit sits on the grid

            ArtCheck check = StageArtRules.CheckMapArt(size.x, size.y, tile, stage.gridWidth, stage.gridHeight, offset, allowLargerArt);
            if (!check.ok)
            {
                EditorUtility.DisplayDialog("Stage art not accepted", picked.name + " (" + size.x + "x" + size.y + " px)\n\n" + check.message, "OK");
                GUIUtility.ExitGUI();
                return;
            }

            RecordForEdit("Set stage art");
            stage.mapVisualSprite = picked;
            stage.visualTileOffset = offset;
            FinishEdit();
            SetStatus("Stage art set. " + check.message, MessageType.Info);
        }

        // ============================
        // Bounds art
        // ============================

        private void DrawBoundsArtSection(int tile)
        {
            EditorGUI.BeginChangeCheck();
            var picked = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Bounds art", "The scenery around the map, outside the playable grid (StageData.boundsVisualSprite)."), stage.boundsVisualSprite, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck()) AssignBoundsArt(picked);

            if (stage.boundsVisualSprite != null)
            {
                Vector2Int size = SpritePixels(stage.boundsVisualSprite);
                ArtCheck check = StageArtRules.CheckBoundsArt(size.x, size.y, tile, stage.gridWidth, stage.gridHeight, stage.boundsTileOffset);
                EditorGUILayout.HelpBox(size.x + "x" + size.y + " px. " + check.message, check.ok ? MessageType.Info : MessageType.Warning);

                DrawOffsetFields("Offset (tiles)", "Where the bounds art's bottom-left corner sits relative to the grid's bottom-left tile (negative: scenery to the left of and below the map).",
                    stage.boundsTileOffset, newOffset => stage.boundsTileOffset = newOffset,
                    () => StageArtRules.CentredBoundsOffset(size.x, size.y, tile, stage.gridWidth, stage.gridHeight));
            }
            else
            {
                EditorGUILayout.LabelField("Optional. It must be a whole number of tiles and at least as big as the grid; new bounds art is centred on the map.", hintStyle);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Import PNG...", "Pick a PNG file. It is size-checked first and only copied into the project if it fits."))) ImportArt(true);
            using (new EditorGUI.DisabledScope(stage.boundsVisualSprite == null))
            {
                if (GUILayout.Button("Show in project", GUILayout.Width(110f))) EditorGUIUtility.PingObject(stage.boundsVisualSprite);
                if (GUILayout.Button("Remove", GUILayout.Width(64f))) AssignBoundsArt(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void AssignBoundsArt(Sprite picked)
        {
            if (picked == stage.boundsVisualSprite) return;

            if (picked == null)
            {
                RecordForEdit("Remove bounds art");
                stage.boundsVisualSprite = null;
                FinishEdit();
                return;
            }

            int tile = StageArtRules.TilePixels(stage);
            Vector2Int size = SpritePixels(picked);
            Vector2 centred = StageArtRules.CentredBoundsOffset(size.x, size.y, tile, stage.gridWidth, stage.gridHeight);
            ArtCheck check = StageArtRules.CheckBoundsArt(size.x, size.y, tile, stage.gridWidth, stage.gridHeight, centred);
            if (!check.ok)
            {
                EditorUtility.DisplayDialog("Bounds art not accepted", picked.name + " (" + size.x + "x" + size.y + " px)\n\n" + check.message, "OK");
                GUIUtility.ExitGUI();
                return;
            }

            RecordForEdit("Set bounds art");
            stage.boundsVisualSprite = picked;
            stage.boundsTileOffset = centred;
            FinishEdit();
            SetStatus("Bounds art set and centred on the map. " + check.message, MessageType.Info);
        }

        // ============================
        // Offset fields (whole tiles)
        // ============================

        private void DrawOffsetFields(string label, string tooltip, Vector2 offset, System.Action<Vector2> apply, System.Func<Vector2> centre)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(92f));
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("X", GUILayout.Width(12f));
            int x = EditorGUILayout.IntField(Mathf.RoundToInt(offset.x), GUILayout.Width(46f));
            GUILayout.Label("Y", GUILayout.Width(12f));
            int y = EditorGUILayout.IntField(Mathf.RoundToInt(offset.y), GUILayout.Width(46f));
            bool changed = EditorGUI.EndChangeCheck();
            if (centre != null && GUILayout.Button(new GUIContent("Centre", "Put the grid in the middle of the art."), GUILayout.Width(60f)))
            {
                RecordForEdit("Centre art");
                apply(centre());
                FinishEdit();
            }
            else if (changed)
            {
                RecordForEdit("Art offset");
                apply(new Vector2(x, y));
                FinishEdit();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ============================
        // Importing a PNG
        // ============================

        /// <summary>Reads a PNG's pixel size from its header, without loading the image. False when the file is not a PNG.</summary>
        public static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    var header = new byte[24];
                    if (stream.Read(header, 0, 24) < 24) return false;
                    byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                    for (int i = 0; i < signature.Length; i++)
                        if (header[i] != signature[i]) return false;

                    width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    return width > 0 && height > 0;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>The folder a stage's art is imported into: Art/Levels/Level N for STAGE_0N, otherwise Art/Levels/Other.</summary>
        private string ArtFolderFor(StageData s)
        {
            Match number = Regex.Match(s.stageId ?? string.Empty, @"(\d+)\s*$");
            string name = number.Success ? "Level " + int.Parse(number.Value) : "Other";
            return ArtFolderRoot + "/" + name;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private void ImportArt(bool bounds)
        {
            string what = bounds ? "bounds art" : "stage art";
            string file = EditorUtility.OpenFilePanel("Import " + what + " (PNG)", string.Empty, "png");
            if (string.IsNullOrEmpty(file)) return;

            if (!TryReadPngSize(file, out int width, out int height))
            {
                EditorUtility.DisplayDialog("Not imported", Path.GetFileName(file) + " is not a PNG image. Stage art has to be a PNG file.", "OK");
                GUIUtility.ExitGUI();
                return;
            }

            // Check the size before anything is copied into the project.
            int tile = StageArtRules.TilePixels(stage);
            ArtCheck check;
            if (bounds)
            {
                Vector2 centred = StageArtRules.CentredBoundsOffset(width, height, tile, stage.gridWidth, stage.gridHeight);
                check = StageArtRules.CheckBoundsArt(width, height, tile, stage.gridWidth, stage.gridHeight, centred);
            }
            else
            {
                bool exact = width == stage.gridWidth * tile && height == stage.gridHeight * tile;
                check = StageArtRules.CheckMapArt(width, height, tile, stage.gridWidth, stage.gridHeight, exact ? Vector2.zero : stage.visualTileOffset, allowLargerArt);
            }
            if (!check.ok)
            {
                EditorUtility.DisplayDialog("Not imported", Path.GetFileName(file) + " (" + width + "x" + height + " px)\n\n" + check.message, "OK");
                GUIUtility.ExitGUI();
                return;
            }

            string assetPath;
            string projectAssets = Application.dataPath.Replace('\\', '/');
            string normalized = file.Replace('\\', '/');
            if (normalized.StartsWith(projectAssets + "/"))
            {
                assetPath = "Assets" + normalized.Substring(projectAssets.Length); // already inside the project: use it where it is
            }
            else
            {
                string folder = ArtFolderFor(stage);
                EnsureAssetFolder(folder);
                assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(file));
                File.Copy(file, Path.GetFullPath(assetPath));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = tile;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("Imported, but not assigned", assetPath + " was added to the project but has no single sprite to assign. Pick the sprite in the Stage art field.", "OK");
                GUIUtility.ExitGUI();
                return;
            }

            if (bounds) AssignBoundsArt(sprite);
            else AssignMapArt(sprite);
            SetStatus("Imported " + assetPath + ". " + check.message, MessageType.Info);
            GUIUtility.ExitGUI();
        }
    }
}
