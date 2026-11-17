using System.Collections.Generic;
using UnityEngine;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Systems;

namespace TrashTD.Operators
{
    /// <summary>
    /// Castiel — 2-Star Caster.
    /// Light-magic caster that locks a continuous laser onto one target and ramps its damage
    /// the longer the target stays in range.
    /// Upgrades:
    /// - Base (2★): ticks every 0.375 s; damage steps up every 1.5 s: 5, 15, 30, 80, 100, 150.
    /// - 1x Upgraded (3★): ticks every 0.3 s, steps up every 1.2 s, and every step deals 20% more: 6, 18, 36, 96, 120, 180.
    /// - 2x Upgraded (4★): a second laser locks onto another enemy in range and ramps on its own.
    /// The laser obeys what every other attack obeys: a stun or a bind breaks it (the ramp starts over),
    /// and anything that changes the attack interval (the sandstorm) changes how often it ticks.
    /// </summary>
    public sealed class CastielOperator : CasterOperator
    {
        public const float BaseTickInterval = 0.375f;
        public const float UpgradedTickInterval = 0.3f;
        public const float BaseLockStepDuration = 1.5f;
        public const float UpgradedLockStepDuration = 1.2f;
        public const int UpgradedLaserCount = 2;

        private static readonly int[] BeamDamageByStage = { 5, 15, 30, 80, 100, 150 };
        private static readonly int[] UpgradedBeamDamageByStage = { 6, 18, 36, 96, 120, 180 };

        /// <summary>One laser: its target, how long it has been locked on, and its visual.</summary>
        private sealed class Beam
        {
            public EnemyBase Target;
            public float LockDuration;
            public LightLaserVisual Laser;
        }

        private Beam primaryBeam = new Beam();
        private Beam secondaryBeam = new Beam();
        private OperatorSpriteAnimation spriteAnim;

        public bool HasFirstUpgrade => currentRarity >= OperatorRarity.Star3;
        public bool HasSecondUpgrade => currentRarity >= OperatorRarity.Star4;

        public float TickInterval => GetTickInterval(HasFirstUpgrade);
        public float LockStepDuration => GetLockStepDuration(HasFirstUpgrade);
        public int LaserCount => HasSecondUpgrade ? UpgradedLaserCount : 1;

        public EnemyBase LockedTarget => primaryBeam.Target;
        public EnemyBase SecondLockedTarget => secondaryBeam.Target;

        public void SetRarityForTesting(OperatorRarity rarity)
        {
            currentRarity = rarity;
        }

        public static float GetTickInterval(bool firstUpgrade) => firstUpgrade ? UpgradedTickInterval : BaseTickInterval;

        public static float GetLockStepDuration(bool firstUpgrade) => firstUpgrade ? UpgradedLockStepDuration : BaseLockStepDuration;

        /// <summary>Ramp stage 0-5: one step for every lock step duration the beam has stayed on its target.</summary>
        public static int GetBeamStage(float lockDuration, bool firstUpgrade) =>
            Mathf.Clamp(Mathf.FloorToInt(lockDuration / GetLockStepDuration(firstUpgrade)), 0, BeamDamageByStage.Length - 1);

        /// <summary>Damage of one tick at a ramp stage (0-5).</summary>
        public static int GetBeamDamageForStage(int stage, bool firstUpgrade)
        {
            int[] table = firstUpgrade ? UpgradedBeamDamageByStage : BeamDamageByStage;
            return table[Mathf.Clamp(stage, 0, table.Length - 1)];
        }

        public override void Initialize(OperatorData operatorData, OperatorRarity rarity, OperatorFacing operatorFacing = OperatorFacing.Right)
        {
            base.Initialize(operatorData, rarity, operatorFacing);
            spriteAnim = GetComponent<OperatorSpriteAnimation>();
            ResetLock();
        }

        /// <summary>
        /// The time between laser ticks, and how often Castiel re-targets: the rank's tick interval with the
        /// stage and buff modifiers every attack interval gets (the sandstorm changes it).
        /// </summary>
        public override float GetAttackInterval() =>
            StageCombatModifiers.GetAttackInterval(TickInterval) / HarmonyAttackSpeedMultiplier;

