using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashTD.Audio
{
    /// <summary>
    /// Sound effect volumes, edited in the Sound Manager (TRASH TD > Sound Manager) or the Inspector. The asset lives
    /// at Assets/_Project/Audio/Resources/SfxVolumeSettings.asset; AudioManager applies it to every effect it plays.
    /// Edits apply live, even in Play mode.
    ///
    /// A sound's volume is: category level x group level (<see cref="SoundVolumeGroups"/>) x its own file level x
    /// the player's SFX volume. Clips and groups without a row use their default (files 100%, groups as listed in
    /// SoundVolumeGroups). Levels only go down: Unity can't play a clip louder than recorded.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "TRASH TD/Audio/SFX Volume Settings")]
    public sealed class SfxVolumeSettings : ScriptableObject
    {
        public const string ResourceName = "SfxVolumeSettings";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The audio file to adjust (drag it here from the Project window).")]
            public AudioClip clip;

            [Tooltip("1 = as recorded. Unity can't play a clip louder than recorded; to boost one, raise it in an audio editor.")]
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Serializable]
        public sealed class GroupEntry
        {
            [Tooltip("The group: category:Enemies, stage:Stage04, operator:Qihou, enemy:SandHulk, bounce:Progeny, ...")]
            public string key;

            [Tooltip("1 = no change. For bounce:* rows, how loud each bounce is next to the one before.")]
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Tooltip("One row per clip you want quieter. Clips not listed play at full volume.")]
        [SerializeField] private List<Entry> clips = new List<Entry>();

        [Tooltip("Category and group levels. Groups not listed use their default (see SoundVolumeGroups).")]
        [SerializeField] private List<GroupEntry> groups = new List<GroupEntry>();

        private Dictionary<AudioClip, Entry> lookup;
        private Dictionary<string, GroupEntry> groupLookup;

        private static SfxVolumeSettings active;

        /// <summary>The project's settings asset (Resources/SfxVolumeSettings), or null if there is none.</summary>
        public static SfxVolumeSettings Active
        {
            get
            {
                if (active == null) active = Resources.Load<SfxVolumeSettings>(ResourceName);
                return active;
            }
        }

        // ── Files ──

        /// <summary>Volume multiplier for this clip (1 if it isn't listed).</summary>
        public float GetVolume(AudioClip clip)
        {
            if (clip == null) return 1f;
            return GetClipLookup().TryGetValue(clip, out Entry found) ? found.volume : 1f;
        }

        public bool HasVolumeRow(AudioClip clip) => clip != null && GetClipLookup().ContainsKey(clip);

        /// <summary>Sets a clip's level. 1 removes its row (full volume needs no row).</summary>
        public void SetVolume(AudioClip clip, float volume)
        {
            if (clip == null) return;

            volume = Mathf.Clamp01(volume);
            if (GetClipLookup().TryGetValue(clip, out Entry entry))
            {
                if (Mathf.Approximately(volume, 1f))
                {
                    clips.Remove(entry);
                    lookup = null;
                }
                else
                {
                    entry.volume = volume;
                }
                return;
            }

            if (Mathf.Approximately(volume, 1f)) return;
            clips.Add(new Entry { clip = clip, volume = volume });
            lookup = null;
        }

        /// <summary>Drops the row of a clip that is being deleted.</summary>
        public void RemoveClip(AudioClip clip)
        {
            if (clip == null) return;
            clips.RemoveAll(entry => entry != null && entry.clip == clip);
            lookup = null;
        }

        /// <summary>Drops rows whose clip no longer exists (a file deleted outside the Sound Manager).</summary>
        public int RemoveMissingClips()
        {
            int removed = clips.RemoveAll(entry => entry == null || entry.clip == null);
            if (removed > 0) lookup = null;
            return removed;
        }

        // ── Groups ──

        /// <summary>A group's level: its row, else its default (SoundVolumeGroups.GetDefault).</summary>
        public float GetGroupLevel(string key)
        {
            if (string.IsNullOrEmpty(key)) return 1f;
            return GetGroupLookup().TryGetValue(key, out GroupEntry entry) ? entry.volume : SoundVolumeGroups.GetDefault(key);
        }

        /// <summary>Sets a group's level. Its default level removes the row.</summary>
        public void SetGroupLevel(string key, float volume)
        {
            if (string.IsNullOrEmpty(key)) return;

            volume = Mathf.Clamp01(volume);
            bool isDefault = Mathf.Approximately(volume, SoundVolumeGroups.GetDefault(key));
            if (GetGroupLookup().TryGetValue(key, out GroupEntry entry))
            {
                if (isDefault)
                {
                    groups.Remove(entry);
                    groupLookup = null;
                }
                else
                {
                    entry.volume = volume;
                }
                return;
            }

            if (isDefault) return;
            groups.Add(new GroupEntry { key = key, volume = volume });
            groupLookup = null;
        }

        /// <summary>Category level x group level for a sound in this Resources folder ("SFX/Enemies/Tank/SandHulk").</summary>
        public float GetGroupVolume(string folder, string clipName)
        {
            SoundVolumeGroups.Resolve(folder, clipName, out string categoryKey, out string groupKey);
            return GetGroupLevel(categoryKey) * GetGroupLevel(groupKey);
        }

        /// <summary>How loud each bounce of this operator's bouncing attack is next to the one before.</summary>
        public float GetBounceFalloff(string unitFolderName) => GetGroupLevel(SoundVolumeGroups.BounceKey(unitFolderName));

        /// <summary>The bounce falloff from the active settings, or the default when there is no settings asset.</summary>
        public static float GetActiveBounceFalloff(string unitFolderName)
        {
            SfxVolumeSettings settings = Active;
            return settings != null ? settings.GetBounceFalloff(unitFolderName) : SoundVolumeGroups.DefaultBounceFalloff;
        }

        private Dictionary<AudioClip, Entry> GetClipLookup()
        {
            if (lookup != null) return lookup;

            lookup = new Dictionary<AudioClip, Entry>();
            foreach (Entry entry in clips)
                if (entry != null && entry.clip != null) lookup[entry.clip] = entry;
            return lookup;
        }

        private Dictionary<string, GroupEntry> GetGroupLookup()
        {
            if (groupLookup != null) return groupLookup;

            groupLookup = new Dictionary<string, GroupEntry>();
            foreach (GroupEntry entry in groups)
                if (entry != null && !string.IsNullOrEmpty(entry.key)) groupLookup[entry.key] = entry;
            return groupLookup;
        }

        /// <summary>Rebuilds the lookups from the lists (after an undo, or an edit made outside these methods).</summary>
        public void InvalidateLookups()
        {
            lookup = null;
            groupLookup = null;
        }

        // Inspector edits (new rows, swapped clips, renamed keys) rebuild the lookups; slider changes are read live anyway.
        private void OnValidate() => InvalidateLookups();
    }
}
