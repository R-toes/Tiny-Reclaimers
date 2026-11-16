using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Systems;

namespace TrashTD.Operators
{
    // Targeting, attacking, bouncing projectiles and attack sounds.
    public partial class ResicochetOperator
    {
        public bool IsWaveActive()
        {
            if (cachedWaveManager == null)
            {
                cachedWaveManager = FindAnyObjectByType<TrashTD.Core.GameLoop.WaveManager>();
            }

            if (cachedWaveManager != null)
            {
                return cachedWaveManager.IsWaveInProgress;
            }

            if (TrashTD.Core.GameLoop.GameManager.Instance != null)
            {
                return TrashTD.Core.GameLoop.GameManager.Instance.CurrentPhase == TrashTD.Core.GameLoop.StagePhase.WaveActive;
            }

            return true;
        }

        protected override void Update()
        {
            if (!isDeployed) return;

            // Charge accumulation: 1 charge every 5 seconds (or 3 seconds when 1x upgraded), up to 3 charges.
            // Never charges during preparation or draft phase; only during active wave combat.
            bool isWaveActive = IsWaveActive();
            if (isWaveActive && !isStunned && !isBound && charges < MaxChargeCount)
            {
                chargeTimer += Time.deltaTime;
                if (chargeTimer >= CurrentChargeInterval)
                {
                    AddCharge();
                }
            }
            else if (!isWaveActive)
            {
                chargeTimer = 0f;
            }

            // If firing a burst, hold the attack timer at 0
            if (isFiringBurst)
            {
                attackTimer = 0f;
            }

            base.Update();

            UpdateCrystalAuras();
        }

        /// <summary>
        /// Gets the extension range cells granted by allied operators deployed inside Resicochet's actual range.
        /// Only cells strictly outside Resicochet's actual range are returned.
        /// </summary>
        public List<GridCell> GetExtensionRangeCells(GridManager gridManager, IEnumerable<GridCell> actualRangeCells = null)
        {
            var extensionCells = new List<GridCell>();
            if (gridManager == null || deployedCell == null || !HasSecondUpgrade) return extensionCells;

            HashSet<GridCell> actualSet;
            if (actualRangeCells != null)
            {
                actualSet = new HashSet<GridCell>(actualRangeCells);
            }
            else
            {
                var actualArray = gridManager.GetCellsInRange(deployedCell.GridPosition, GetRangePattern(), Facing);
                actualSet = actualArray != null ? new HashSet<GridCell>(actualArray) : new HashSet<GridCell>();
            }

            // Find all allied operators deployed inside Resicochet's actual range
            List<OperatorBase> alliesInRange = null;
            if (OperatorManager.Instance != null)
            {
                var cellList = new List<GridCell>(actualSet);
                alliesInRange = OperatorManager.Instance.GetOperatorsInCells(cellList);
            }
            else
            {
                var allOps = FindObjectsByType<OperatorBase>();
                alliesInRange = new List<OperatorBase>();
                for (int i = 0; i < allOps.Length; i++)
                {
                    var op = allOps[i];
                    if (op != null && op != this && op.IsDeployed && op.DeployedCell != null && actualSet.Contains(op.DeployedCell))
                    {
                        alliesInRange.Add(op);
                    }
                }
            }

            if (alliesInRange == null || alliesInRange.Count == 0) return extensionCells;

            var visited = new HashSet<GridCell>();

            for (int i = 0; i < alliesInRange.Count; i++)
            {
                var ally = alliesInRange[i];
                if (ally == null || ally == this || !ally.IsDeployed || ally.DeployedCell == null) continue;

                var allyPattern = ally.GetRangePattern();
                if (allyPattern == null || allyPattern.Length == 0) continue;

                var allyCells = gridManager.GetCellsInRange(ally.DeployedCell.GridPosition, allyPattern, ally.Facing);
                if (allyCells == null) continue;

                for (int j = 0; j < allyCells.Length; j++)
                {
                    var cell = allyCells[j];
                    if (cell == null) continue;

                    // Anything outside of Resicochet's actual range is the extension
                    if (!actualSet.Contains(cell) && visited.Add(cell))
                    {
                        extensionCells.Add(cell);
                    }
                }
            }

            return extensionCells;
        }

        protected override EnemyBase FindTarget()
        {
            return FindTargetInternal(null);
        }

        public override EnemyBase FindAdditionalTarget(EnemyBase excludeTarget)
        {
            return FindTargetInternal(excludeTarget);
        }

        private EnemyBase FindTargetInternal(EnemyBase excludeTarget)
        {
            if (EnemyManager.Instance == null || deployedCell == null || data == null || data.rangePattern == null)
                return null;

            var gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null) return null;

