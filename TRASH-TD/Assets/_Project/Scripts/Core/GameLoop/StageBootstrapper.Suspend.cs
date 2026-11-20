using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Core.GameLoop
{
    // Save & Exit: saving a stage run, and continuing it later.
    //
    // What a saved run holds (StageSnapshot): the wave, the lives, the deck (slot by slot) and every operator on the map (who,
    // where, facing, rarity, HP). It is always saved at a moment the game can rebuild exactly: a wave that is running is
    // saved as it was when the wave STARTED (the wave is replayed), and a card pick is saved before the offer, which is dealt
    // again on return. What is not kept: enemies on the map, temporary effects and buffs, a Framiliar's copy, and
    // a card still waiting in the "deck full" queue. The tutorial and the sandbox cannot be saved.
    public partial class StageBootstrapper
    {
        // The state at the moment the current wave started (what a run saved mid-wave goes back to)
        private StageSnapshot waveStartSnapshot;

        /// <summary>True when the run can be saved now: a real stage (not the sandbox or the tutorial) that is under way.</summary>
        public bool CanSuspend =>
            stageData != null && !IsSandbox && !IsTutorial && gameManager != null && playerDeck != null && operatorManager != null &&
            cardDraftSystem != null && (gameManager.CurrentState == GamePlayState.Playing || gameManager.CurrentState == GamePlayState.Paused);

        /// <summary>The wave a saved run would continue from, for the leave-the-stage message ("WAVE 5 OF 12").</summary>
        public string SuspendDescription()
        {
            StageSnapshot preview = BuildSuspendSnapshot();
            return preview != null ? StageSnapshotRules.DescribeWave(preview) : string.Empty;
        }

        /// <summary>Saves the run to the save file. False when it cannot be saved or the file could not be written.</summary>
        public bool SuspendAndSave()
        {
            StageSnapshot run = BuildSuspendSnapshot();
            if (run == null) return false;

            SaveSystem.Data.SetRun(run);
            return SaveSystem.Save();
        }

        /// <summary>Forgets the saved run of this stage (it was won or lost, or the player started again).</summary>
        private void ClearSuspendedRun()
        {
            if (stageData == null) return;
            if (SaveSystem.Data.ClearRun(stageData.stageId, (int)difficulty)) SaveSystem.Save();
        }

        private StageSnapshot BuildSuspendSnapshot()
        {
            if (!CanSuspend) return null;

            switch (gameManager.CurrentPhase)
            {
                case StagePhase.WaveActive:
                    return waveStartSnapshot ?? CaptureRun(StageSnapshotRules.PhasePreparation);
                case StagePhase.Preparation:
                    return CaptureRun(StageSnapshotRules.PhasePreparation);
                default:
                    return CaptureRun(StageSnapshotRules.PhaseCardPick);
            }
        }

        private void HandlePhaseForSuspend(StagePhase phase)
        {
            // A wave is about to run: remember how things stand, so leaving mid-wave can go back to this moment
            if (phase == StagePhase.WaveActive && CanSuspend) waveStartSnapshot = CaptureRun(StageSnapshotRules.PhasePreparation);
        }

        private void HandleRunFinished(int stars) => ClearSuspendedRun();
        private void HandleRunFinished() => ClearSuspendedRun();

        /// <summary>Phones close apps without asking: going to the background saves the run, so it is there to continue.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && Application.isMobilePlatform && CanSuspend) SuspendAndSave();
        }

        private StageSnapshot CaptureRun(int resumePhase)
        {
            var run = new StageSnapshot
            {
                stageId = stageData.stageId,
                difficulty = (int)difficulty,
                waveIndex = gameManager.GetCurrentWaveIndex(),
                totalWaves = waveManager != null ? waveManager.TotalWaves : 0,
                phase = resumePhase,
                lives = gameManager.CurrentLifePoints,
                elapsed = gameManager.ElapsedTime,
                leaked = gameManager.LeakedAnyEnemy,
                draftRound = StageSnapshotRules.DraftRoundToSave(resumePhase, cardDraftSystem.CurrentRound),
                rerolls = cardDraftSystem.RerollsRemaining,
                savedAtUtc = DateTime.UtcNow.ToString("o")
            };

            for (int slot = 0; slot < PlayerDeck.MAX_DECK_SIZE; slot++)
            {
                DraftCard card = playerDeck.GetCard(slot);
                run.deck.Add(card == null || card.operatorData == null
                    ? new CardSnapshot()
                    : new CardSnapshot
                    {
                        operatorName = card.operatorData.operatorName,
                        rarity = (int)card.rarity,
                        cooldown = card.cooldownRoundsRemaining,
                        heat = card.heatStacks,
                        savedHP = card.savedHP
                    });
            }

            foreach (OperatorBase op in operatorManager.DeployedOperators)
            {
                if (op == null || !op.IsDeployed || op.DeployedCell == null || op.Data == null) continue;
                Vector2Int cell = op.DeployedCell.GridPosition;
                run.deployed.Add(new DeployedSnapshot
                {
                    operatorName = op.Data.operatorName,
                    rarity = (int)op.CurrentRarity,
                    x = cell.x,
                    y = cell.y,
                    facing = (int)op.Facing,
                    hp = op.CurrentHP,
                    heat = op.HeatStacks
                });
            }
            return run;
        }

        /// <summary>Puts the draft counters back. Called before the stage starts, so the first draft is not taken for the opening one.</summary>
        private void RestoreDraftState(StageSnapshot run)
        {
            if (run != null && cardDraftSystem != null) cardDraftSystem.RestoreState(run.draftRound, run.rerolls);
        }

        /// <summary>Puts the deck and the operators back. Called right after the stage started in the saved phase.</summary>
        private void RestoreDeckAndOperators(StageSnapshot run)
        {
            if (run == null || playerDeck == null) return;

            var slots = new List<DraftCard>();
            foreach (CardSnapshot saved in run.deck)
            {
                OperatorData data = saved == null || saved.IsEmpty ? null : FindOperatorData(saved.operatorName);
                slots.Add(data == null ? null : new DraftCard(data, (OperatorRarity)saved.rarity, saved.cooldown, saved.heat, saved.savedHP));
            }
            playerDeck.RestoreSlots(slots);

            if (operatorManager == null) return;
            foreach (DeployedSnapshot saved in run.deployed)
            {
                OperatorData data = saved == null ? null : FindOperatorData(saved.operatorName);
                if (data == null)
                {
                    Debug.LogWarning("Continue: operator '" + (saved != null ? saved.operatorName : "?") + "' was not found; skipped.");
                    continue;
                }

                var card = new DraftCard(data, (OperatorRarity)saved.rarity, 0, saved.heat, saved.hp);
                if (!operatorManager.TryDeployOperator(card, new Vector2Int(saved.x, saved.y), (OperatorFacing)saved.facing, out _))
                    Debug.LogWarning("Continue: could not place " + data.operatorName + " at (" + saved.x + ", " + saved.y + ").");
            }

            // They stood there before: no free retreat as if they had just been placed
            operatorManager.MarkAllDeployedOperatorsAsFought();
        }

        private OperatorData FindOperatorData(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName)) return null;

            if (operatorPool != null)
            {
                foreach (OperatorData data in operatorPool)
                {
                    if (data != null && data.operatorName == operatorName) return data;
                }
            }

            foreach (OperatorData data in Resources.FindObjectsOfTypeAll<OperatorData>())
            {
                if (data != null && data.operatorName == operatorName) return data;
            }
            return null;
        }
    }
}
