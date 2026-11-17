using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Data;
using TrashTD.Operators;

namespace TrashTD.Tests
{
    /// <summary>
    /// Keeps the Sound Manager's catalog (SoundCatalog) in step with the code: every shared sound and every unit is
    /// described, every named sound the code plays is a slot, and sound calls use SoundNames instead of typed strings.
    /// Also pins the volume groups, whose defaults must keep the volumes that used to be fixed in code.
    /// </summary>
    public class SoundCatalogTests
    {
        private const string ScriptsFolder = "Assets/_Project/Scripts";

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

        private static List<SoundGroup> BuildRealCatalog() =>
            SoundCatalog.Build(LoadAssets<OperatorData>(), LoadAssets<EnemyData>(),
                new[] { "Stage01", "Stage02", "Stage03", "Stage04", "Stage05", "Stage06" });

        // ── The catalog covers everything ──

        [Test]
        public void EverySharedSoundHasAFileNameAndADescription()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                Assert.IsTrue(AudioManager.SfxPaths.ContainsKey(id), $"{id} has no file in AudioManager.SfxPaths");
                Assert.IsTrue(SoundCatalog.HasSharedDescription(id), $"{id} has no description in SoundCatalog.Descriptions");
            }
        }

        [Test]
        public void EveryOperatorAndEnemyIsDescribed()
        {
            foreach (OperatorData data in LoadAssets<OperatorData>())
                Assert.IsTrue(SoundCatalog.HasOperatorDescription(data.operatorName),
                    $"Operator \"{data.operatorName}\" has no entry in SoundCatalog.Descriptions (OperatorEntries)");
            foreach (EnemyData data in LoadAssets<EnemyData>())
                Assert.IsTrue(SoundCatalog.HasEnemyDescription(data.enemyName),
                    $"Enemy \"{data.enemyName}\" has no entry in SoundCatalog.Descriptions (EnemyEntries)");
        }

        [Test]
        public void EverySlotIsUniqueAndEverySharedSoundIsListed()
        {
            List<SoundGroup> groups = BuildRealCatalog();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shared = new HashSet<SfxId>();
            foreach (SoundGroup group in groups)
            {
                foreach (SoundSlot slot in group.Slots)
                {
                    Assert.IsTrue(paths.Add(slot.ResourcePath), "Two slots share " + slot.ResourcePath);
                    Assert.IsFalse(string.IsNullOrEmpty(slot.WhenItPlays), slot.ResourcePath + " has no description");
                    if (slot.SharedId.HasValue) shared.Add(slot.SharedId.Value);
                }
            }
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                Assert.IsTrue(shared.Contains(id), $"{id} is in no group of the catalog");
        }

        [Test]
        public void EverySoundNameIsASlot()
        {
            List<SoundGroup> groups = BuildRealCatalog();
            foreach (FieldInfo field in typeof(SoundNames).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
                string name = (string)field.GetRawConstantValue();
                bool found = false;
                foreach (SoundGroup group in groups)
                    foreach (SoundSlot slot in group.Slots)
                        if (slot.Name == SoundNames.AbilityClip(name) || slot.Name == SoundNames.AttackClip(name) ||
                            slot.Name == SoundNames.ImpactClip(name) || Array.IndexOf(slot.Aliases, name) >= 0)
                            found = true;
                Assert.IsTrue(found, $"SoundNames.{field.Name} (\"{name}\") is played by the code but is no slot in SoundCatalog");
            }
        }

        private static readonly Regex[] LiteralSoundCalls =
        {
            new Regex(@"PlayAbilitySound\(\s*""", RegexOptions.Compiled),
            new Regex(@"PlayAttackSound\(\s*""", RegexOptions.Compiled),
            new Regex(@"PlayImpactSound\(\s*""", RegexOptions.Compiled),
            new Regex(@"UnitSfx\.(PlayAbility|PlayAttack|PlayVariant|PlayFromFolder)\([^,()]+,\s*""", RegexOptions.Compiled),
        };

        [Test]
        public void SoundCallsUseSoundNamesNotTypedStrings()
        {
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(ScriptsFolder, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimStart();
                    if (line.StartsWith("//")) continue;
                    foreach (Regex pattern in LiteralSoundCalls)
                        if (pattern.IsMatch(line)) offenders.Add($"{file.Replace('\\', '/')}:{i + 1}: {line}");
                }
            }
            Assert.IsEmpty(offenders,
                "Sound names must come from SoundNames (and be slots in SoundCatalog) so the Sound Manager knows them:\n" +
                string.Join("\n", offenders));
        }

        // ── Volume groups ──

        [Test]
        public void DefaultGroupLevelsKeepTheVolumesThatWereFixedInCode()
        {
            Assert.AreEqual(0.7f, SoundVolumeGroups.GetDefault(SoundVolumeGroups.Enemies), 1e-5f);
            Assert.AreEqual(0.8f, SoundVolumeGroups.GetDefault(SoundVolumeGroups.EnemyDeaths), 1e-5f);
            Assert.AreEqual(0.45f, SoundVolumeGroups.GetDefault(SoundVolumeGroups.Ambience), 1e-5f);
            Assert.AreEqual(0.5f, SoundVolumeGroups.GetDefault(SoundVolumeGroups.BounceKey("Progeny")), 1e-5f);
            Assert.AreEqual(1f, SoundVolumeGroups.GetDefault(SoundVolumeGroups.UI), 1e-5f);
            Assert.AreEqual(1f, SoundVolumeGroups.GetDefault(SoundVolumeGroups.OperatorKey("Qihou")), 1e-5f);
        }

        [TestCase("SFX/UI", "ButtonClick", SoundVolumeGroups.UI, null)]
        [TestCase("SFX/Gameplay", "Deploy", SoundVolumeGroups.Gameplay, null)]
        [TestCase("SFX/Gameplay", "EnemyDeath_2", SoundVolumeGroups.EnemyDeaths, null)]
        [TestCase("SFX/Combat", "Stun", SoundVolumeGroups.Combat, null)]
        [TestCase("SFX/Tiles", "TrapTick", SoundVolumeGroups.Tiles, null)]
        [TestCase("SFX/Stages/Stage04", "HeatUp", SoundVolumeGroups.StageHazards, "stage:Stage04")]
        [TestCase("SFX/Stages/Stage04", "Ambience_2", SoundVolumeGroups.Ambience, "stage:Stage04")]
        [TestCase("SFX/Operators/Guard/Qihou", "Attack_2", SoundVolumeGroups.Operators, "operator:Qihou")]
        [TestCase("SFX/Enemies/Tank/SandHulk", "Attack", SoundVolumeGroups.Enemies, "enemy:SandHulk")]
        [TestCase("SFX/Enemies/Tank/SandHulk", "Death", SoundVolumeGroups.EnemyDeaths, "enemy:SandHulk")]
        [TestCase("Music/Stages", "Stage02", null, null)]
        [TestCase(null, "Attack", null, null)]
        public void FoldersMapToTheirVolumeGroups(string folder, string clip, string category, string group)
        {
            SoundVolumeGroups.Resolve(folder, clip, out string categoryKey, out string groupKey);
            Assert.AreEqual(category, categoryKey);
            Assert.AreEqual(group, groupKey);
        }

        [Test]
        public void SettingsCombineCategoryGroupAndFileLevels()
        {
            var settings = ScriptableObject.CreateInstance<SfxVolumeSettings>();
            AudioClip clip = AudioClip.Create("Attack", 64, 1, 44100, false);
            try
            {
                Assert.AreEqual(0.7f, settings.GetGroupVolume("SFX/Enemies/Tank/SandHulk", "Attack"), 1e-5f);
                Assert.AreEqual(0.8f, settings.GetGroupVolume("SFX/Enemies/Tank/SandHulk", "Death"), 1e-5f);

                settings.SetGroupLevel(SoundVolumeGroups.EnemyKey("SandHulk"), 0.5f);
                Assert.AreEqual(0.35f, settings.GetGroupVolume("SFX/Enemies/Tank/SandHulk", "Attack"), 1e-5f);
                Assert.AreEqual(0.4f, settings.GetGroupVolume("SFX/Enemies/Tank/SandHulk", "Death"), 1e-5f);
                Assert.AreEqual(0.7f, settings.GetGroupVolume("SFX/Enemies/Grunt/SludgeGrunt", "Attack"), 1e-5f);

                settings.SetGroupLevel(SoundVolumeGroups.Enemies, 0.7f); // its default: no row needed
                Assert.AreEqual(0.7f, settings.GetGroupLevel(SoundVolumeGroups.Enemies), 1e-5f);

                Assert.AreEqual(1f, settings.GetVolume(clip), 1e-5f);
                settings.SetVolume(clip, 0.25f);
                Assert.AreEqual(0.25f, settings.GetVolume(clip), 1e-5f);
                Assert.IsTrue(settings.HasVolumeRow(clip));
                settings.SetVolume(clip, 1f);
                Assert.IsFalse(settings.HasVolumeRow(clip), "Full volume needs no row");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BounceVolumeFollowsTheBounceFalloff()
        {
            float falloff = SfxVolumeSettings.GetActiveBounceFalloff("Progeny");
            Assert.AreEqual(1f, ProgenyOperator.GetBounceVolumeMultiplier(0), 1e-5f);
            Assert.AreEqual(Mathf.Max(0.05f, falloff), ProgenyOperator.GetBounceVolumeMultiplier(1), 1e-5f);
            Assert.AreEqual(Mathf.Max(0.05f, falloff * falloff), ProgenyOperator.GetBounceVolumeMultiplier(2), 1e-5f);
        }

        // ── Status and names ──

        private static Func<string, IEnumerable<string>> Folders(Dictionary<string, string[]> files) =>
            folder => files.TryGetValue(folder, out string[] names) ? names : new string[0];

        [Test]
        public void StatusFollowsTheFallbackChain()
        {
            var slot = new SoundSlot
            {
                Folder = "SFX/Operators/Guard/Stagger",
                Name = "Ability_Pull",
                Aliases = new[] { "Pull" },
                WhenItPlays = "test",
                Fallbacks = new[] { "SFX/Operators/Guard/Stagger/Ability", "SFX/Combat/Stun" }
            };

            Assert.AreEqual(SoundStatus.Silent, SoundCatalog.GetStatus(slot, Folders(new Dictionary<string, string[]>()), out _));

            var withStun = new Dictionary<string, string[]> { { "SFX/Combat", new[] { "Stun" } } };
            Assert.AreEqual(SoundStatus.StandIn, SoundCatalog.GetStatus(slot, Folders(withStun), out string used));
            Assert.AreEqual("SFX/Combat/Stun", used);

            var withAbility = new Dictionary<string, string[]>
            {
                { "SFX/Combat", new[] { "Stun" } }, { "SFX/Operators/Guard/Stagger", new[] { "Ability_2" } }
            };
            SoundCatalog.GetStatus(slot, Folders(withAbility), out used);
            Assert.AreEqual("SFX/Operators/Guard/Stagger/Ability", used, "The unit's own Ability comes before the shared sound");

            var withOwn = new Dictionary<string, string[]> { { "SFX/Operators/Guard/Stagger", new[] { "pull" } } };
            Assert.AreEqual(SoundStatus.OwnFile, SoundCatalog.GetStatus(slot, Folders(withOwn), out _), "An older name counts");

            slot.Fallbacks = new string[0];
            slot.Optional = true;
            Assert.AreEqual(SoundStatus.Optional, SoundCatalog.GetStatus(slot, Folders(new Dictionary<string, string[]>()), out _));
        }

        [Test]
        public void MusicNeedsItsExactName()
        {
            var slot = new SoundSlot { Folder = "Music/Stages", Name = "Stage02", IsMusic = true, Fallbacks = new[] { "Music/GameplayMusic" } };
            var files = new Dictionary<string, string[]> { { "Music/Stages", new[] { "Stage02_2" } }, { "Music", new[] { "GameplayMusic" } } };
            Assert.AreEqual(SoundStatus.StandIn, SoundCatalog.GetStatus(slot, Folders(files), out string used),
                "Music has no variations: Stage02_2 is not Stage02");
            Assert.AreEqual("Music/GameplayMusic", used);
        }

        [TestCase("Attack_2", "Attack", true)]
        [TestCase("attack", "Attack", true)]
        [TestCase("Charge 1_2", "Charge 1", true)]
        [TestCase("Charge1", "Charge 1", true)]
        [TestCase("cry", "Cry", true)]
        [TestCase("Ability_Dodge", "Ability", false)]
        [TestCase("Deploy", "Death", false)]
        public void FileNamesMatchTheirSound(string clipName, string eventName, bool expected)
        {
            Assert.AreEqual(expected, UnitSfx.IsClipFor(clipName, eventName));
        }

        [Test]
        public void ExistingFilesStillMatchTheirOperatorSlots()
        {
            // Files named with older conventions must still be recognised (Progeny's cry/laser, Thornchin's Surrounding).
            List<SoundGroup> groups = BuildRealCatalog();
            SoundGroup progeny = groups.Find(g => g.Id == "operator/Progeny");
            SoundGroup thornchin = groups.Find(g => g.Id == "operator/Thornchin");
            Assert.IsNotNull(progeny);
            Assert.IsNotNull(thornchin);
            Assert.IsTrue(progeny.Slots.Exists(s => s.Name == "Deploy" && s.Accepts("cry")));
            Assert.IsTrue(progeny.Slots.Exists(s => s.Name == "Attack" && s.Accepts("laser")));
            Assert.IsTrue(thornchin.Slots.Exists(s => s.Name == "Ability_Surrounding" && s.Accepts("Surrounding")));
        }
    }
}
