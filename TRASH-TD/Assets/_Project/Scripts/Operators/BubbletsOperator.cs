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
    /// Bubblets — 3-Star Medic.
    /// Range covers self and 8 surrounding tiles (3x3 area).
    /// Does not heal directly; every 2.0 seconds, grants allied operator(s) in range
    /// a protective bubble shield that negates one instance of damage (even at full HP).
    /// When the bubble pops, it heals the recipient for 100% of Bubblets' ATK and deals Arts damage
    /// to enemies on that tile and surrounding tiles.
    /// Upgrades:
    /// - Base (3★): Deals Arts damage equal to 30% of Bubblets' ATK when popped.
    /// - 1x Upgraded (4★): Arts damage increases to 50% of Bubblets' ATK, and allies in range gain +10 RES and +40 DEF.
    /// - 2x Upgraded (5★): Gives bubble shield to an additional operator (up to 2), and Bubblets can now attack enemies like a caster.
    /// </summary>
    public class BubbletsOperator : MedicOperator
    {
        public const int AuraDefBonus = 40;
        public const string AuraSourceName = "BUBBLETS";
        public const int AuraResBonus = 10;
        public static readonly Color BubbleProjectileColor = new Color(0.45f, 0.85f, 1f, 0.95f);

        /// <summary>
        /// Local offsets for Bubblets' 3 mouths (relative to 32x32 sprite center, in unflipped/left-facing coordinates):
        /// - [0] Lower-left head (front mouth)
        /// - [1] Lower-right head (back mouth)
        /// - [2] Top head (upper mouth)
        /// </summary>
        public static readonly Vector2[] MouthOffsets = new Vector2[]
        {
            new Vector2(-0.28f, -0.13f), // Lower-left mouth
            new Vector2(0.17f, -0.06f),  // Lower-right mouth
            new Vector2(-0.06f, 0.12f)   // Top mouth
        };

        public bool HasFirstUpgrade => currentRarity >= OperatorRarity.Star4;
        public bool HasSecondUpgrade => currentRarity >= OperatorRarity.Star5;

        public float ArtsDamageMultiplier => HasFirstUpgrade ? 0.50f : 0.30f;
        public int TargetCount => HasSecondUpgrade ? 2 : 1;

        /// <summary>
        /// Where Bubblets' shots and shields leave its body: randomly chosen among the 3 mouths.
        /// </summary>
        public override Vector3 ProjectileOrigin => GetRandomMouthOrigin();

        /// <summary>
        /// Gets the world-space launch origin for a specific mouth index (0, 1, or 2), respecting facing direction.
        /// </summary>
        public Vector3 GetMouthOrigin(int mouthIndex)
        {
            if (MouthOffsets == null || MouthOffsets.Length == 0)
                return transform.position;

            int idx = Mathf.Clamp(mouthIndex, 0, MouthOffsets.Length - 1);
            Vector2 offset = MouthOffsets[idx];
            if (facing == OperatorFacing.Right) offset.x = -offset.x;
            Vector3 scale = transform.lossyScale;
            return transform.position + new Vector3(offset.x * scale.x, offset.y * scale.y, 0f);
        }

        /// <summary>
        /// Returns a randomly chosen mouth launch origin among Bubblets' 3 heads.
        /// </summary>
        public Vector3 GetRandomMouthOrigin()
        {
            int index = UnityEngine.Random.Range(0, MouthOffsets.Length);
            return GetMouthOrigin(index);
        }

        private readonly HashSet<OperatorBase> appliedAuraAllies = new HashSet<OperatorBase>();
        private readonly List<OperatorBase> tempAuraCleanupList = new List<OperatorBase>();

        public IReadOnlyCollection<OperatorBase> AppliedAuraAllies => appliedAuraAllies;

        public void SetRarityForTesting(OperatorRarity rarity)
        {
            currentRarity = rarity;
        }

        protected override EnemyBase FindTarget()
        {
            if (!HasSecondUpgrade)
            {
                return null;
            }

            return FindCasterTargetInternal();
        }

        public override void Attack(EnemyBase target)
        {
            if (target == null || !isDeployed) return;

            PlayAttackSound();
            FireCasterAttack(target);
        }

        private void FireCasterAttack(EnemyBase target)
        {
            if (target == null) return;

            if (!Application.isPlaying)
            {
                ApplyCasterAttackImpact(target);
                return;
            }

            CombatProjectileVisual.Fire(
                ProjectileOrigin,
                target,
                BubbleProjectileColor,
                9.5f,
                0.22f,
                0.14f,
                true,
                () => ApplyCasterAttackImpact(target),
                ProjectileStyle.Bubble);
        }

        public void ApplyCasterAttackImpact(EnemyBase target)
        {
            if (target == null) return;

            PlayShieldPopSound();

            const float armorDamageBonusRatio = 0.5f;

            // Single target Arts damage bypassing DEF and hitting RES, with caster DEF bonus
            if (!target.IsDead)
            {
                int armorBonus = Mathf.RoundToInt(target.CurrentDEF * armorDamageBonusRatio);
                int primaryPower = currentATK + armorBonus;
                int primaryDamage = DamageCalculator.CalculateDamage(primaryPower, target.CurrentRES);
                target.TryTakeAttackDamage(primaryDamage, DamageType.Arts, this);
            }
        }

        private EnemyBase FindCasterTargetInternal()
        {
            if (deployedCell == null || data == null || data.rangePattern == null)
                return GetNearbyEnemiesFallback();

            GridManager gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null && OperatorManager.Instance != null)
            {
                gridManager = OperatorManager.Instance.GridManager;
            }
            if (gridManager == null) return GetNearbyEnemiesFallback();

            if (EnemyManager.Instance == null) return null;

            GridCell[] rangeCells = gridManager.GetCellsInRange(deployedCell.GridPosition, GetRangePattern(), Facing);
            List<EnemyBase> candidates = EnemyManager.Instance.GetEnemiesInCells(rangeCells, data.position);
            if (candidates == null || candidates.Count == 0) return null;

            EnemyBase bestTarget = null;
            int highestDefense = -1;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.IsDead || !CanAttackTarget(candidate)) continue;

                bool isTaunt = candidate.HasTaunt;
                bool bestIsTaunt = bestTarget != null && bestTarget.HasTaunt;
                int defense = candidate.CurrentDEF;
                bool better;

                if (bestTarget == null || (isTaunt && !bestIsTaunt)) better = true;
                else if (isTaunt != bestIsTaunt) better = false;
                else if (defense > highestDefense) better = true;
                else better = false;

                if (better)
                {
                    highestDefense = defense;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private EnemyBase GetNearbyEnemiesFallback()
        {
            if (EnemyManager.Instance == null) return null;
            var active = EnemyManager.Instance.ActiveEnemies;
            if (active == null || active.Count == 0) return null;

            float maxDist = (data != null && data.attackRange > 0) ? data.attackRange + 0.5f : 1.5f;
            EnemyBase best = null;
            int highestDef = -1;

            for (int i = 0; i < active.Count; i++)
            {
                var e = active[i];
                if (e == null || e.IsDead || !CanAttackTarget(e)) continue;

                float dx = Mathf.Abs(e.transform.position.x - transform.position.x);
                float dy = Mathf.Abs(e.transform.position.y - transform.position.y);
                if (dx <= maxDist && dy <= maxDist)
                {
                    if (best == null || (e.HasTaunt && !best.HasTaunt) || e.CurrentDEF > highestDef)
                    {
                        best = e;
                        highestDef = e.CurrentDEF;
                    }
                }
            }

            return best;
        }

        public override void Heal(OperatorBase target)
        {
            // Forward healing requests to shielding
            if (target != null && isDeployed)
            {
                int firstMouth = UnityEngine.Random.Range(0, MouthOffsets.Length);
                Shield(target, firstMouth);
                if (HasSecondUpgrade)
                {
                    var targets = FindShieldTargets(2);
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (targets[i] != target)
                        {
                            Shield(targets[i], (firstMouth + 1) % MouthOffsets.Length);
                            break;
                        }
                    }
                }
            }
        }

        protected override void Update()
        {
            if (!isDeployed) return;

            // Shield cast interval (uses attackInterval as cycle interval, defaulting to 2.0s).
            // Stays ready until a shield target exists, so the first shield is cast immediately.
            float interval = Combat.StageCombatModifiers.GetAttackInterval(data != null ? data.attackInterval : 2.0f);
            attackTimer = Mathf.Min(attackTimer + Time.deltaTime, interval);

            // No bubbles between waves.
            if (IsCombatPaused) return;

            if (isStunned || isBound)
            {
                if (isStunned) UpdateStunStatus(Time.deltaTime);
                if (isBound) UpdateBindStatus(Time.deltaTime);
                return;
            }

            equippedSkill?.UpdateSkill(Time.deltaTime);

            if (HasFirstUpgrade && currentHP > 0)
            {
                UpdateDefResAura();
            }

            if (attackTimer >= interval)
            {
                bool performedAction = false;

                // 1. Shield allies (core Medic ability)
                int count = TargetCount;
                var targets = FindShieldTargets(count);
                if (targets != null && targets.Count > 0)
                {
                    int firstMouth = UnityEngine.Random.Range(0, MouthOffsets.Length);
                    for (int i = 0; i < targets.Count; i++)
                    {
                        int mouth = (firstMouth + i) % MouthOffsets.Length;
                        Shield(targets[i], mouth);
                    }
                    performedAction = true;
                }

                // 2. 2nd Upgrade: Bubblets can now attack enemies like a caster
                if (HasSecondUpgrade)
                {
                    EnemyBase enemyTarget = FindTarget();
                    if (enemyTarget != null && CanAttackTarget(enemyTarget))
                    {
                        Attack(enemyTarget);
                        performedAction = true;
                    }
                }

                if (performedAction)
                {
                    attackTimer = 0f;
                }
            }
        }

        protected override void OnDeployed()
        {
            base.OnDeployed();
            if (HasFirstUpgrade)
            {
                UpdateDefResAura();
            }
        }

        protected override void OnRetreated()
        {
            ClearDefResAura();
            base.OnRetreated();
        }

        private void OnDestroy()
        {
            ClearDefResAura();
        }

        /// <summary>Played by a bubble this Bubblets cast when it pops.</summary>
        public void PlayShieldPopSound() => PlayAbilitySound(Audio.SfxId.GameplayMagicAttack);

        public void Shield(OperatorBase target, int mouthIndex = -1)
        {
            if (target == null || !isDeployed || IsCombatPaused || isStunned || isBound) return;

            PlayAttackSound();

            if (!Application.isPlaying)
            {
                BubbleShield.Apply(target, currentATK, this);
                return;
            }

            Vector3 origin = mouthIndex >= 0 ? GetMouthOrigin(mouthIndex) : GetRandomMouthOrigin();

            CombatProjectileVisual.Fire(
                origin,
                target,
                BubbleProjectileColor,
                6.5f,
                0.22f,
                0.14f,
                true,
                () =>
                {
                    // A bubble still in flight when the wave ends is dropped.
                    if (target != null && target.IsDeployed && !IsCombatPaused)
                    {
                        BubbleShield.Apply(target, currentATK, this);
                    }
                }, style: ProjectileStyle.Bubble);
        }

        /// <summary>
        /// Selects the best unshielded ally in range to grant a bubble shield.
        /// Prioritizes actively blocking allies, then injured allies, then unshielded full HP allies.
        /// </summary>
        public OperatorBase FindShieldTarget()
        {
            var targets = FindShieldTargets(1);
            return targets.Count > 0 ? targets[0] : null;
        }

        /// <summary>
        /// Finds up to maxCount unshielded allies in range, sorted by priority.
        /// </summary>
        public List<OperatorBase> FindShieldTargets(int maxCount = 1)
        {
            var targets = new List<OperatorBase>();
            if (OperatorManager.Instance == null || deployedCell == null || data == null || data.rangePattern == null)
                return targets;

            GridManager gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null && OperatorManager.Instance != null)
            {
                gridManager = OperatorManager.Instance.GridManager;
            }
            if (gridManager == null) return targets;

            GridCell[] rangeCells = gridManager.GetCellsInRange(deployedCell.GridPosition, data.rangePattern, Facing);
            List<OperatorBase> candidates = OperatorManager.Instance.GetOperatorsInCells(rangeCells);
            if (candidates == null || candidates.Count == 0) return targets;

            var scoredCandidates = new List<KeyValuePair<OperatorBase, float>>();

            for (int i = 0; i < candidates.Count; i++)
            {
                OperatorBase ally = candidates[i];
                if (ally == null || !ally.IsDeployed || ally.CurrentHP <= 0 || BubbleShield.HasActiveShield(ally))
                    continue;

                float score = 10f;

                // Priority 1: Actively blocking enemies (taking front-line pressure)
                if (ally.GetCurrentBlockCount() > 0)
                {
                    score += 1000f;
                }

                // Priority 2: Injured allies (lower HP percentage gets higher score)
                if (ally.MaxHP > 0)
                {
                    float missingRatio = 1f - Mathf.Clamp01((float)ally.CurrentHP / ally.MaxHP);
                    score += missingRatio * 100f;
                }

                scoredCandidates.Add(new KeyValuePair<OperatorBase, float>(ally, score));
            }

            scoredCandidates.Sort((a, b) => b.Value.CompareTo(a.Value));

            for (int i = 0; i < scoredCandidates.Count && i < maxCount; i++)
            {
                targets.Add(scoredCandidates[i].Key);
            }

            return targets;
        }

        /// <summary>
        /// Updates the DEF & RES aura (+40 DEF, +10 RES to allies in range, unlocked on 1st upgrade).
        /// </summary>
        public void UpdateDefResAura()
        {
            if (!HasFirstUpgrade || !isDeployed || currentHP <= 0)
            {
                if (appliedAuraAllies.Count > 0)
                {
                    ClearDefResAura();
                }
                return;
            }

            List<OperatorBase> currentInRange = GetOperatorsInRange();

            // 1. Remove buff from operators no longer in range or no longer deployed/alive
            tempAuraCleanupList.Clear();
            foreach (var op in appliedAuraAllies)
            {
                if (op == null || !op.IsDeployed || op.CurrentHP <= 0 || currentInRange == null || !currentInRange.Contains(op))
                {
                    tempAuraCleanupList.Add(op);
                }
            }

            for (int i = 0; i < tempAuraCleanupList.Count; i++)
            {
                OperatorBase op = tempAuraCleanupList[i];
                if (op != null)
                {
                    if (op.IsDeployed && op.CurrentHP > 0)
                    {
                        op.ModifyDEF(-AuraDefBonus, AuraSourceName);
                        op.ModifyRES(-AuraResBonus, AuraSourceName);
                    }
                    appliedAuraAllies.Remove(op);
                }
            }

            // 2. Add buff to newly detected operators in range
            if (currentInRange != null)
            {
                for (int i = 0; i < currentInRange.Count; i++)
                {
                    OperatorBase op = currentInRange[i];
                    if (op != null && op.IsDeployed && op.CurrentHP > 0 && !appliedAuraAllies.Contains(op))
                    {
                        op.ModifyDEF(AuraDefBonus, AuraSourceName);
                        op.ModifyRES(AuraResBonus, AuraSourceName);
                        appliedAuraAllies.Add(op);
                    }
                }
            }
        }

        public void ClearDefResAura()
        {
            foreach (var op in appliedAuraAllies)
            {
                if (op != null && op.IsDeployed && op.CurrentHP > 0)
                {
                    op.ModifyDEF(-AuraDefBonus, AuraSourceName);
                    op.ModifyRES(-AuraResBonus, AuraSourceName);
                }
            }
            appliedAuraAllies.Clear();
        }

        public List<OperatorBase> GetOperatorsInRange()
        {
            GridCell originCell = deployedCell;
            GridManager gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null && OperatorManager.Instance != null)
            {
                gridManager = OperatorManager.Instance.GridManager;
            }

            if (originCell == null && gridManager != null)
            {
                originCell = gridManager.GetCell(gridManager.WorldToGridPosition(transform.position));
            }

            if (OperatorManager.Instance != null && gridManager != null && originCell != null)
            {
                Vector2Int[] pattern = GetRangePattern();
                if (pattern != null)
                {
                    GridCell[] rangeCells = gridManager.GetCellsInRange(originCell.GridPosition, pattern, Facing);
                    return OperatorManager.Instance.GetOperatorsInCells(rangeCells);
                }
            }

            return GetNearbyOperatorsFallback();
        }

        private List<OperatorBase> GetNearbyOperatorsFallback()
        {
            var list = new List<OperatorBase>();
            var allOps = FindObjectsByType<OperatorBase>();
            float maxDist = (data != null && data.attackRange > 0) ? data.attackRange + 0.5f : 1.5f;

            for (int i = 0; i < allOps.Length; i++)
            {
                OperatorBase op = allOps[i];
                if (op != null && op.IsDeployed && op.CurrentHP > 0)
                {
                    float dx = Mathf.Abs(op.transform.position.x - transform.position.x);
                    float dy = Mathf.Abs(op.transform.position.y - transform.position.y);
                    if (dx <= maxDist && dy <= maxDist)
                    {
                        list.Add(op);
                    }
                }
            }

            return list;
        }
    }
}
