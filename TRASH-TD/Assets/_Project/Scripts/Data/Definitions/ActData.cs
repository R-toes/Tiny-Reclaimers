using System;
using UnityEngine;

namespace TrashTD.Data
{
    /// <summary>One stage of an act and where its marker sits on that act's stage map.</summary>
    [Serializable]
    public class ActStageEntry
    {
        public StageData stage;

        [Tooltip("Where this stage's marker sits on the act's stage map: 0-1 across the map art, origin top-left (0,0), bottom-right (1,1).")]
        public Vector2 mapPosition = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// An act: a group of stages played on one stage map (its own map art, marker positions and zone). The game starts with
    /// Act 1, the six stages STAGE_01 to STAGE_06; later acts add more stages on a different map.
    ///
    /// Stage ids stay global and contiguous across acts (Act 2 continues with STAGE_07, ...): saves, audio folders, Intel and
    /// the unlock chain (a stage needs the one before it) are all keyed by them and need no change. The act only groups
    /// stages for the stage map and for labels (ACT 2 // STAGE 01 is the first stage of Act 2). A stage listed in an act
    /// is unlocked by clearing the stage before it, so the first stage of an act is unlocked by clearing the last stage
    /// of the previous one, and an act is open when its first stage is.
    ///
    /// To add an act: see PROJECT_GUIDELINES.md, "Acts". Acts are loaded from Resources/Acts (ActCatalog).
    /// </summary>
    [CreateAssetMenu(fileName = "ACT_02", menuName = "TRASH TD/Act Data")]
    public class ActData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("1 for Act 1. Acts are shown in this order.")]
        public int actNumber = 1;

        [Tooltip("The zone this act takes place in, shown under ACT N on the stage map (for example: NERC Development Zone).")]
        public string zoneName;

        [Tooltip("The hover card under the zone name.")]
        [TextArea(3, 10)]
        public string zoneLore;

        [Tooltip("The small heading on the lore card (for example: NATIONAL ENERGY RESOURCE CORPORATION).")]
        public string loreTag;

        [Header("Stage map")]
        [Tooltip("The stage selector map art for this act, also shown on its card on the act selection screen. Leave empty to use the project's default Stage Selector Map.")]
        public Sprite stageMapSprite;

        [Tooltip("The act's stages in play order. Their ids must continue the previous act's (STAGE_07 after STAGE_06).")]
        public ActStageEntry[] stages = new ActStageEntry[0];

        [Tooltip("Optional: the tutorial stage (STAGE 00), shown as a marker on this act's map. Only Act 1 has one.")]
        public StageData tutorialStage;

        [Tooltip("Where the tutorial marker sits on the map (0-1, origin top-left).")]
        public Vector2 tutorialMapPosition = new Vector2(0.5f, 0.5f);

        /// <summary>"ACT 1".</summary>
        public string DisplayName => "ACT " + actNumber;

        public int StageCount => stages != null ? stages.Length : 0;

        /// <summary>A placeholder act: its first stage is marked coming soon, so the act can never be opened (not even in dev mode).</summary>
        public bool IsComingSoon => FirstStage != null && FirstStage.comingSoon;

        /// <summary>The first stage of the act, or null when it has none.</summary>
        public StageData FirstStage => StageCount > 0 ? stages[0].stage : null;

        /// <summary>The last stage of the act, or null when it has none.</summary>
        public StageData LastStage => StageCount > 0 ? stages[StageCount - 1].stage : null;

        /// <summary>Position (1-based) of the stage with this id in the act, or 0 when it is not part of it.</summary>
        public int GetPosition(string stageId)
        {
            if (string.IsNullOrEmpty(stageId) || stages == null) return 0;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null && stages[i].stage != null && stages[i].stage.stageId == stageId) return i + 1;
            }
            return 0;
        }
    }
}
