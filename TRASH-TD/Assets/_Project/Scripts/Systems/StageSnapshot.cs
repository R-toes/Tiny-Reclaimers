using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashTD.Systems
{
    /// <summary>One deck slot of a saved run. An empty slot has an empty <see cref="operatorName"/>.</summary>
    [Serializable]
    public class CardSnapshot
    {
        public string operatorName;
        public int rarity;
        public int cooldown;
        public int heat;
        public int savedHP = -1;

        public bool IsEmpty => string.IsNullOrEmpty(operatorName);
    }

    /// <summary>An operator standing on the map in a saved run.</summary>
    [Serializable]
    public class DeployedSnapshot
    {
        public string operatorName;
        public int rarity;
        public int x;
        public int y;
        public int facing;
        public int hp;
        public int heat;
    }

    /// <summary>
    /// A stage run that was saved so it can be continued later: where it was (wave and phase), the lives left, the deck and
    /// the operators on the map. A run is always saved at a moment the game can rebuild exactly: before a wave starts (so a
    /// wave that was running is replayed from its start) or during a card pick. One run is kept per stage and difficulty.
    /// </summary>
    [Serializable]
    public class StageSnapshot
    {
        public int version = StageSnapshotRules.CurrentVersion;
        public string stageId;
        public int difficulty;
        /// <summary>GameManager's wave index: the wave that is about to be played (or was running when it was saved).</summary>
        public int waveIndex;
        public int totalWaves;
        /// <summary>The phase to continue in: 0 = card pick, 1 = preparation (a wave is never resumed mid-way).</summary>
        public int phase;
        public int lives;
        public float elapsed;
        public bool leaked;
        public int draftRound;
        public int rerolls;
        public string savedAtUtc;
        public List<CardSnapshot> deck = new List<CardSnapshot>();
        public List<DeployedSnapshot> deployed = new List<DeployedSnapshot>();
    }

    /// <summary>The rules for saving and continuing a stage, kept free of game objects so they can be tested.</summary>
    public static class StageSnapshotRules
    {
        public const int CurrentVersion = 1;
        public const int PhaseCardPick = 0;
        public const int PhasePreparation = 1;

        /// <summary>
        /// True when the saved run can be continued: it belongs to a stage, sits at a phase the game can rebuild, has
        /// lives left and its lists are present. A damaged or newer save is refused instead of crashing the stage.
        /// </summary>
        public static bool IsUsable(StageSnapshot run)
        {
            if (run == null) return false;
            if (run.version < 1 || run.version > CurrentVersion) return false;
            if (string.IsNullOrEmpty(run.stageId)) return false;
            if (run.waveIndex < 0 || run.lives <= 0) return false;
            if (run.phase != PhaseCardPick && run.phase != PhasePreparation) return false;
            return run.deck != null && run.deployed != null;
        }

        /// <summary>
        /// The draft round to restore. A draft that is open when the player leaves is dealt again on return, and dealing
        /// counts a round, so one round is taken back (otherwise the next draft would look like the opening draft or skip a round).
        /// </summary>
        public static int DraftRoundToSave(int phase, int liveRound)
        {
            return phase == PhaseCardPick ? Mathf.Max(0, liveRound - 1) : liveRound;
        }

        /// <summary>"WAVE 5 OF 12" (the wave the player will play next), without the total when it is unknown.</summary>
        public static string DescribeWave(StageSnapshot run)
        {
            if (run == null) return string.Empty;
            int wave = run.waveIndex + 1;
            return run.totalWaves > 0 ? "WAVE " + Mathf.Min(wave, run.totalWaves) + " OF " + run.totalWaves : "WAVE " + wave;
        }
    }

    /// <summary>
    /// A run the player chose to continue, handed from the main menu to the stage that loads next. The stage takes it once
    /// (<see cref="Consume"/>); a request for another stage or difficulty is thrown away, so it can never apply to the wrong stage.
    /// </summary>
    public static class StageResume
    {
        private static StageSnapshot pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => pending = null;

        public static void Request(StageSnapshot run) => pending = run;

        /// <summary>The requested run if it is for this stage and difficulty (and usable), otherwise null. Clears the request.</summary>
        public static StageSnapshot Consume(string stageId, int difficulty)
        {
            StageSnapshot run = pending;
            pending = null;
            if (run == null || run.stageId != stageId || run.difficulty != difficulty) return null;
            return StageSnapshotRules.IsUsable(run) ? run : null;
        }

        /// <summary>True while a continue request is waiting (for tests).</summary>
        public static bool HasPending => pending != null;
    }
}
