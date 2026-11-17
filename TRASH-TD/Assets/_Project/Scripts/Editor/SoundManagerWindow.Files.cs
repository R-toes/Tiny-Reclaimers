using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TrashTD.Audio;

namespace TrashTD.Editor
{
    // Files: listing a folder's audio, adding, replacing, renaming and deleting files, and the preview player.
    public sealed partial class SoundManagerWindow
    {
        private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg", ".aif", ".aiff" };

        private sealed class AudioFile
        {
            public string AssetPath;        // Assets/_Project/Audio/Resources/SFX/Operators/Guard/Qihou/Attack_2.wav
            public string ResourcesFolder;  // SFX/Operators/Guard/Qihou
            public string Name;             // Attack_2
            public string Extension;        // .wav
            public AudioClip Clip;
            public string FileName => Name + Extension;
        }

        private readonly Dictionary<string, List<AudioFile>> folderCache = new Dictionary<string, List<AudioFile>>();

        /// <summary>The audio files directly in a Resources folder (not its subfolders), by name.</summary>
        private List<AudioFile> GetFiles(string resourcesFolder)
        {
            if (folderCache.TryGetValue(resourcesFolder, out List<AudioFile> cached)) return cached;

            var files = new List<AudioFile>();
            string assetFolder = FindFolder(resourcesFolder);
            string fullFolder = assetFolder != null ? ToFullPath(assetFolder) : null;
            if (fullFolder != null && Directory.Exists(fullFolder))
            {
                foreach (string fullPath in Directory.GetFiles(fullFolder))
                {
                    if (!IsAudioFile(fullPath)) continue;
                    string assetPath = assetFolder + "/" + Path.GetFileName(fullPath);
                    files.Add(new AudioFile
                    {
                        AssetPath = assetPath,
                        ResourcesFolder = resourcesFolder,
                        Name = Path.GetFileNameWithoutExtension(fullPath),
                        Extension = Path.GetExtension(fullPath).ToLowerInvariant(),
                        Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath)
                    });
                }
            }
            files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            folderCache[resourcesFolder] = files;
            return files;
        }

        private List<AudioFile> GetSlotFiles(SoundSlot slot)
        {
            var files = new List<AudioFile>();
            foreach (AudioFile file in GetFiles(slot.Folder))
                if (slot.Accepts(file.Name)) files.Add(file);
            return files;
        }

        private IEnumerable<string> GetFileNames(string resourcesFolder)
        {
            foreach (AudioFile file in GetFiles(resourcesFolder)) yield return file.Name;
        }

        private SoundStatus GetStatus(SoundSlot slot, out string usedFallback) =>
            SoundCatalog.GetStatus(slot, GetFileNames, out usedFallback);

        // ─────────────────────────────────────────────────────
        //  Changing files
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Copies audio files into the slot's folder: the first one gets the slot's name, the rest become variations
        /// (Name_2, Name_3 ...). A music slot takes one file and replaces the track it has.
        /// </summary>
        private void AddFiles(SoundSlot slot, IEnumerable<string> sourcePaths)
        {
            var added = new List<string>();
            foreach (string rawPath in sourcePaths)
            {
                string source = NormalizeSource(rawPath);
                if (!IsAudioFile(source) || !File.Exists(ToFullPath(source))) continue;

                if (slot.IsMusic)
                {
                    List<AudioFile> existing = GetSlotFiles(slot);
                    if (existing.Count > 0)
                    {
                        if (!EditorUtility.DisplayDialog("Replace music", $"Replace {existing[0].FileName} with {Path.GetFileName(source)}?",
                                "Replace", "Cancel")) return;
                        ReplaceFile(existing[0], source);
                        return;
                    }
                    CopyInto(source, slot.Folder, slot.Name + Path.GetExtension(source).ToLowerInvariant());
                    added.Add(slot.Name);
                    break;
                }

                string name = NextFreeName(slot);
                CopyInto(source, slot.Folder, name + Path.GetExtension(source).ToLowerInvariant());
                added.Add(name);
            }

            AfterFilesChanged();
            if (added.Count > 0) ShowNotification(new GUIContent("Added " + string.Join(", ", added)));
        }

