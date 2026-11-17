using System;
using System.Collections.Generic;
using TrashTD.Data;

namespace TrashTD.Audio
{
    public enum SoundSection
    {
        Shared,   // UI, Gameplay, Combat, Tiles
        Stage,    // one stage's hazards and ambience
        Music,
        Operator,
        Enemy
    }

    /// <summary>What happens today when a slot's sound should play.</summary>
    public enum SoundStatus
    {
        OwnFile,   // its own file plays
        StandIn,   // another file (or a generated sound) plays instead
        Optional,  // nothing extra plays, and nothing is missed (Deploy, Impact, variants)
        Silent     // nothing plays
    }

    /// <summary>
    /// One sound the game can play: a file name in a Resources folder, when it plays, and what plays without it.
    /// </summary>
    public sealed class SoundSlot
    {
        private static readonly string[] None = new string[0];

        /// <summary>Resources folder, e.g. "SFX/Operators/Guard/Qihou" or "Music/Stages".</summary>
        public string Folder;
        /// <summary>The file name to use (no extension), e.g. "Ability_Dodge".</summary>
        public string Name;
        /// <summary>Other file names the game also accepts for this sound (older names), e.g. "Dodge".</summary>
        public string[] Aliases = None;
        public string WhenItPlays;
        /// <summary>Resources paths ("SFX/UI/ButtonHover") tried in order when this slot has no file.</summary>
        public string[] Fallbacks = None;
        /// <summary>What plays when neither this slot nor a fallback has a file (e.g. "a blip generated in code"); null = silence.</summary>
        public string FallbackNote;
        /// <summary>Nothing is missed without a file (an extra on top of something that always plays, or a variant).</summary>
        public bool Optional;
        /// <summary>A looping sound (stage ambience, Mlem's digest).</summary>
        public bool IsLoop;
        /// <summary>A music track: one file, found by its exact name, no variations, no volume row.</summary>
        public bool IsMusic;
        /// <summary>The shared sound id, for shared slots.</summary>
        public SfxId? SharedId;

        public string ResourcePath => Folder + "/" + Name;
        public bool AllowsVariations => !IsMusic;

        /// <summary>True if a file with this name (no extension) plays for this slot: "Attack_2", "dodge", an alias.</summary>
        public bool Accepts(string clipName)
        {
            if (IsMusic) return string.Equals(clipName, Name, StringComparison.OrdinalIgnoreCase);
            if (UnitSfx.IsClipFor(clipName, Name)) return true;
            foreach (string alias in Aliases)
                if (UnitSfx.IsClipFor(clipName, alias)) return true;
            return false;
        }
    }

    /// <summary>A set of slots shown together: one shared folder, one stage, the music, one operator or one enemy.</summary>
    public sealed class SoundGroup
    {
        public string Id;          // "shared/UI", "stage/Stage04", "music", "operator/Qihou", "enemy/SandHulk"
        public string Title;       // "UI", "Stage 04", "Music", "Qihou", "Sand Hulk"
        public SoundSection Section;
        public string Subsection;  // operator class or enemy archetype, else null
        public string Folder;      // the folder most of its files live in
        /// <summary>Its own volume group (stage, operator or enemy), or null for shared folders and music.</summary>
        public string GroupKey;
        /// <summary>The bounce falloff key for operators with a bouncing attack (Progeny, Resicochet), else null.</summary>
        public string BounceKey;
        public readonly List<SoundSlot> Slots = new List<SoundSlot>();
    }

    /// <summary>
    /// Every sound slot in the game, built from the shared sound table (AudioManager.SfxPaths), the stages and the
    /// operator and enemy data assets. The Sound Manager window lists it; SoundCatalogTests keeps it in step with the
    /// code. When code starts playing a new sound, add it here (and to SoundNames for a named unit sound).
    /// Descriptions live in SoundCatalog.Descriptions.cs.
    /// </summary>
    public static partial class SoundCatalog
    {
        public const string SfxRoot = "SFX";
        public const string MusicRoot = "Music";