        /// <summary>Whether the lasers may run: a stunned or bound Castiel cannot attack, like any operator.</summary>
        public bool CanSustainLasers => isDeployed && !isStunned && !isBound;

        protected override void Update()
        {
            // The base Update stops attacking while stunned or bound, but a laser already running would keep
            // ticking by itself. Break it; the ramp starts over, as it does for Askalaser.
            if (isDeployed && !CanSustainLasers) ResetLock();

            base.Update();
        }

        public override void Attack(EnemyBase target)
        {
            if (target == null || !isDeployed) return;

            if (primaryBeam.Target != target)
            {
                StopBeam(primaryBeam);
                primaryBeam.Target = target;
            }

            // The primary beam plays the attack sound; the second one is silent so the two don't double up.
            if (primaryBeam.Laser == null) PlayAttackSound();
            EnsureLaser(primaryBeam);
            SyncTickInterval(primaryBeam);

            // 2nd upgrade: a second laser on another enemy in range.
            if (HasSecondUpgrade)
            {
                UpdateSecondaryBeam();
            }
            else
            {
                StopBeam(secondaryBeam);
            }
        }

        protected override EnemyBase FindTarget()
        {
            List<EnemyBase> candidates = GetCandidatesInRange();
            if (candidates == null || candidates.Count == 0)
            {
                ResetLock();
                return null;
            }

            bool hasTauntCandidate = HasTauntCandidate(candidates);

            if (IsLockStillValid(primaryBeam.Target, candidates, hasTauntCandidate))
                return primaryBeam.Target;

            // The primary lock is lost. If the second laser is still on a valid target it takes over
            // as the primary and keeps its ramp, instead of starting again from the first step.
            if (HasSecondUpgrade && IsLockStillValid(secondaryBeam.Target, candidates, hasTauntCandidate))
            {
                StopBeam(primaryBeam);
                Beam promoted = secondaryBeam;
                secondaryBeam = primaryBeam;
                primaryBeam = promoted;
                UpdateHaloSpeed();
                return primaryBeam.Target;
            }

            StopBeam(primaryBeam);
            UpdateHaloSpeed();
            return ChooseTarget(candidates, null);
        }

        protected override void OnRetreated()
        {
            ResetLock();
            base.OnRetreated();
        }

        public override void CollectStatEffects(List<StatEffect> effects)
        {
            base.CollectStatEffects(effects);

            if (HasFirstUpgrade)
            {
                effects.Add(new StatEffect("3★ UPGRADE",
                    $"Tick {BaseTickInterval:0.###}s > {UpgradedTickInterval:0.###}s, ramp {BaseLockStepDuration:0.#}s > {UpgradedLockStepDuration:0.#}s",
                    "ATK SPD"));
                effects.Add(new StatEffect("3★ UPGRADE", "Beam damage +20%", "ATK"));
            }

            if (HasSecondUpgrade)
                effects.Add(new StatEffect("4★ UPGRADE", "Second laser on another enemy", "ATK"));
        }

        // ---------- Targeting ----------

        private List<EnemyBase> GetCandidatesInRange()
        {
            if (EnemyManager.Instance == null || deployedCell == null || data == null || data.rangePattern == null)
                return null;

            GridManager gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null) return null;

            var rangeCells = gridManager.GetCellsInRange(deployedCell.GridPosition, GetRangePattern(), Facing);
            return EnemyManager.Instance.GetEnemiesInCells(rangeCells, data.position);
        }

        private bool HasTauntCandidate(List<EnemyBase> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyBase candidate = candidates[i];
                if (candidate != null && !candidate.IsDead && candidate.HasTaunt && CanAttackTarget(candidate))
                    return true;
            }