        /// <summary>The slot's name if it has no file yet, else the first free variation name (Name_2, Name_3 ...).</summary>
        private string NextFreeName(SoundSlot slot)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AudioFile file in GetFiles(slot.Folder)) taken.Add(file.Name);

            if (!taken.Contains(slot.Name) && GetSlotFiles(slot).Count == 0) return slot.Name;
            for (int i = 2; ; i++)
            {
                string candidate = slot.Name + "_" + i;
                if (!taken.Contains(candidate)) return candidate;
            }
        }

        private void CopyInto(string source, string resourcesFolder, string fileName)
        {
            string folder = EnsureFolder(resourcesFolder);
            string target = folder + "/" + fileName;
            File.Copy(ToFullPath(source), ToFullPath(target), false);
            AssetDatabase.ImportAsset(target);
            folderCache.Remove(resourcesFolder);
        }

        /// <summary>
        /// Swaps a file's audio for another file. Same format: the file is overwritten in place, so it keeps its asset id
        /// and volume row. Another format: the old file goes to the trash and the new one takes its name and volume.
        /// </summary>
        private void ReplaceFile(AudioFile file, string rawSource)
        {
            string source = NormalizeSource(rawSource);
            if (!IsAudioFile(source) || !File.Exists(ToFullPath(source))) return;
            if (string.Equals(ToFullPath(source), ToFullPath(file.AssetPath), StringComparison.OrdinalIgnoreCase)) return;

            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (extension == file.Extension)
            {
                File.Copy(ToFullPath(source), ToFullPath(file.AssetPath), true);
                AssetDatabase.ImportAsset(file.AssetPath, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                string target = Path.GetDirectoryName(file.AssetPath).Replace('\\', '/') + "/" + file.Name + extension;
                if (File.Exists(ToFullPath(target)))
                {
                    EditorUtility.DisplayDialog("Replace", $"{target} already exists.", "OK");
                    return;
                }

                float level = settings != null && file.Clip != null ? settings.GetVolume(file.Clip) : 1f;
                if (settings != null)
                {
                    Undo.RecordObject(settings, "Replace sound file");
                    settings.RemoveClip(file.Clip);
                }
                if (!AssetDatabase.MoveAssetToTrash(file.AssetPath)) AssetDatabase.DeleteAsset(file.AssetPath);

                File.Copy(ToFullPath(source), ToFullPath(target), false);
                AssetDatabase.ImportAsset(target);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
                if (settings != null && clip != null)
                {
                    settings.SetVolume(clip, level);
                    EditorUtility.SetDirty(settings);
                }
            }

            AfterFilesChanged();
            ShowNotification(new GUIContent("Replaced " + file.Name));
        }

        /// <summary>Renames a file to another slot's name in the same folder (a variation if that slot has files already).</summary>
        private void RenameFile(AudioFile file, SoundSlot target)
        {
            string name = NextFreeName(target);
            string folder = EnsureFolder(target.Folder);
            string newPath = folder + "/" + name + file.Extension;
            string error = AssetDatabase.MoveAsset(file.AssetPath, newPath);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename failed", error, "OK");
                return;
            }

            AfterFilesChanged();
            ShowNotification(new GUIContent($"{file.Name} is now {name}"));
        }

        private void DeleteFile(AudioFile file)
        {
            if (!EditorUtility.DisplayDialog("Delete sound file",
                    $"Move {file.AssetPath} to the trash?\n\nYou can get it back from the trash (Recycle Bin), or from git.",
                    "Delete", "Cancel")) return;

            if (previewSource != null && previewSource.clip == file.Clip) StopPreview();
            if (settings != null && file.Clip != null)
            {
                Undo.RecordObject(settings, "Delete sound file");
                settings.RemoveClip(file.Clip);
                EditorUtility.SetDirty(settings);
            }
            if (!AssetDatabase.MoveAssetToTrash(file.AssetPath)) AssetDatabase.DeleteAsset(file.AssetPath);

            AfterFilesChanged();
            ShowNotification(new GUIContent("Deleted " + file.FileName));
        }

        /// <summary>
        /// Creates the Resources folder (and its parents) if needed; returns its Assets path. A new folder gets a .keep
        /// file like the others, so git keeps it (and its .meta) even after its last sound is deleted.
        /// </summary>
        private static string EnsureFolder(string resourcesFolder)
        {
            string current = ResourcesRoot;
            foreach (string part in resourcesFolder.Split('/'))
            {
                string existing = FindChildFolder(current, part);
                if (existing == null)
                {
                    AssetDatabase.CreateFolder(current, part);
                    File.WriteAllText(ToFullPath(current + "/" + part + "/.keep"), string.Empty);
                    existing = part;
                }
                current = current + "/" + existing;
            }
            return current;
        }

        /// <summary>
        /// The Assets path of a Resources folder as it is on disk, or null if it doesn't exist. Folder names are
        /// matched ignoring case, as Resources loading does: Dead Eye's sounds are found as SFX/.../DeadEye but the
        /// folder on disk is Deadeye.
        /// </summary>
        private static string FindFolder(string resourcesFolder)
        {
            string current = ResourcesRoot;
            foreach (string part in resourcesFolder.Split('/'))
            {
                string existing = FindChildFolder(current, part);
                if (existing == null) return null;
                current = current + "/" + existing;
            }
            return current;
        }

        private static string FindChildFolder(string assetFolder, string name)
        {
            string full = ToFullPath(assetFolder);
            if (!Directory.Exists(full)) return null;
            foreach (string directory in Directory.GetDirectories(full))
            {
                string directoryName = Path.GetFileName(directory);
                if (string.Equals(directoryName, name, StringComparison.OrdinalIgnoreCase)) return directoryName;
            }
            return null;
        }

        /// <summary>An Assets/ path stays as it is; a full path inside the project becomes an Assets/ path.</summary>
        private static string NormalizeSource(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/');
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
            if (path.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) path = path.Substring(projectRoot.Length);
            return path;
        }

        private void AfterFilesChanged()
        {
            folderCache.Clear();
            UnitSfx.ClearCache(); // in Play mode the game finds the new files on their next play
            SaveSettings();
            Repaint();
        }

        private void SaveSettings()
        {
            if (settings != null && EditorUtility.IsDirty(settings)) AssetDatabase.SaveAssetIfDirty(settings);
        }

        // ─────────────────────────────────────────────────────
        //  Preview
        // ─────────────────────────────────────────────────────

        private AudioSource previewSource;

        private void PlayPreview(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (previewSource == null)
            {
                GameObject player = EditorUtility.CreateGameObjectWithHideFlags("SoundManagerPreview", HideFlags.HideAndDontSave,
                    typeof(AudioSource));
                previewSource = player.GetComponent<AudioSource>();
                previewSource.playOnAwake = false;
                previewSource.spatialBlend = 0f;
                previewSource.loop = false;
            }

            previewSource.Stop();
            previewSource.clip = clip;
            previewSource.volume = Mathf.Clamp01(volume);
            previewSource.Play();
        }

        private void StopPreview()
        {
            if (previewSource != null) previewSource.Stop();
        }

        private void DestroyPreviewSource()
        {
            if (previewSource != null) DestroyImmediate(previewSource.gameObject);
            previewSource = null;
        }
    }
}