            var actualRangeCells = gridManager.GetCellsInRange(deployedCell.GridPosition, GetRangePattern(), Facing);
            if (actualRangeCells == null || actualRangeCells.Length == 0) return null;

            List<GridCell> targetSearchCells;
            // 2x Upgraded: Operators on her range act as range extension, but doesn't attack into extension range if not fully charged (charges == 3)
            if (HasSecondUpgrade && charges >= MaxChargeCount)
            {
                var extensionCells = GetExtensionRangeCells(gridManager, actualRangeCells);
                if (extensionCells != null && extensionCells.Count > 0)
                {
                    targetSearchCells = new List<GridCell>(actualRangeCells.Length + extensionCells.Count);
                    targetSearchCells.AddRange(actualRangeCells);
                    targetSearchCells.AddRange(extensionCells);
                }
                else
                {
                    targetSearchCells = new List<GridCell>(actualRangeCells);
                }
            }
            else
            {
                // Not fully charged: can only attack within Resicochet's actual range
                targetSearchCells = new List<GridCell>(actualRangeCells);
            }

            var candidates = EnemyManager.Instance.GetEnemiesInCells(targetSearchCells, data.position);
            if (candidates == null || candidates.Count == 0) return null;

            EnemyBase bestTarget = null;
            int highestDefense = -1;
            int largestCluster = -1;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.IsDead || candidate == excludeTarget || !CanAttackTarget(candidate)) continue;

                bool isTaunt = candidate.HasTaunt;
                bool bestIsTaunt = bestTarget != null && bestTarget.HasTaunt;
                int defense = candidate.CurrentDEF;
                int count = -1;
                bool better;

                if (bestTarget == null || (isTaunt && !bestIsTaunt)) better = true;
                else if (isTaunt != bestIsTaunt) better = false;
                else if (defense > highestDefense) better = true;
                else if (defense == highestDefense)
                {
                    if (largestCluster < 0) largestCluster = CountSplashCluster(bestTarget);
                    count = CountSplashCluster(candidate);
                    better = count > largestCluster;
                }
                else better = false;