        private static readonly (string folder, string title)[] SharedFolders =
        {
            ("UI", "UI"), ("Gameplay", "Gameplay"), ("Combat", "Combat"), ("Tiles", "Tiles")
        };

        /// <param name="stageFolders">The playable stages' folder names ("Stage01" ...), see AudioManager.ToStageFolderName.</param>
        public static List<SoundGroup> Build(IEnumerable<OperatorData> operators, IEnumerable<EnemyData> enemies,
            IEnumerable<string> stageFolders)
        {
            var groups = new List<SoundGroup>();

            foreach ((string folder, string title) in SharedFolders)
            {
                var group = new SoundGroup
                {
                    Id = "shared/" + folder, Title = title, Section = SoundSection.Shared, Folder = SfxRoot + "/" + folder
                };
                foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                    if (AudioManager.GetSharedFolder(id) == group.Folder) group.Slots.Add(CreateSharedSlot(id));
                groups.Add(group);
            }

            var stages = new SortedSet<string>(StringComparer.Ordinal);
            if (stageFolders != null)
                foreach (string stage in stageFolders)
                    if (!string.IsNullOrEmpty(stage)) stages.Add(stage);
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                string folder = AudioManager.GetSharedFolder(id);
                if (folder != null && folder.StartsWith(SfxRoot + "/Stages/")) stages.Add(folder.Substring((SfxRoot + "/Stages/").Length));
            }
            foreach (string stage in stages) groups.Add(BuildStageGroup(stage));

            groups.Add(BuildMusicGroup(stages));

            var operatorGroups = new List<SoundGroup>();
            if (operators != null)
                foreach (OperatorData data in operators)
                    if (data != null) operatorGroups.Add(BuildOperatorGroup(data));
            operatorGroups.Sort(CompareUnits);
            groups.AddRange(operatorGroups);

            var enemyGroups = new List<SoundGroup>();
            if (enemies != null)
                foreach (EnemyData data in enemies)
                    if (data != null) enemyGroups.Add(BuildEnemyGroup(data));
            enemyGroups.Sort(CompareUnits);
            groups.AddRange(enemyGroups);

            return groups;
        }

        private static int CompareUnits(SoundGroup a, SoundGroup b)
        {
            int bySubsection = string.CompareOrdinal(a.Subsection, b.Subsection);
            return bySubsection != 0 ? bySubsection : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        }

        private static SoundSlot CreateSharedSlot(SfxId id)
        {
            SharedEntry entry = GetSharedEntry(id);
            var slot = new SoundSlot
            {
                Folder = AudioManager.GetSharedFolder(id),
                Name = AudioManager.GetSharedFileName(id),
                WhenItPlays = entry.when,
                FallbackNote = entry.note,
                SharedId = id
            };
            if (entry.fallback.HasValue) slot.Fallbacks = new[] { SharedPath(entry.fallback.Value) };
            return slot;
        }

        private static string SharedPath(SfxId id) => AudioManager.GetSharedFolder(id) + "/" + AudioManager.GetSharedFileName(id);

        private static SoundGroup BuildStageGroup(string stageFolder)
        {
            string folder = SfxRoot + "/Stages/" + stageFolder;
            var group = new SoundGroup
            {
                Id = "stage/" + stageFolder,
                Title = stageFolder.Replace("Stage", "Stage "),
                Section = SoundSection.Stage,
                Folder = folder,
                GroupKey = SoundVolumeGroups.StageKey(stageFolder)
            };

            group.Slots.Add(new SoundSlot
            {
                Folder = folder,
                Name = "Ambience",
                IsLoop = true,
                WhenItPlays = "The stage's background loop, under the music for the whole stage (planning, waves and pause). " +
                              "Make it a seamless loop of 10-30 s. Variations: one is picked when the stage starts."
            });
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                if (AudioManager.GetSharedFolder(id) == folder) group.Slots.Add(CreateSharedSlot(id));
            return group;
        }

