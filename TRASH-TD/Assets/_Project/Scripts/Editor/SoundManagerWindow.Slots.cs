using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TrashTD.Audio;

namespace TrashTD.Editor
{
    // Drawing: the overview, a group's volume sliders, each slot with its files, and the files no slot uses.
    public sealed partial class SoundManagerWindow
    {
        private const string OwnHex = "7fd18b";
        private const string StandInHex = "e0b84f";
        private const string SilentHex = "e07070";
        private const string OptionalHex = "9a9a9a";

        private GUIStyle wrapStyle;
        private GUIStyle richStyle;
        private GUIStyle titleStyle;

        private void EnsureStyles()
        {
            if (wrapStyle != null) return;
            wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
            richStyle = new GUIStyle(EditorStyles.label) { richText = true };
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
        }

        // ─────────────────────────────────────────────────────
        //  Overview
        // ─────────────────────────────────────────────────────

        private void DrawOverview()
        {
            EnsureStyles();
            GUILayout.Label("Sound Manager", titleStyle);
            EditorGUILayout.HelpBox(
                "Every sound the game can play. Pick a group on the left, or search. Drop audio files onto a sound to add " +
                "them: they are copied into the right folder with the right name (more than one becomes variations, " +
                "picked at random).\n\n" +
                "A sound's volume = its category x its group (stage, operator, enemy) x its own file level x the " +
                "player's SFX volume. Levels only go down: to make a file louder than recorded, raise it in an audio editor. " +
                "Music plays at the player's Music volume and has no levels here.",
                MessageType.None);

            EditorGUILayout.Space(6f);
            GUILayout.Label("Category volumes", EditorStyles.boldLabel);
            if (settings != null)
                foreach ((string key, string label) in SoundVolumeGroups.Categories)
                    DrawLevelSlider(label, key, "Every sound in this category.");

            EditorGUILayout.Space(10f);
            GUILayout.Label("What plays today", EditorStyles.boldLabel);
            DrawSectionCounts("Shared (UI, Gameplay, Combat, Tiles)", SoundSection.Shared);
            DrawSectionCounts("Stages", SoundSection.Stage);
            DrawSectionCounts("Music", SoundSection.Music);
            DrawSectionCounts("Operators", SoundSection.Operator);
            DrawSectionCounts("Enemies", SoundSection.Enemy);
            EditorGUILayout.LabelField(
                "Silent: nothing plays.  Stand-in: another sound plays until this one has a file.  " +
                "Optional: nothing extra plays, and nothing is missed.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(10f);
            if (settings != null && GUILayout.Button("Remove volume rows of files that no longer exist", GUILayout.Width(330f)))
            {
                Undo.RecordObject(settings, "Remove missing sound rows");
                int removed = settings.RemoveMissingClips();
                EditorUtility.SetDirty(settings);
                ShowNotification(new GUIContent(removed + " row(s) removed"));
            }
        }

        private void DrawSectionCounts(string label, SoundSection section)
        {
            int own = 0, standIn = 0, silent = 0, optional = 0;
            foreach (SoundGroup group in groups)
            {
                if (group.Section != section) continue;
                foreach (SoundSlot slot in group.Slots)
                {
                    switch (GetStatus(slot, out _))
                    {
                        case SoundStatus.OwnFile: own++; break;
                        case SoundStatus.StandIn: standIn++; break;
                        case SoundStatus.Silent: silent++; break;
                        default: optional++; break;
                    }
                }
            }
            GUILayout.Label(
                $"{label}:  <color=#{OwnHex}>{own} own file</color>   <color=#{StandInHex}>{standIn} stand-in</color>   " +
                $"<color=#{SilentHex}>{silent} silent</color>   <color=#{OptionalHex}>{optional} optional</color>",
                richStyle);
        }

        // ─────────────────────────────────────────────────────
        //  A group
        // ─────────────────────────────────────────────────────

        private void DrawGroup(SoundGroup group)
        {
            EnsureStyles();
            GUILayout.Label(group.Title, titleStyle);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{SectionLabel(group)}{ToAssetPath(group.Folder)}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Show folder", EditorStyles.miniButton, GUILayout.Width(90f))) PingFolder(group.Folder);
            EditorGUILayout.EndHorizontal();

            DrawGroupVolumes(group);

            foreach (SoundSlot slot in group.Slots) DrawSlot(group, slot);
            DrawUnusedFiles(group);
        }

        private void DrawGroupVolumes(SoundGroup group)
        {
            if (group.Section == SoundSection.Music)
            {
                EditorGUILayout.HelpBox("Music plays at the player's Music volume (Settings). Tracks loop: make them seamless. " +
                                        "One file per track, found by its exact name (no variations).", MessageType.None);
                return;
            }
            if (settings == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Volume", EditorStyles.boldLabel);

            var categories = new List<string>();
            foreach (SoundSlot slot in group.Slots)
            {
                SoundVolumeGroups.Resolve(slot.Folder, slot.Name, out string category, out _);
                if (category != null && !categories.Contains(category)) categories.Add(category);
            }
            foreach (string category in categories)
                DrawLevelSlider("Category: " + CategoryLabel(category), category,
                    "Shared by every sound in this category, not just this group.");

            if (group.GroupKey != null)
                DrawLevelSlider(GroupKindLabel(group) + ": " + group.Title, group.GroupKey, "Every sound of " + group.Title + ".");
            if (group.BounceKey != null)
                DrawLevelSlider("Bounce falloff", group.BounceKey,
                    "How loud each bounce is next to the one before (0.5 = half as loud). Never below 5% of the first.");
            EditorGUILayout.EndVertical();
        }

        private static string CategoryLabel(string key)
        {
            foreach ((string categoryKey, string label) in SoundVolumeGroups.Categories)
                if (categoryKey == key) return label;
            return key;
        }

        private static string GroupKindLabel(SoundGroup group)
        {
            return group.Section switch
            {
                SoundSection.Operator => "This operator",
                SoundSection.Enemy => "This enemy",
                SoundSection.Stage => "This stage",
                _ => "This group"
            };
        }

        private void DrawLevelSlider(string label, string key, string tooltip)
        {
            float level = settings.GetGroupLevel(key);
            float defaultLevel = SoundVolumeGroups.GetDefault(key);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            float changed = EditorGUILayout.Slider(new GUIContent(label, tooltip + $" Default {defaultLevel:P0}."), level, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Sound volume");
                settings.SetGroupLevel(key, changed);
                EditorUtility.SetDirty(settings);
            }
            using (new EditorGUI.DisabledScope(Mathf.Approximately(level, defaultLevel)))
            {
                if (GUILayout.Button(new GUIContent("Reset", $"Back to {defaultLevel:P0}"), EditorStyles.miniButton, GUILayout.Width(46f)))
                {
                    Undo.RecordObject(settings, "Sound volume");
                    settings.SetGroupLevel(key, defaultLevel);
                    EditorUtility.SetDirty(settings);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────────────
        //  A slot
        // ─────────────────────────────────────────────────────

        private void DrawSlot(SoundGroup group, SoundSlot slot)
        {
            EnsureStyles();
            SoundStatus status = GetStatus(slot, out string usedFallback);
            List<AudioFile> files = GetSlotFiles(slot);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(StatusTag(status), richStyle, GUILayout.Width(78f));
            GUILayout.Label($"<b>{slot.Name}</b>" + (slot.IsLoop ? "  <color=#9ab>(loop)</color>" : string.Empty), richStyle);
            GUILayout.FlexibleSpace();
            string addLabel = slot.IsMusic && files.Count > 0 ? "Replace..." : "Add file...";
            if (GUILayout.Button(addLabel, EditorStyles.miniButton, GUILayout.Width(80f)))
            {
                string picked = EditorUtility.OpenFilePanel("Add a sound for " + slot.Name, string.Empty, "wav,mp3,ogg,aif,aiff");
                if (!string.IsNullOrEmpty(picked)) AddFiles(slot, new[] { picked });
                GUIUtility.ExitGUI(); // a file panel or dialog was shown: end this GUI pass cleanly
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label(slot.WhenItPlays, wrapStyle);
            if (slot.Aliases.Length > 0)
                GUILayout.Label("Also accepted (older names): " + string.Join(", ", slot.Aliases), EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(DescribeFallback(slot, status, usedFallback), new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true });
            GUILayout.Label($"File: {ToAssetPath(slot.Folder)}/{slot.Name}" + (slot.AllowsVariations ? "  (+ _2, _3 ... variations)" : string.Empty),
                EditorStyles.miniLabel);

            foreach (AudioFile file in files) DrawFileRow(group, slot, file);

            DrawDropArea(slot);
            EditorGUILayout.EndVertical();
        }

        private static string StatusTag(SoundStatus status)
        {
            return status switch
            {
                SoundStatus.OwnFile => $"<color=#{OwnHex}><b>OWN FILE</b></color>",
                SoundStatus.StandIn => $"<color=#{StandInHex}><b>STAND-IN</b></color>",
                SoundStatus.Silent => $"<color=#{SilentHex}><b>SILENT</b></color>",
                _ => $"<color=#{OptionalHex}><b>OPTIONAL</b></color>"
            };
        }

        private static string DescribeFallback(SoundSlot slot, SoundStatus status, string usedFallback)
        {
            var chain = new List<string>();
            foreach (string path in slot.Fallbacks) chain.Add(path.StartsWith("SFX/") ? path.Substring(4) : path);
            if (!string.IsNullOrEmpty(slot.FallbackNote)) chain.Add(slot.FallbackNote);
            string without = chain.Count > 0 ? "Without its own file: " + string.Join(", else ", chain) + ", else nothing." : "Without its own file: nothing plays.";
            if (slot.Optional && chain.Count == 0) without = "Without its own file: nothing extra plays (that's fine).";

            switch (status)
            {
                case SoundStatus.StandIn:
                    string used = usedFallback != null && usedFallback.StartsWith("SFX/") ? usedFallback.Substring(4) : usedFallback;
                    return $"<color=#{StandInHex}>Now playing instead: {used}.</color>  {without}";
                case SoundStatus.Silent:
                    return $"<color=#{SilentHex}>Nothing plays now.</color>  {without}";
                default:
                    return without;
            }
        }

        private void DrawFileRow(SoundGroup group, SoundSlot slot, AudioFile file)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10f);

            float effective = GetEffectiveLevel(file);
            if (GUILayout.Button(new GUIContent("Play", "Play it at its in-game level (before the player's SFX volume)"),
                    EditorStyles.miniButtonLeft, GUILayout.Width(38f)))
                PlayPreview(file.Clip, slot.IsMusic ? 1f : effective);
            if (GUILayout.Button("Stop", EditorStyles.miniButtonRight, GUILayout.Width(38f))) StopPreview();

            string length = file.Clip != null ? $"{file.Clip.length:0.00} s" : "not imported";
            GUILayout.Label(new GUIContent(file.FileName, file.AssetPath), GUILayout.MinWidth(120f));
            GUILayout.Label(length, EditorStyles.miniLabel, GUILayout.Width(70f));

            if (!slot.IsMusic && settings != null && file.Clip != null)
            {
                float level = settings.GetVolume(file.Clip);
                EditorGUI.BeginChangeCheck();
                float changed = GUILayout.HorizontalSlider(level, 0f, 1f, GUILayout.Width(110f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings, "Sound file volume");
                    settings.SetVolume(file.Clip, changed);
                    EditorUtility.SetDirty(settings);
                }
                GUILayout.Label(new GUIContent($"{level:P0}", "This file's own level"), EditorStyles.miniLabel, GUILayout.Width(34f));
                GUILayout.Label(new GUIContent($"= {effective:P0}", EffectiveTooltip(file)), EditorStyles.miniBoldLabel, GUILayout.Width(44f));
            }

            if (GUILayout.Button(new GUIContent("Replace...", "Swap the audio for another file, keeping its name and level"),
                    EditorStyles.miniButtonLeft, GUILayout.Width(66f)))
            {
                string picked = EditorUtility.OpenFilePanel("Replace " + file.FileName, string.Empty, "wav,mp3,ogg,aif,aiff");
                if (!string.IsNullOrEmpty(picked)) ReplaceFile(file, picked);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(new GUIContent("Rename", "Give it another sound's name in this group"),
                    EditorStyles.miniButtonMid, GUILayout.Width(56f)))
                ShowRenameMenu(group, file);
            if (GUILayout.Button(new GUIContent("Delete", "Move the file to the trash"), EditorStyles.miniButtonRight, GUILayout.Width(50f)))
            {
                DeleteFile(file);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        private float GetEffectiveLevel(AudioFile file)
        {
            if (settings == null || file.Clip == null) return 1f;
            return settings.GetGroupVolume(file.ResourcesFolder, file.Name) * settings.GetVolume(file.Clip);
        }

        private string EffectiveTooltip(AudioFile file)
        {
            SoundVolumeGroups.Resolve(file.ResourcesFolder, file.Name, out string category, out string group);
            string text = $"In-game level before the player's SFX volume:\n{CategoryLabel(category)} {settings.GetGroupLevel(category):P0}";
            if (group != null) text += $" x {group} {settings.GetGroupLevel(group):P0}";
            return text + $" x this file {settings.GetVolume(file.Clip):P0}";
        }

        private void ShowRenameMenu(SoundGroup group, AudioFile file)
        {
            var menu = new GenericMenu();
            foreach (SoundSlot target in group.Slots)
            {
                if (target.Folder != file.ResourcesFolder) continue;
                bool current = target.Accepts(file.Name);
                SoundSlot captured = target;
                if (current) menu.AddDisabledItem(new GUIContent(target.Name + " (now)"));
                else menu.AddItem(new GUIContent(target.Name), false, () => RenameFile(file, captured));
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No other sound in this folder"));
            menu.ShowAsContext();
        }

        private void DrawDropArea(SoundSlot slot)
        {
            Rect area = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            string hint = slot.IsMusic
                ? "Drop an audio file here to set this track"
                : "Drop audio files here to add them" + (GetSlotFiles(slot).Count > 0 ? " as variations" : string.Empty);
            GUI.Box(area, hint, EditorStyles.centeredGreyMiniLabel);

            Event current = Event.current;
            if (!area.Contains(current.mousePosition)) return;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;

            var audioPaths = new List<string>();
            foreach (string path in DragAndDrop.paths)
                if (IsAudioFile(path)) audioPaths.Add(path);
            DragAndDrop.visualMode = audioPaths.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (current.type == EventType.DragPerform && audioPaths.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                current.Use();
                AddFiles(slot, audioPaths);
                GUIUtility.ExitGUI();
            }
            current.Use();
        }

        // ─────────────────────────────────────────────────────
        //  Files no slot uses
        // ─────────────────────────────────────────────────────

        private void DrawUnusedFiles(SoundGroup group)
        {
            var folders = new List<string>();
            foreach (SoundSlot slot in group.Slots)
                if (!folders.Contains(slot.Folder)) folders.Add(slot.Folder);

            var unused = new List<AudioFile>();
            foreach (string folder in folders)
                foreach (AudioFile file in GetFiles(folder))
                    if (!IsUsedByAnySlot(folder, file.Name)) unused.Add(file);
            if (unused.Count == 0) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"<color=#{StandInHex}><b>Files the game never plays</b></color>", richStyle);
            GUILayout.Label("Their names match no sound in this folder. Rename one to the sound it was meant for, or delete it.",
                EditorStyles.wordWrappedMiniLabel);
            foreach (AudioFile file in unused)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10f);
                if (GUILayout.Button("Play", EditorStyles.miniButtonLeft, GUILayout.Width(38f))) PlayPreview(file.Clip, 1f);
                if (GUILayout.Button("Stop", EditorStyles.miniButtonRight, GUILayout.Width(38f))) StopPreview();
                GUILayout.Label(new GUIContent(file.FileName, file.AssetPath));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Rename", EditorStyles.miniButtonLeft, GUILayout.Width(56f))) ShowRenameMenu(group, file);
                if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(50f)))
                {
                    DeleteFile(file);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private bool IsUsedByAnySlot(string folder, string fileName)
        {
            foreach (SoundGroup group in groups)
                foreach (SoundSlot slot in group.Slots)
                    if (slot.Folder == folder && slot.Accepts(fileName)) return true;
            return false;
        }

        private void PingFolder(string resourcesFolder)
        {
            string assetPath = FindFolder(resourcesFolder);
            if (assetPath == null || !AssetDatabase.IsValidFolder(assetPath))
            {
                ShowNotification(new GUIContent("No folder yet: adding a file creates it"));
                return;
            }
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            EditorGUIUtility.PingObject(folder);
            Selection.activeObject = folder;
        }

        private static bool IsAudioFile(string path) =>
            !string.IsNullOrEmpty(path) && Array.IndexOf(AudioExtensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
    }
}
