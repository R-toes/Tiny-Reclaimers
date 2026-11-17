using System.Collections.Generic;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Development;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// The acts of the game and the questions the UI asks about them: which act a stage belongs to, its label ("STAGE 03" while
    /// there is one act, "ACT 2 // STAGE 01" once there are several), and whether an act is open. Acts are ActData assets in
    /// Resources/Acts. Until Act 1's asset exists the catalog builds Act 1 from the original six stages and their marker
    /// positions (<see cref="CreateBuiltInAct1"/>), so the game behaves the same before and after the asset is created
    /// (TRASH TD > Acts > Create Act 1 Asset).
    /// </summary>
    public static class ActCatalog
    {
        public const string ResourcesFolder = "Acts";
        public const string Act1AssetPath = "Assets/Resources/Acts/ACT_01.asset";

        /// <summary>Act 1's stage selector map art: its Resources name, and the project file it is edited as (used when Resources has no copy).</summary>
        public const string Act1MapResourceName = "Act1StageSelectorMap";
        public const string Act1MapEditorPath = "Assets/_Project/Art/Levels/Act 1 Stage Selector Map.png";

        // ---- Act 1 as the game first shipped it (the source for the built-in Act 1 and for the editor command)
        private const string BuiltInZoneName = "NERC Development Zone";
        private const string BuiltInLoreTag = "\u25C6 NATIONAL ENERGY RESOURCE CORPORATION";
        private const string BuiltInZoneLore = "Once a thriving industrial region built around energy production, Calder Basin became the center of NERC's ambitious development program. What began as an effort to modernize the nation's energy infrastructure eventually left behind abandoned facilities, contaminated settlements, and entire districts deemed uninhabitable. Now, the NERC Development Zone is a fractured landscape of decaying industry and tightly controlled settlements, where the remnants of the corporation's legacy are impossible to escape.";

        // Marker positions on Act 1's stage selector map, origin top-left
        private static readonly Vector2[] BuiltInStagePositions =
        {
            new Vector2(0.245f, 0.740f),  // Stage 1
            new Vector2(0.380f, 0.550f),  // Stage 2
            new Vector2(0.470f, 0.740f),  // Stage 3
            new Vector2(0.610f, 0.460f),  // Stage 4
            new Vector2(0.820f, 0.670f),  // Stage 5
            new Vector2(0.820f, 0.350f),  // Stage 6
        };

        // STAGE 00, the tutorial: the small outpost by the lake on the map's west side
        private static readonly Vector2 BuiltInTutorialPosition = new Vector2(0.275f, 0.345f);

        private static List<ActData> loaded;

        // The editor can keep statics between Play sessions (domain reload off): load the acts again each session
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => loaded = null;

        /// <summary>Every act, in order (Act 1 first). Never empty.</summary>
        public static IReadOnlyList<ActData> All
        {
            get
            {
                if (loaded == null) loaded = Load();
                return loaded;
            }
        }

        /// <summary>More than one act: the world map is shown and stage labels carry their act.</summary>
        public static bool HasSeveralActs => All.Count > 1;

        /// <summary>Replaces the acts (tests). Pass null to load from Resources again.</summary>
        public static void UseActs(IEnumerable<ActData> acts)
        {
            loaded = acts == null ? null : Sort(acts);
            if (loaded != null && loaded.Count == 0) loaded = null;
        }

        private static List<ActData> Load()
        {
            List<ActData> found = Sort(Resources.LoadAll<ActData>(ResourcesFolder));

            // Act 1 is always there: until its asset exists (TRASH TD > Acts > Create Act 1 Asset) it is built in
            if (!found.Exists(act => act.actNumber == 1)) found.Insert(0, CreateBuiltInAct1());
            return found;
        }

        private static List<ActData> Sort(IEnumerable<ActData> acts)
        {
            var list = new List<ActData>();
            foreach (ActData act in acts)
            {
                if (act != null) list.Add(act);
            }
            list.Sort((a, b) => a.actNumber.CompareTo(b.actNumber));
            return list;
        }

        /// <summary>
        /// Act 1 built from the six original stage assets and the marker positions the map used before acts existed. Used when
        /// there is no ACT_01 asset, and by the editor command that creates it.
        /// </summary>
        public static ActData CreateBuiltInAct1()
        {
            ActData act = ScriptableObject.CreateInstance<ActData>();
            act.name = "ACT_01 (built in)";
            act.actNumber = 1;
            act.zoneName = BuiltInZoneName;
            act.zoneLore = BuiltInZoneLore;
            act.loreTag = BuiltInLoreTag;

            var entries = new List<ActStageEntry>();
            for (int i = 0; i < BuiltInStagePositions.Length; i++)
            {
                StageData stage = Resources.Load<StageData>("Stages/" + StageProgress.GetStageId(i + 1));
                if (stage == null) continue;
                entries.Add(new ActStageEntry { stage = stage, mapPosition = BuiltInStagePositions[i] });
            }
            act.stages = entries.ToArray();

            act.tutorialStage = Resources.Load<StageData>(TutorialDirector.TutorialStageResourcePath);
            act.tutorialMapPosition = BuiltInTutorialPosition;
            return act;
        }

        public static ActData GetAct(int actNumber)
        {
            foreach (ActData act in All)
            {
                if (act.actNumber == actNumber) return act;
            }
            return null;
        }

        /// <summary>The act that lists this stage, or null (the tutorial, the sandbox and unknown ids belong to none).</summary>
        public static ActData GetActOfStage(string stageId)
        {
            foreach (ActData act in All)
            {
                if (act.GetPosition(stageId) > 0) return act;
            }
            return null;
        }

        /// <summary>The stage's position (1-based) within its act, or 0 when it is not part of one.</summary>
        public static int GetPositionInAct(string stageId)
        {
            ActData act = GetActOfStage(stageId);
            return act != null ? act.GetPosition(stageId) : 0;
        }

        /// <summary>
        /// "STAGE 03" while there is a single act, "ACT 2 // STAGE 01" with several (the position is within the act). The
        /// tutorial and any stage outside the acts read "STAGE 00" / "STAGE NN" from their id.
        /// </summary>
        public static string FormatStageLabel(string stageId)
        {
            ActData act = GetActOfStage(stageId);
            if (act == null) return "STAGE " + StageProgress.GetStageNumber(stageId).ToString("00");

            string stage = "STAGE " + act.GetPosition(stageId).ToString("00");
            return HasSeveralActs ? act.DisplayName + " // " + stage : stage;
        }

        /// <summary>
        /// An act can be opened only when its first stage is a real stage (not a coming-soon placeholder) and is unlocked. Act 1
        /// always is. Dev mode unlocks stages as usual, so it opens every real act, but it never opens a placeholder.
        /// </summary>
        public static bool IsActUnlocked(ActData act)
        {
            if (act == null) return false;

            StageData first = act.FirstStage;
            if (first == null || first.comingSoon) return false;
            return StageProgress.IsAnyDifficultyUnlocked(first.stageId);
        }

        /// <summary>What a locked act needs ("CLEAR ACT 1 // STAGE 06 FIRST"), or an empty string when it is open or has no stages.</summary>
        public static string GetActUnlockRequirement(ActData act)
        {
            if (act == null || act.FirstStage == null) return string.Empty;
            if (act.IsComingSoon) return "COMING SOON";

            int number = StageProgress.GetStageNumber(act.FirstStage.stageId);
            if (number <= 1) return string.Empty;
            return "CLEAR " + FormatStageLabel(StageProgress.GetStageId(number - 1)) + " FIRST";
        }

        /// <summary>How many of the act's stages have been cleared on any difficulty.</summary>
        public static int GetClearedCount(ActData act)
        {
            if (act == null || act.stages == null) return 0;

            int cleared = 0;
            foreach (ActStageEntry entry in act.stages)
            {
                if (entry == null || entry.stage == null) continue;
                foreach (StageDifficulty difficulty in System.Enum.GetValues(typeof(StageDifficulty)))
                {
                    if (StageProgress.IsCleared(entry.stage.stageId, difficulty))
                    {
                        cleared++;
                        break;
                    }
                }
            }
            return cleared;
        }

        /// <summary>
        /// Problems with how the acts are set up, for the tests and the editor command (an empty list means fine): each act
        /// number used once, every stage in one act only, stage ids contiguous across the acts in order (the unlock chain is
        /// "the stage before"), and every marker on its map.
        /// </summary>
        public static List<string> FindProblems(IReadOnlyList<ActData> acts)
        {
            var problems = new List<string>();
            var actNumbers = new HashSet<int>();
            var stageOwners = new Dictionary<string, string>();
            int expectedNumber = 1;

            foreach (ActData act in acts)
            {
                if (act == null) continue;
                if (!actNumbers.Add(act.actNumber)) problems.Add(act.name + ": act number " + act.actNumber + " is used by another act");
                if (act.StageCount == 0) problems.Add(act.name + ": has no stages");

                for (int i = 0; i < act.StageCount; i++)
                {
                    ActStageEntry entry = act.stages[i];
                    if (entry == null || entry.stage == null)
                    {
                        problems.Add(act.name + ": stage entry " + (i + 1) + " is empty");
                        continue;
                    }

                    string id = entry.stage.stageId;
                    if (stageOwners.TryGetValue(id, out string owner)) problems.Add(id + " is in both " + owner + " and " + act.name);
                    else stageOwners[id] = act.name;

                    int number = StageProgress.GetStageNumber(id);
                    if (number != expectedNumber) problems.Add(act.name + ": " + id + " should be " + StageProgress.GetStageId(expectedNumber) + " (ids continue across acts, in order)");
                    expectedNumber = number + 1;

                    Vector2 p = entry.mapPosition;
                    if (p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f) problems.Add(act.name + ": " + id + " is placed outside its map " + p);
                }
            }
            return problems;
        }
    }
}