        private static SoundGroup BuildMusicGroup(IEnumerable<string> stages)
        {
            var group = new SoundGroup { Id = "music", Title = "Music", Section = SoundSection.Music, Folder = MusicRoot };
            group.Slots.Add(new SoundSlot
            {
                Folder = MusicRoot, Name = "MainMenuMusic", IsMusic = true, IsLoop = true, WhenItPlays = "The main menu and stage map."
            });
            group.Slots.Add(new SoundSlot
            {
                Folder = MusicRoot, Name = "GameplayMusic", IsMusic = true, IsLoop = true,
                WhenItPlays = "Any stage without its own track."
            });
            foreach (string stage in stages)
            {
                group.Slots.Add(new SoundSlot
                {
                    Folder = MusicRoot + "/Stages",
                    Name = stage,
                    IsMusic = true,
                    IsLoop = true,
                    WhenItPlays = stage.Replace("Stage", "Stage ") + "'s own track (the sandbox plays the track of the map it shows).",
                    Fallbacks = new[] { MusicRoot + "/GameplayMusic" }
                });
            }
            return group;
        }

        private static SoundGroup BuildOperatorGroup(OperatorData data)
        {
            string folder = UnitSfx.GetFolder(data);
            string folderName = UnitSfx.ToFolderName(data.operatorName);
            var group = new SoundGroup
            {
                Id = "operator/" + folderName,
                Title = data.operatorName,
                Section = SoundSection.Operator,
                Subsection = data.operatorClass.ToString(),
                Folder = folder,
                GroupKey = SoundVolumeGroups.OperatorKey(folderName)
            };
            if (HasBouncingAttack(data.operatorName)) group.BounceKey = SoundVolumeGroups.BounceKey(folderName);

            OperatorEntry entry = GetOperatorEntry(data.operatorName);

            group.Slots.Add(new SoundSlot
            {
                Folder = folder,
                Name = nameof(UnitSfxEvent.Deploy),
                Aliases = entry.deployAliases ?? new string[0],
                Optional = true,
                WhenItPlays = "Placed on the field. Plays on top of Gameplay/Deploy, which plays on every deploy."
            });

            if (!entry.noAttack)
            {
                string attackFallback = entry.attackFallback.HasValue
                    ? SharedPath(entry.attackFallback.Value)
                    : entry.silentAttack ? null : SharedPath(GetDefaultAttackFallback(data));
                group.Slots.Add(new SoundSlot
                {
                    Folder = folder,
                    Name = nameof(UnitSfxEvent.Attack),
                    Aliases = entry.attackAliases ?? new string[0],
                    WhenItPlays = entry.attack ?? "Its attack starts (swing, shot, cast).",
                    Fallbacks = attackFallback != null ? new[] { attackFallback } : new string[0]
                });
            }

            if (!entry.noImpact)
            {
                group.Slots.Add(new SoundSlot
                {
                    Folder = folder,
                    Name = nameof(UnitSfxEvent.Impact),
                    Optional = true,
                    WhenItPlays = (entry.impact ?? "One of its hits lands (once per volley, even on several enemies).") +
                                  " Cuts a still-running Attack sound short."
                });
            }

            if (entry.abilities != null)
                foreach (AbilityEntry ability in entry.abilities) group.Slots.Add(CreateAbilitySlot(folder, ability));

            group.Slots.Add(new SoundSlot
            {
                Folder = folder,
                Name = nameof(UnitSfxEvent.Death),
                WhenItPlays = "Dies.",
                Fallbacks = new[] { SharedPath(SfxId.GameplayOperatorDeath) }
            });
            return group;
        }

        private static SfxId GetDefaultAttackFallback(OperatorData data)
        {
            if (data.damageType == DamageType.Arts) return SfxId.GameplayMagicAttack;
            return data.position == OperatorPosition.Ranged ? SfxId.GameplayRangedPhysicalAttack : SfxId.GameplayMeleeAttack;
        }