                if (better)
                {
                    highestDefense = defense;
                    largestCluster = count;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        public override bool CanAttackTarget(EnemyBase target)
        {
            return base.CanAttackTarget(target);
        }

        /// <summary>
        /// Returns world positions of the floating crystals based on charge count and operator facing:
        /// - 1 charge: 1 crystal (mid)
        /// - 2 charges: 2 crystals (mid, top)
        /// - 3 charges: 3 crystals (mid, top, bot)
        /// </summary>
        public Vector3[] GetCrystalPositions(int chargeCount)
        {
            int count = Mathf.Clamp(chargeCount, 1, 3);
            Vector3[] positions = new Vector3[count];
            float flipFactor = (Facing == OperatorFacing.Right) ? -1f : 1f;

            // Pixel-derived crystal offsets relative to sprite center (32 PPU)
            Vector3 c1 = transform.position + new Vector3(-0.39f * flipFactor, 0.09f, 0f);
            Vector3 c2 = transform.position + new Vector3(-0.33f * flipFactor, 0.31f, 0f);
            Vector3 c3 = transform.position + new Vector3(-0.11f * flipFactor, 0.38f, 0f);

            if (count == 1)
            {
                positions[0] = c1;
            }
            else if (count == 2)
            {
                positions[0] = c1;
                positions[1] = c2;
            }
            else
            {
                positions[0] = c1;
                positions[1] = c2;
                positions[2] = c3;
            }
            return positions;
        }

        public override void Attack(EnemyBase target)
        {
            if (target == null || !isDeployed || isStunned || isBound || isFiringBurst) return;

            HasFoughtInWave = true;
            DeployedDuringCurrentPreparation = false;

            int chargeLevel = charges;
            float chargeMultiplier = GetChargeDamageMultiplier(chargeLevel);
            Color projectileColor = GetProjectileColorForCharges(chargeLevel);

            // Consume extra charges back to default 1
            charges = 1;
            chargeTimer = 0f;
            UpdateChargeVisuals();

            PlayResicochetAttackSound(0);
            FireChargeAttack(target, chargeLevel, chargeMultiplier, projectileColor);
        }

        protected override void FireAttackProjectile(EnemyBase target, Vector3 startOffset = default)
        {
            PlayResicochetAttackSound(0);
            FireChargeAttack(target, charges, GetChargeDamageMultiplier(charges), GetProjectileColorForCharges(charges));
        }

        public void FireChargeAttack(EnemyBase target, int chargeLevel, float chargeMultiplier, Color projectileColor)
        {
            if (target == null) return;

            Vector3[] crystals = GetCrystalPositions(chargeLevel);
            Vector3 targetPos = target.VisualPosition;

            // Flashy laser/beam lines from crystals to target
            ResicochetAttackBeamVisual.PlayFromCrystals(crystals, targetPos, projectileColor);

            // Fire projectiles along each crystal beam line
            for (int i = 0; i < crystals.Length; i++)
            {
                Vector3 crystalPos = crystals[i];
                bool isPrimary = (i == 0);

                CombatProjectileVisual.Fire(
                    crystalPos,
                    target,
                    projectileColor,
                    14f,
                    0.24f,
                    0.12f,
                    true,
                    isPrimary ? () => HandleHitAndBounce(target, target, 0, new HashSet<EnemyBase> { target }, chargeLevel, chargeMultiplier, projectileColor) : null,
                    ProjectileStyle.ArtsOrb);
            }
        }

        public void FireSingleBouncingProjectile(EnemyBase target, float chargeMultiplier = 1.0f, Color? projectileColor = null)
        {
            Color color = projectileColor ?? GetProjectileColorForCharges(1);
            PlayResicochetAttackSound(0);
            FireChargeAttack(target, 1, chargeMultiplier, color);
        }

        /// <summary>
        /// Calculates the volume multiplier for ricochet bounces.
        /// Volume becomes progressively quieter per bounce (e.g. 1.0x -> 0.50x -> 0.25x -> 0.125x -> 0.0625x).
        /// </summary>
        public static float GetBounceVolumeMultiplier(int bounceIndex)
        {
            if (bounceIndex <= 0) return 1.0f;
            return Mathf.Clamp(Mathf.Pow(0.5f, bounceIndex), 0.05f, 1.0f);
        }

        private static readonly string[] ResicochetChargeNames = new[]
        {
            "Charge 1", "Charge 2", "Charge 3",
            "Charge1", "Charge2", "Charge3",
            "Charge"
        };

        /// <summary>
        /// Plays attack sound. Randomizes among available Attack files (Attack, Attack_2, Attack_3...)
        /// or inspector attackSounds. When bouncing, replays the exact same attack sound progressively quieter per bounce.
        /// Fades out the previous bounce voice so sounds never stack into an overwhelming volume spike.
        /// </summary>
        public void PlayResicochetAttackSound(int bounceIndex = 0)
        {
            float volume = GetBounceVolumeMultiplier(bounceIndex);

            // If bouncing (bounceIndex > 0), replay the chosen clip or fallback quieter
            if (bounceIndex > 0)
            {
                StopPreviousVolleyVoice();

                // If somehow currentVolleyAttackClip was not captured, try to pick one from folder
                if (currentVolleyAttackClip == null && data != null)
                {
                    currentVolleyAttackClip = UnitSfx.GetRandomClip(UnitSfx.GetFolder(data), nameof(UnitSfxEvent.Attack));
                }

                if (currentVolleyAttackClip != null)
                {
                    currentVolleyVoice = AudioManager.Instance?.PlaySfx(currentVolleyAttackClip, volume, 1.0f);
                    currentVolleyVoiceClip = currentVolleyAttackClip;
                    return;
                }

                var fbVoice = AudioManager.Instance?.PlaySfx(SfxId.GameplayMagicAttack, volume, 1.0f);
                currentVolleyVoice = fbVoice;
                currentVolleyVoiceClip = AudioManager.Instance?.GetClip(SfxId.GameplayMagicAttack);
                return;
            }

            // --- Initial Attack (bounceIndex == 0) ---
            StopPreviousVolleyVoice();

            // 1. Inspector attackSounds array (randomized)
            if (attackSounds != null && attackSounds.Length > 0)
            {
                AudioClip clip = attackSounds[Random.Range(0, attackSounds.Length)];
                if (clip != null)
                {
                    currentVolleyAttackClip = clip;
                    currentVolleyVoiceClip = clip;
                    var voice = AudioManager.Instance?.PlaySfx(clip, volume, 1.0f);
                    currentVolleyVoice = voice;
                    TrackAttackSound(voice);
                    return;
                }
            }

            // 2. Single inspector attackSound slot
            if (attackSound != null)
            {
                currentVolleyAttackClip = attackSound;
                currentVolleyVoiceClip = attackSound;
                var voice = AudioManager.Instance?.PlaySfx(attackSound, volume, 1.0f);
                currentVolleyVoice = voice;
                TrackAttackSound(voice);
                return;
            }

            // 3. Conventional resource lookup (SFX/Operators/Caster/Resicochet/Attack, Attack_2, Attack_3...)
            if (data != null && UnitSfx.Play(data, UnitSfxEvent.Attack, out AudioSource audioVoice, out AudioClip audioClip, volume, ignoreThrottle: false))
            {
                currentVolleyAttackClip = audioClip;
                currentVolleyVoiceClip = audioClip;
                currentVolleyVoice = audioVoice;
                TrackAttackSound(audioVoice);
                return;
            }

            // Also check Charge 1 / Charge 2 / Charge 3 files (randomized among them) if user placed Charge files
            if (UnitSfx.PlayRandomFromFolder("SFX/Operators/Caster/Resicochet", ResicochetChargeNames, out AudioSource chargeVoice, out AudioClip chargeClip, volume, ignoreThrottle: false))
            {
                currentVolleyAttackClip = chargeClip;
                currentVolleyVoiceClip = chargeClip;
                currentVolleyVoice = chargeVoice;
                TrackAttackSound(chargeVoice);
                return;
            }

            // 4. Shared Arts attack sound fallback
            var fallbackVoice = AudioManager.Instance?.PlaySfx(SfxId.GameplayMagicAttack, volume, 1.0f);
            currentVolleyVoice = fallbackVoice;
            currentVolleyVoiceClip = AudioManager.Instance?.GetClip(SfxId.GameplayMagicAttack);
            TrackAttackSound(fallbackVoice);
        }

        public void PlayChargeAttackSound(int chargeLevel, int bounceIndex = 0)
        {
            PlayResicochetAttackSound(bounceIndex);
        }

        /// <summary>
        /// When bouncing, plays the attack sound again, progressively quieter per bounce.
        /// Fades out the previous bounce voice so sounds never stack into an overwhelming volume spike.
        /// </summary>
        public void PlayBounceSound(int bounceIndex = 0, int chargeLevel = 1)
        {
            float vol = GetBounceVolumeMultiplier(bounceIndex + 1);

            if (bounceSounds != null && bounceSounds.Length > 0)
            {
                AudioClip clip = bounceSounds[Random.Range(0, bounceSounds.Length)];
                if (clip != null)
                {
                    StopPreviousVolleyVoice();
                    currentVolleyVoice = AudioManager.Instance?.PlaySfx(clip, vol, 1f);
                    currentVolleyVoiceClip = clip;
                    return;
                }
            }

            if (bounceSound != null)
            {
                StopPreviousVolleyVoice();
                currentVolleyVoice = AudioManager.Instance?.PlaySfx(bounceSound, vol, 1f);
                currentVolleyVoiceClip = bounceSound;
                return;
            }

            // Replays the attack sound, but quieter with each bounce
            PlayResicochetAttackSound(bounceIndex + 1);
        }

        public void HandleHitAndBounce(EnemyBase primaryTarget, EnemyBase currentTarget, int bounceIndex, HashSet<EnemyBase> hitEnemies, float chargeMultiplier = 1.0f, Color? projectileColor = null)
        {
            HandleHitAndBounce(primaryTarget, currentTarget, bounceIndex, hitEnemies, 1, chargeMultiplier, projectileColor);
        }

        /// <summary>
        /// Handles projectile hit and bouncing:
        /// - Deals Arts damage with 25% reduction per bounce.
        /// - Scales with charge multiplier (1 charge: 1.0x, 2 charges: 1.10x, 3 charges: 1.25x).
        /// - Applies Sluggish effect to enemies hit.
        /// - Tints damaged enemies to the highest charge color dealt to them.
        /// - Shows 1, 2, or 3 flashy parallel bounce lines to each bounce target.
        /// - Bounces up to 4 targets total (initial hit + 3 bounces).
        /// </summary>
        public void HandleHitAndBounce(
            EnemyBase primaryTarget,
            EnemyBase currentTarget,
            int bounceIndex,
            HashSet<EnemyBase> hitEnemies,
            int chargeLevel,
            float chargeMultiplier = 1.0f,
            Color? projectileColor = null)
        {
            Color color = projectileColor ?? GetProjectileColorForCharges(chargeLevel);

            if (currentTarget != null && !currentTarget.IsDead)
            {
                float bounceMultiplier = GetBounceDamageMultiplier(bounceIndex);
                int rawAtk = Mathf.RoundToInt(CurrentATK * chargeMultiplier * bounceMultiplier);
                int damage = DamageCalculator.CalculateDamage(rawAtk, currentTarget.CurrentRES);
                currentTarget.TryTakeAttackDamage(damage, DamageType.Arts, this);
                currentTarget.ApplySluggish(sluggishDuration, sluggishSpeedMultiplier, sluggishAttackIntervalMultiplier);
                currentTarget.ApplyResicochetHit(chargeLevel);
                PlayImpactSound();
            }

            // If we have not reached max targets, search for next bounce target
            if (bounceIndex + 1 < CurrentMaxBounceTargets)
            {
                EnemyBase nextTarget = FindNextBounceTarget(primaryTarget, currentTarget, hitEnemies);
                if (nextTarget != null)
                {
                    hitEnemies.Add(nextTarget);
                    Vector3 bounceStart = currentTarget != null ? currentTarget.VisualPosition : transform.position;
                    Vector3 bounceEnd = nextTarget.VisualPosition;

                    // Play flashy parallel bounce lines matching charge
                    ResicochetAttackBeamVisual.PlayParallel(bounceStart, bounceEnd, chargeLevel, color);

                    // Replay the charge attack sound, but quieter and quieter per bounce
                    PlayBounceSound(bounceIndex, chargeLevel);

                    CombatProjectileVisual.Fire(
                        bounceStart,
                        nextTarget,
                        color,
                        15f,
                        0.2f,
                        0.1f,
                        true,
                        () => HandleHitAndBounce(primaryTarget, nextTarget, bounceIndex + 1, hitEnemies, chargeLevel, chargeMultiplier, color),
                        ProjectileStyle.ArtsOrb);
                }
            }
        }

        /// <summary>
        /// Finds an enemy on the surrounding tiles of the primary target or current target.
        /// Excludes enemies already hit in this bounce chain.
        /// </summary>
        public EnemyBase FindNextBounceTarget(EnemyBase primaryTarget, EnemyBase currentTarget, HashSet<EnemyBase> hitEnemies)
        {
            // First check surrounding tiles of primary target
            var candidates = GetEnemiesInSurroundingTiles(primaryTarget, hitEnemies);
            if (candidates.Count == 0 && currentTarget != null && currentTarget != primaryTarget)
            {
                // Fallback to surrounding tiles of current hit target
                candidates = GetEnemiesInSurroundingTiles(currentTarget, hitEnemies);
            }

            if (candidates.Count == 0) return null;

            Vector3 fromPos = currentTarget != null ? currentTarget.transform.position : transform.position;
            EnemyBase best = null;
            float closestDist = float.PositiveInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                var enemy = candidates[i];
                if (enemy == null || enemy.IsDead) continue;

                float dist = Vector3.SqrMagnitude(enemy.transform.position - fromPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    best = enemy;
                }
            }

            return best;
        }