            return false;
        }

        /// <summary>A lock holds while its target is alive and in range, unless a taunting enemy must be attacked instead.</summary>
        private static bool IsLockStillValid(EnemyBase target, List<EnemyBase> candidates, bool hasTauntCandidate) =>
            target != null && !target.IsDead && candidates.Contains(target) && (!hasTauntCandidate || target.HasTaunt);

        /// <summary>Taunting enemies first, then the enemy closest to the goal. <paramref name="exclude"/> is skipped.</summary>
        private EnemyBase ChooseTarget(List<EnemyBase> candidates, EnemyBase exclude)
        {
            EnemyBase bestTarget = null;
            float closestToGoal = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyBase candidate = candidates[i];
                if (candidate == null || candidate == exclude || candidate.IsDead || !CanAttackTarget(candidate)) continue;
                bool isTaunt = candidate.HasTaunt;
                bool bestIsTaunt = bestTarget != null && bestTarget.HasTaunt;
                if (bestTarget != null && !isTaunt && bestIsTaunt) continue;
                if (bestTarget != null && isTaunt == bestIsTaunt && candidate.DistanceToGoal >= closestToGoal) continue;
                bestTarget = candidate;
                closestToGoal = candidate.DistanceToGoal;
            }

            return bestTarget;
        }

        /// <summary>
        /// Keeps the second laser on its target while that target stays alive and in range; otherwise moves it
        /// to another enemy. It never shares the primary's target, so with one enemy in range it stays off.
        /// </summary>
        private void UpdateSecondaryBeam()
        {
            List<EnemyBase> candidates = GetCandidatesInRange();
            if (candidates == null)
            {
                StopBeam(secondaryBeam);
                return;
            }

            EnemyBase current = secondaryBeam.Target;
            bool keep = current != null && current != primaryBeam.Target && !current.IsDead &&
                        candidates.Contains(current) && CanAttackTarget(current);
            if (!keep)
            {
                StopBeam(secondaryBeam);
                secondaryBeam.Target = ChooseTarget(candidates, primaryBeam.Target);
            }

            if (secondaryBeam.Target != null) EnsureLaser(secondaryBeam);
            SyncTickInterval(secondaryBeam);
        }

        // ---------- Beams ----------

        private void EnsureLaser(Beam beam)
        {
            if (beam.Laser != null || beam.Target == null) return;

            beam.Laser = LightLaserVisual.Fire(
                transform,
                beam.Target,
                LaserPalette.Holy,
                GetAttackInterval(),
                () => ApplyBeamTick(beam));
        }

        /// <summary>Keeps a running laser's tick rate in step with the attack interval (the sandstorm comes and goes).</summary>
        private void SyncTickInterval(Beam beam)
        {
            if (beam.Laser != null) beam.Laser.SetTickInterval(GetAttackInterval());
        }

        private void ApplyBeamTick(Beam beam)
        {
            if (beam.Target == null || beam.Target.IsDead || !CanSustainLasers) return;

            bool upgraded = HasFirstUpgrade;
            int damage = GetBeamDamageForStage(GetBeamStage(beam.LockDuration, upgraded), upgraded);
            beam.Target.TryTakeAttackDamage(damage, DamageType.Arts, this);
            beam.LockDuration += GetAttackInterval(); // real time between ticks, so the ramp keeps its pace in seconds

            if (beam.Laser != null)
            {
                beam.Laser.OnDamageTick();
                beam.Laser.SetStage(GetBeamStage(beam.LockDuration, upgraded)); // The beam shows the power of the next tick
            }

            if (beam == primaryBeam) UpdateHaloSpeed();
        }

        /// <summary>The halo spins faster as the primary beam ramps up (frame 0.10 s down to 0.04 s).</summary>
        private void UpdateHaloSpeed()
        {
            if (spriteAnim == null) return;

            float t = Mathf.Clamp01(primaryBeam.LockDuration / (LockStepDuration * 5f));
            spriteAnim.FrameDuration = Mathf.Lerp(0.10f, 0.04f, t);
        }

        private static void StopBeam(Beam beam)
        {
            if (beam.Laser != null)
            {
                beam.Laser.Stop();
                beam.Laser = null;
            }

            beam.Target = null;
            beam.LockDuration = 0f;
        }

        private void ResetLock()
        {
            StopBeam(primaryBeam);
            StopBeam(secondaryBeam);
            UpdateHaloSpeed();
        }
    }
}
