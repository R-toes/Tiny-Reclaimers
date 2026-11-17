using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Core.GameLoop;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>
    /// TRASH TD > Sound Manager: every sound in the game in one window. Lists each sound slot (SoundCatalog) with
    /// what plays today (its own file, a stand-in, or nothing), plays files, sets file and group volumes
    /// (SfxVolumeSettings), and adds, replaces, renames and deletes the audio files under
    /// Assets/_Project/Audio/Resources with the exact names the game looks for.
    /// Parts: Slots (drawing the slots and volumes), Files (file operations, folder listing, preview).
    /// </summary>
    public sealed partial class SoundManagerWindow : EditorWindow
    {
        private const string ResourcesRoot = "Assets/_Project/Audio/Resources";
        private const string GuidePath = "Assets/_Project/Audio/SFX_GUIDE.md";
        private const string OverviewId = "overview";
        private const float ListWidth = 250f;

        private enum StatusFilter
        {
            All,
            Silent,
            StandIn,
            HasFile,
            Optional
        }

        private static readonly string[] StatusFilterLabels =
        {
            "All sounds", "Silent (no sound plays)", "Stand-in (another sound plays)", "Has its own file", "Optional, empty"
        };

        private List<SoundGroup> groups = new List<SoundGroup>();
        private readonly Dictionary<string, bool> sectionFoldouts = new Dictionary<string, bool>();
        private string selectedGroupId = OverviewId;
        private string search = string.Empty;
        private StatusFilter statusFilter = StatusFilter.All;
        private Vector2 listScroll;
        private Vector2 contentScroll;
        private SfxVolumeSettings settings;
        private bool needsRefresh = true;

        [MenuItem("TRASH TD/Sound Manager", false, 20)]
        public static void Open()
        {
            var window = GetWindow<SoundManagerWindow>("Sound Manager");
            window.minSize = new Vector2(820f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            needsRefresh = true;
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            StopPreview();
            DestroyPreviewSource();
            SaveSettings();
        }

        private void OnFocus() => needsRefresh = true;
        private void OnLostFocus() => SaveSettings();
        private void OnProjectChange() => needsRefresh = true;

        private void HandleUndoRedo()
        {
            if (settings != null) settings.InvalidateLookups();
            Repaint();
        }

        private void Refresh()
        {
            needsRefresh = false;
            settings = SfxVolumeSettings.Active;
            if (settings == null)
                settings = AssetDatabase.LoadAssetAtPath<SfxVolumeSettings>(ResourcesRoot + "/" + SfxVolumeSettings.ResourceName + ".asset");

            groups = SoundCatalog.Build(LoadAssets<OperatorData>(), LoadAssets<EnemyData>(), LoadStageFolders());
            folderCache.Clear();
            if (groups.Find(g => g.Id == selectedGroupId) == null) selectedGroupId = OverviewId;
        }

        private static List<T> LoadAssets<T>() where T : UnityEngine.Object
        {
            var assets = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) assets.Add(asset);
            }
            return assets;
        }

        /// <summary>The playable stages' folder names (Stage01 ...): numbered, not COMING SOON placeholders.</summary>
        private static List<string> LoadStageFolders()
        {
            var folders = new List<string>();
            foreach (StageData stage in LoadAssets<StageData>())
            {
                if (stage.comingSoon || StageProgress.GetStageNumber(stage.stageId) <= 0) continue;
                string folder = AudioManager.ToStageFolderName(stage.stageId);
                if (!folders.Contains(folder)) folders.Add(folder);
            }
            folders.Sort(StringComparer.Ordinal);
            return folders;
        }

        // ─────────────────────────────────────────────────────
        //  Layout
        // ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (needsRefresh) Refresh();

            DrawToolbar();
            if (settings == null) DrawMissingSettings();

            EditorGUILayout.BeginHorizontal();
            DrawGroupList();
            DrawContent();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search", GUILayout.Width(44f));
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
            GUILayout.Space(8f);
            statusFilter = (StatusFilter)EditorGUILayout.Popup((int)statusFilter, StatusFilterLabels, EditorStyles.toolbarPopup,
                GUILayout.Width(210f));
            GUILayout.FlexibleSpace();

            if (EditorUtility.audioMasterMute)
            {
                var muted = new GUIContent("Editor audio is muted", "Unmute it (the speaker button on the Game view) to hear previews.");
                GUILayout.Label(muted, EditorStyles.miniBoldLabel);
            }
            if (GUILayout.Button(new GUIContent("Stop", "Stop the preview"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
                StopPreview();
            if (GUILayout.Button(new GUIContent("Refresh", "Look at the files on disk again"), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                needsRefresh = true;
            if (GUILayout.Button(new GUIContent("Guide", "Open SFX_GUIDE.md"), EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                var guide = AssetDatabase.LoadAssetAtPath<TextAsset>(GuidePath);
                if (guide != null) AssetDatabase.OpenAsset(guide);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMissingSettings()
        {
            EditorGUILayout.HelpBox("There is no SfxVolumeSettings asset, so volumes can't be set.", MessageType.Warning);
            if (GUILayout.Button("Create " + ResourcesRoot + "/" + SfxVolumeSettings.ResourceName + ".asset"))
            {
                var created = CreateInstance<SfxVolumeSettings>();
                AssetDatabase.CreateAsset(created, ResourcesRoot + "/" + SfxVolumeSettings.ResourceName + ".asset");
                AssetDatabase.SaveAssets();
                needsRefresh = true;
            }
        }

        private void DrawGroupList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            DrawListEntry(OverviewId, "Overview and volumes", null);
            GUILayout.Label($"<color=#{SilentHex}>[n]</color> silent   <color=#{StandInHex}>[n]</color> stand-in",
                new GUIStyle(EditorStyles.miniLabel) { richText = true, padding = new RectOffset(8, 4, 0, 4) });

            DrawSection("Shared", SoundSection.Shared);
            DrawSection("Stages", SoundSection.Stage);
            DrawSection("Music", SoundSection.Music);
            DrawSection("Operators", SoundSection.Operator);
            DrawSection("Enemies", SoundSection.Enemy);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSection(string title, SoundSection section)
        {
            if (!sectionFoldouts.TryGetValue(title, out bool open)) open = section != SoundSection.Enemy && section != SoundSection.Operator;
            open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            sectionFoldouts[title] = open;
            if (!open) return;

            string subsection = null;
            foreach (SoundGroup group in groups)
            {
                if (group.Section != section) continue;
                if (group.Subsection != null && group.Subsection != subsection)
                {
                    subsection = group.Subsection;
                    GUILayout.Label("  " + subsection, EditorStyles.miniBoldLabel);
                }
                DrawListEntry(group.Id, group.Title, group);
            }
        }

        private static readonly Color SelectedRowColor = new Color(0.24f, 0.48f, 0.90f, 0.35f);
        private GUIStyle listEntryStyle;
        private GUIStyle listChildStyle;

        private void DrawListEntry(string id, string title, SoundGroup group)
        {
            if (listEntryStyle == null)
            {
                listEntryStyle = new GUIStyle(EditorStyles.label) { richText = true, padding = new RectOffset(6, 4, 2, 2) };
                listChildStyle = new GUIStyle(listEntryStyle) { padding = new RectOffset(18, 4, 2, 2) };
            }

            string label = title;
            if (group != null)
            {
                CountStatuses(group, out int silent, out int standIn);
                if (silent > 0) label += $"  <color=#{SilentHex}>[{silent}]</color>";
                if (standIn > 0) label += $" <color=#{StandInHex}>[{standIn}]</color>";
            }

            GUIStyle style = group != null ? listChildStyle : listEntryStyle;
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.ExpandWidth(true));
            if (selectedGroupId == id) EditorGUI.DrawRect(rect, SelectedRowColor);
            if (GUI.Button(rect, label, style))
            {
                selectedGroupId = id;
                contentScroll = Vector2.zero;
                GUI.FocusControl(null);
            }
        }

        private void CountStatuses(SoundGroup group, out int silent, out int standIn)
        {
            silent = 0;
            standIn = 0;
            foreach (SoundSlot slot in group.Slots)
            {
                SoundStatus status = GetStatus(slot, out _);
                if (status == SoundStatus.Silent) silent++;
                else if (status == SoundStatus.StandIn) standIn++;
            }
        }

        private void DrawContent()
        {
            EditorGUILayout.BeginVertical();
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);

            bool filtering = !string.IsNullOrEmpty(search) || statusFilter != StatusFilter.All;
            if (filtering) DrawFilteredSlots();
            else if (selectedGroupId == OverviewId) DrawOverview();
            else
            {
                SoundGroup group = groups.Find(g => g.Id == selectedGroupId);
                if (group != null) DrawGroup(group);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFilteredSlots()
        {
            int shown = 0;
            foreach (SoundGroup group in groups)
            {
                bool headerDrawn = false;
                foreach (SoundSlot slot in group.Slots)
                {
                    if (!MatchesFilter(group, slot)) continue;
                    if (!headerDrawn)
                    {
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.LabelField($"{SectionLabel(group)}{group.Title}", EditorStyles.boldLabel);
                        headerDrawn = true;
                    }
                    DrawSlot(group, slot);
                    shown++;
                }
            }
            if (shown == 0) EditorGUILayout.HelpBox("No sound matches the search and filter.", MessageType.Info);
        }

        private bool MatchesFilter(SoundGroup group, SoundSlot slot)
        {
            if (statusFilter != StatusFilter.All)
            {
                SoundStatus status = GetStatus(slot, out _);
                bool keep = statusFilter switch
                {
                    StatusFilter.Silent => status == SoundStatus.Silent,
                    StatusFilter.StandIn => status == SoundStatus.StandIn,
                    StatusFilter.HasFile => status == SoundStatus.OwnFile,
                    StatusFilter.Optional => status == SoundStatus.Optional,
                    _ => true
                };
                if (!keep) return false;
            }

            if (string.IsNullOrEmpty(search)) return true;
            string term = search.Trim();
            return Contains(group.Title, term) || Contains(slot.Name, term) || Contains(slot.WhenItPlays, term) ||
                   Contains(slot.ResourcePath, term) || Array.Exists(slot.Aliases, alias => Contains(alias, term));
        }

        private static bool Contains(string text, string term) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string SectionLabel(SoundGroup group)
        {
            return group.Section switch
            {
                SoundSection.Operator => "Operator  ·  " + group.Subsection + "  ·  ",
                SoundSection.Enemy => "Enemy  ·  " + group.Subsection + "  ·  ",
                SoundSection.Stage => "Stage  ·  ",
                _ => string.Empty
            };
        }

        /// <summary>The Assets path of a Resources folder ("SFX/UI" -> Assets/_Project/Audio/Resources/SFX/UI).</summary>
        private static string ToAssetPath(string resourcesFolder) => ResourcesRoot + "/" + resourcesFolder;

        private static string ToFullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