        private List<EnemyBase> GetEnemiesInSurroundingTiles(EnemyBase center, HashSet<EnemyBase> exclude)
        {
            var result = new List<EnemyBase>();
            if (center == null || EnemyManager.Instance == null) return result;

            var grid = FindAnyObjectByType<GridManager>();
            if (grid != null)
            {
                Vector2Int centerGrid = grid.WorldToGridPosition(center.transform.position);

                var cells = new List<GridCell>(9);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        GridCell cell = grid.GetCell(centerGrid.x + dx, centerGrid.y + dy);
                        if (cell != null) cells.Add(cell);
                    }
                }

                var inCells = EnemyManager.Instance.GetEnemiesInCells(cells, data != null ? data.position : OperatorPosition.Ranged);
                if (inCells != null)
                {
                    for (int i = 0; i < inCells.Count; i++)
                    {
                        var e = inCells[i];
                        if (e != null && !e.IsDead && (exclude == null || !exclude.Contains(e)) && CanAttackTarget(e))
                        {
                            result.Add(e);
                        }
                    }
                }
            }
            else
            {
                // Fallback for tests or scenes without GridManager (1.6 world units radius)
                var inRadius = EnemyManager.Instance.GetEnemiesInRadius(center.transform.position, 1.6f, data != null ? data.position : OperatorPosition.Ranged);
                if (inRadius != null)
                {
                    for (int i = 0; i < inRadius.Count; i++)
                    {
                        var e = inRadius[i];
                        if (e != null && !e.IsDead && (exclude == null || !exclude.Contains(e)) && CanAttackTarget(e))
                        {
                            result.Add(e);
                        }
                    }
                }
            }

            return result;
        }

        protected override void OnRetreated()
        {
            StopPreviousVolleyVoice();
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
                burstCoroutine = null;
            }
            isFiringBurst = false;
            charges = 1;
            chargeTimer = 0f;
            SetCrystalAuraEnabled(false);
            base.OnRetreated();
        }
    }
}