        private static SoundGroup BuildEnemyGroup(EnemyData data)
        {
            string folder = UnitSfx.GetFolder(data);
            string folderName = UnitSfx.ToFolderName(data.enemyName);
            var group = new SoundGroup
            {
                Id = "enemy/" + folderName,
                Title = data.enemyName,
                Section = SoundSection.Enemy,
                Subsection = data.archetype.ToString(),
                Folder = folder,
                GroupKey = SoundVolumeGroups.EnemyKey(folderName)
            };

            EnemyEntry entry = GetEnemyEntry(data.enemyName);
            group.Slots.Add(new SoundSlot
            {
                Folder = folder,
                Name = nameof(UnitSfxEvent.Attack),
                WhenItPlays = entry.attack ?? "It attacks (a melee hit on its blocker, or its ranged attack).",
                Fallbacks = entry.attackFallback.HasValue ? new[] { SharedPath(entry.attackFallback.Value) } : new string[0]
            });
            group.Slots.Add(new SoundSlot
            {
                Folder = folder,
                Name = nameof(UnitSfxEvent.Impact),
                Optional = true,
                WhenItPlays = (entry.impact ?? "Its ranged attack lands (melee-only enemies never play this).") +
                              " Cuts a still-running Attack sound short."
            });

            if (entry.extras != null)
                foreach (AbilityEntry extra in entry.extras) group.Slots.Add(CreateAbilitySlot(folder, extra));

            group.Slots.Add(new SoundSlot
            {
                Folder = folder,
                Name = nameof(UnitSfxEvent.Death),
                WhenItPlays = "Dies.",
                Fallbacks = new[] { SharedPath(SfxId.GameplayEnemyDeath) }
            });
            return group;
        }

        private static SoundSlot CreateAbilitySlot(string folder, AbilityEntry ability)
        {
            var fallbacks = new List<string>();
            if (ability.fallbackNames != null)
                foreach (string name in ability.fallbackNames) fallbacks.Add(folder + "/" + name);
            if (ability.sharedFallback.HasValue) fallbacks.Add(SharedPath(ability.sharedFallback.Value));

            return new SoundSlot
            {
                Folder = folder,
                Name = ability.name,
                Aliases = ability.aliases ?? new string[0],
                WhenItPlays = ability.when,
                Fallbacks = fallbacks.ToArray(),
                FallbackNote = ability.note,
                Optional = ability.optional,
                IsLoop = ability.loop
            };
        }

        /// <summary>
        /// What plays for a slot today, given the audio file names (no extension) in each Resources folder.
        /// usedFallback is the Resources path or note that plays instead, when the status is StandIn.
        /// </summary>
        public static SoundStatus GetStatus(SoundSlot slot, Func<string, IEnumerable<string>> filesInFolder, out string usedFallback)
        {
            usedFallback = null;
            if (slot == null) return SoundStatus.Silent;
            if (HasFile(slot.Folder, filesInFolder, slot.Accepts)) return SoundStatus.OwnFile;

            foreach (string path in slot.Fallbacks)
            {
                int slash = path.LastIndexOf('/');
                string folder = path.Substring(0, slash);
                string name = path.Substring(slash + 1);
                bool exact = path.StartsWith(MusicRoot + "/");
                if (HasFile(folder, filesInFolder, file => exact
                        ? string.Equals(file, name, StringComparison.OrdinalIgnoreCase)
                        : UnitSfx.IsClipFor(file, name)))
                {
                    usedFallback = path;
                    return SoundStatus.StandIn;
                }
            }

            if (!string.IsNullOrEmpty(slot.FallbackNote))
            {
                usedFallback = slot.FallbackNote;
                return SoundStatus.StandIn;
            }
            return slot.Optional ? SoundStatus.Optional : SoundStatus.Silent;
        }

        private static bool HasFile(string folder, Func<string, IEnumerable<string>> filesInFolder, Func<string, bool> accepts)
        {
            IEnumerable<string> files = filesInFolder(folder);
            if (files == null) return false;
            foreach (string file in files)
                if (accepts(file)) return true;
            return false;
        }
    }
}
