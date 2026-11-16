using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Core.Pathfinding;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Enemies
{
    // Per-frame behaviour: status ticking, blocked and ranged attacks, operator targeting and sprite animation.
    public partial class EnemyBase
    {
        // ========================
        // MonoBehaviour Lifecycle
        // ========================

        protected virtual void Update()
        {
            if (isDead || isDevoured) return;

            if (isInsideHole)
            {
                UpdateHoleStay(Time.deltaTime);
                return;
            }

            UpdateSpriteAnimation(Time.deltaTime);
            UpdateTrapDamage(Time.deltaTime);
            if (isDead) return;

            UpdateChillStatus(Time.deltaTime);

            if (isPushingBack)
            {
                UpdatePushback();
            }

            if (isFrozen || isStunned) return;

            if (isBlocked)
            {
                UpdateFacingTowardsBlocker();

                if (Vector3.Distance(transform.position, blockedPosition) > 0.02f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, blockedPosition, currentMoveSpeed * Time.deltaTime);
                }
                else
                {
                    transform.position = blockedPosition;
                }

                // Attack the blocking operator while blocked
                AttackBlocker();
            }
            else
            {
                // The melee attack keeps charging while walking (capped at one interval), so the first
                // hit lands as soon as an operator blocks this enemy instead of a full interval later.
                ChargeMeleeAttack(Time.deltaTime);

                if (!isPushingBack)
                {
                    // Move along path
                    MoveAlongPath();
                }
            }
        }

        /// <summary>
        /// Per-frame upkeep for enemies with their own Update: hole stay, animation, trap damage, chill/stun
        /// timers and pushback. Returns false when the enemy can't act this frame.
        /// </summary>
        protected bool TickStatusEffects(float deltaTime)
        {
            if (isDead || isDevoured) return false;

            if (isInsideHole)
            {
                UpdateHoleStay(deltaTime);
                return false;
            }

            UpdateSpriteAnimation(deltaTime);
            UpdateTrapDamage(deltaTime);
            if (isDead) return false;

            UpdateChillStatus(deltaTime);

            if (isPushingBack)
            {
                UpdatePushback();
            }

            if (isFrozen || isStunned) return false;

            return true;
        }

        /// <summary>
        /// Ensures the blocked enemy always faces the operator blocking it.
        /// Prevents mirroring bugs when fast enemies (like Rushers) overshoot and nudge back into blockedPosition.
        /// </summary>
        protected void UpdateFacingTowardsBlocker()
        {
            if (blockingOperator != null)
            {
                float dirX = blockingOperator.transform.position.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.01f)
                {
                    UpdateFacing(dirX);
                }
            }
        }

        /// <summary>Walks into (and then holds) the stopping point in front of the blocker.</summary>
        protected void HoldBlockedPosition(float deltaTime)
        {
            UpdateFacingTowardsBlocker();

            if (Vector3.Distance(transform.position, blockedPosition) > 0.02f)
            {
                transform.position = Vector3.MoveTowards(transform.position, blockedPosition, currentMoveSpeed * deltaTime);
            }
            else
            {
                transform.position = blockedPosition;
            }
        }

        protected GridManager CurrentGrid => gridManager;

        private void ChargeMeleeAttack(float deltaTime)
        {
            if (data == null) return;

            float attackInterval = GetEffectiveAttackInterval();
            attackTimer = Mathf.Min(attackTimer + deltaTime, attackInterval);
        }

        protected void UpdateRangedBehavior(bool prioritizeRangedOperators)
        {
            if (isDead) return;

            float deltaTime = Time.deltaTime;
            if (isInsideHole)
            {
                UpdateHoleStay(deltaTime);
                return;
            }

            UpdateSpriteAnimation(deltaTime);
            UpdateTrapDamage(deltaTime);
            if (isDead) return;

            UpdateChillStatus(deltaTime);
            if (isFrozen || isStunned) return;

            if (isPushingBack)
            {
                UpdatePushback();
                return;
            }

            if (rangedAttackStopTimer > 0f)
            {
                rangedAttackStopTimer = Mathf.Max(0f, rangedAttackStopTimer - deltaTime);
                return;
            }

            attackTimer += deltaTime;
            float attackInterval = GetEffectiveAttackInterval();

            if (attackTimer >= attackInterval)
            {
                OperatorBase target = FindOperatorTarget(prioritizeRangedOperators);
                if (target != null)
                {
                    attackTimer = 0f;
                    rangedAttackStopTimer = RangedAttackStopDurationSeconds;
                    AttackOperator(target);
                    return;
                }
            }

            // A blocked caster holds in front of its blocker and keeps shooting its usual targets.
            if (isBlocked)
                HoldBlockedPosition(deltaTime);
            else
                MoveAlongPath();
        }

        protected void UpdateFrontalAreaAttackBehavior(int tileRange)
        {
            if (isDead) return;

            float deltaTime = Time.deltaTime;
            if (isInsideHole)
            {
                UpdateHoleStay(deltaTime);
                return;
            }

            UpdateSpriteAnimation(deltaTime);
            UpdateTrapDamage(deltaTime);
            if (isDead) return;

            UpdateChillStatus(deltaTime);
            if (isFrozen || isStunned) return;

            if (isPushingBack)
            {
                UpdatePushback();
                return;
            }

            attackTimer += deltaTime;
            float attackInterval = GetEffectiveAttackInterval();

            if (attackTimer >= attackInterval)
            {
                Vector2Int direction = GetPathForwardDirection();
                bool includeBlockingTile = isBlocked && blockingOperator != null;
                List<FrontAttackTarget> targets =
                    FindOperatorsInFront(tileRange, direction, includeBlockingTile);
                if (includeBlockingTile && !ContainsOperator(targets, blockingOperator))
                    targets.Add(new FrontAttackTarget(blockingOperator, 1));

                if (targets.Count > 0)
                {
                    attackTimer = 0f;
                    PlayAttackSound();
                    PlayFrontalSlamVisual(direction, tileRange, includeBlockingTile);
                    for (int i = 0; i < targets.Count; i++)
                    {
                        FrontAttackTarget frontTarget = targets[i];
                        OperatorBase target = frontTarget.Operator;
                        if (target != null && target.IsDeployed)
                        {
                            int damage = frontTarget.Distance == 2
                                ? Mathf.RoundToInt(currentATK * 0.5f)
                                : currentATK;
                            target.TakeDamage(damage, data.damageType, this);
                        }
                    }

                    return;
                }
            }

            if (isBlocked)
            {
                UpdateFacingTowardsBlocker();

                if (Vector3.Distance(transform.position, blockedPosition) > 0.02f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, blockedPosition, currentMoveSpeed * deltaTime);
                }
                else
                {
                    transform.position = blockedPosition;
                }
            }
            else
            {
                MoveAlongPath();
            }
        }

        private List<FrontAttackTarget> FindOperatorsInFront(
            int tileRange,
            Vector2Int direction,
            bool includeBlockingTile)
        {
            var targets = new List<FrontAttackTarget>();
            if (tileRange <= 0 || path == null || gridManager == null ||
                OperatorManager.Instance == null || direction == Vector2Int.zero)
                return targets;

            Vector2Int enemyCell = gridManager.WorldToGridPosition(transform.position);
            var deployedOperators = OperatorManager.Instance.DeployedOperators;
            for (int i = 0; i < deployedOperators.Count; i++)
            {
                OperatorBase op = deployedOperators[i];
                if (op == null || !op.IsDeployed || op.DeployedCell == null)
                    continue;

                Vector2Int offset = op.DeployedCell.GridPosition - enemyCell;
                int tileDistance = offset.x * direction.x + offset.y * direction.y;
                int minimumDistance = includeBlockingTile ? 0 : 1;
                if (tileDistance >= minimumDistance &&
                    tileDistance < minimumDistance + tileRange &&
                    offset == direction * tileDistance)
                {
                    targets.Add(new FrontAttackTarget(op, tileDistance - minimumDistance + 1));
                }
            }

            return targets;
        }

        protected Vector2Int GetPathForwardDirection()
        {
            if (path == null) return Vector2Int.zero;

            for (int i = currentPathIndex; i < path.Count; i++)
            {
                Vector3 waypointOffset = path[i] - transform.position;
                if (waypointOffset.sqrMagnitude <= 0.0001f)
                    continue;

                if (Mathf.Abs(waypointOffset.x) >= Mathf.Abs(waypointOffset.y))
                    return new Vector2Int(waypointOffset.x >= 0f ? 1 : -1, 0);
                return new Vector2Int(0, waypointOffset.y >= 0f ? 1 : -1);
            }

            return Vector2Int.zero;
        }

        private static bool ContainsOperator(List<FrontAttackTarget> targets, OperatorBase target)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Operator == target)
                    return true;
            }

            return false;
        }

        private void PlayFrontalSlamVisual(
            Vector2Int direction,
            int tileRange,
            bool includeBlockingTile)
        {
            if (gridManager == null || direction == Vector2Int.zero || tileRange <= 0)
                return;

            Vector2Int enemyCell = gridManager.WorldToGridPosition(transform.position);
            Vector3 center = gridManager.GridToWorldPosition(enemyCell) +
                new Vector3(direction.x, direction.y, 0f) *
                (gridManager.CellSize *
                    (includeBlockingTile ? tileRange - 1 : tileRange + 1) * 0.5f);

            Combat.SandHulkSlamVisual.Play(center, direction, gridManager.CellSize, tileRange);
        }

        protected virtual void UpdateSpriteAnimation(float deltaTime)
        {
            if (isFrozen) return; // Frozen solid: the animation holds its current frame
            if (data == null || data.animationFrames == null || data.animationFrames.Length <= 1)
                return;

            float frameDuration = 1f / Mathf.Max(0.01f, data.animationFrameRate);
            animationFrameTimer += deltaTime;
            while (animationFrameTimer >= frameDuration)
            {
                animationFrameTimer -= frameDuration;
                animationFrameIndex = (animationFrameIndex + 1) % data.animationFrames.Length;
                ApplyAnimationFrame();
            }
        }

        protected virtual void ApplyAnimationFrame()
        {
            if (data == null || data.animationFrames == null ||
                animationFrameIndex >= data.animationFrames.Length ||
                data.animationFrames[animationFrameIndex] == null)
                return;

            if (animationRenderers == null)
                animationRenderers = GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < animationRenderers.Length; i++)
            {
                // The flyer's ground shadow keeps its own ellipse sprite.
                if (animationRenderers[i] != null && animationRenderers[i].gameObject.name != "AirShadow")
                {
                    animationRenderers[i].sprite = data.animationFrames[animationFrameIndex];
                    animationRenderers[i].flipX = isFacingLeft;
                }
            }
        }

        private OperatorBase FindOperatorTarget(bool prioritizeRangedOperators)
        {
            if (OperatorManager.Instance == null || data == null) return null;

            float range = data.attackRange > 0 ? data.attackRange : 2.5f;
            float sqrRange = range * range;
            OperatorBase tauntTarget = null;
            float tauntSqrDistance = float.MaxValue;
            OperatorBase preferredTarget = null;
            OperatorBase fallbackTarget = null;
            float preferredSqrDistance = float.MaxValue;
            float fallbackSqrDistance = float.MaxValue;
            var deployed = OperatorManager.Instance.DeployedOperators;

            for (int i = 0; i < deployed.Count; i++)
            {
                OperatorBase op = deployed[i];
                if (op == null || !op.IsDeployed || !op.CanBeTargetedByEnemy(this)) continue;

                float sqrDistance = (op.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance > sqrRange) continue;

                // Taunt mechanic: Ranged enemies prioritize taunting operators (Mossmo)
                // if this enemy (the attacker) is in a surrounding tile.
                if (op.ShouldTauntAttacker(transform.position, gridManager))
                {
                    if (sqrDistance < tauntSqrDistance)
                    {
                        tauntSqrDistance = sqrDistance;
                        tauntTarget = op;
                    }
                    continue;
                }

                bool isPreferred = op.Data != null &&
                    (op.Data.position == OperatorPosition.Ranged) == prioritizeRangedOperators;
                if (isPreferred)
                {
                    if (sqrDistance < preferredSqrDistance)
                    {
                        preferredSqrDistance = sqrDistance;
                        preferredTarget = op;
                    }
                }
                else if (sqrDistance < fallbackSqrDistance)
                {
                    fallbackSqrDistance = sqrDistance;
                    fallbackTarget = op;
                }
            }

            if (tauntTarget != null) return tauntTarget;
            return preferredTarget != null ? preferredTarget : fallbackTarget;
        }

        // Enemy sounds sit a little under operator sounds: there are many more enemies on screen.
        private const float EnemySfxVolume = 0.7f;

        // Melee hits land in the same moment the swing starts; an attack sound younger than this is left alone.
        private const float MinAttackSoundBeforeCut = 0.1f;

        private AudioSource attackVoice;
        private AudioClip attackVoiceClip;
        private float attackVoiceStartTime;

        /// <summary>The enemy's own Attack clip (swing, shot, cast), if it has one.</summary>
        protected void PlayAttackSound()
        {
            UnitSfx.Play(data, UnitSfxEvent.Attack, out attackVoice, EnemySfxVolume);
            attackVoiceClip = attackVoice != null ? attackVoice.clip : null;
            attackVoiceStartTime = Time.unscaledTime;
        }

        /// <summary>The enemy's own Attack clip, else the given shared sound (e.g. the melee hit).</summary>
        protected void PlayAttackSound(SfxId fallback)
        {
            if (!UnitSfx.Play(data, UnitSfxEvent.Attack, out attackVoice, EnemySfxVolume))
                attackVoice = AudioManager.Instance?.PlaySfx(fallback, EnemySfxVolume);
            attackVoiceClip = attackVoice != null ? attackVoice.clip : null;
            attackVoiceStartTime = Time.unscaledTime;
        }

        /// <summary>
        /// The enemy's own Impact clip (its projectile landing), if it has one. When it plays, the attack sound
        /// still running is cut so the two don't overlap.
        /// </summary>
        protected void PlayImpactSound()
        {
            if (!UnitSfx.Play(data, UnitSfxEvent.Impact, out AudioSource impactVoice, EnemySfxVolume) || impactVoice == null) return;
            if (attackVoice == null) return;

            if (Time.unscaledTime - attackVoiceStartTime >= MinAttackSoundBeforeCut)
                AudioManager.Instance?.StopSfx(attackVoice, attackVoiceClip);
            attackVoice = null;
            attackVoiceClip = null;
        }

        private void AttackOperator(OperatorBase target)
        {
            PlayAttackSound();
            System.Action onImpact = () =>
            {
                if (target == null || !target.IsDeployed || data == null) return;
                PlayImpactSound();
                target.TryTakeAttackDamage(currentATK, data.damageType, this);
            };

            Color projectileColor = data != null
                ? data.attackProjectileColor
                : new Color(0.55f, 0.9f, 0.3f);
            if (data != null && data.usesSandProjectileVisual)
            {
                CombatProjectileVisual.FireSand(
                    ProjectileOrigin,
                    target.transform.position,
                    projectileColor,
                    8f,
                    onImpact);
                return;
            }

            bool plasma = data != null && data.usesPlasmaProjectileVisual;
            CombatProjectileVisual.Fire(
                ProjectileOrigin,
                target.transform.position,
                projectileColor,
                plasma ? 10f : 8f,
                0.1f,
                0.05f,
                false,
                onImpact,
                plasma ? ProjectileStyle.Plasma : ProjectileStyle.Sludge);
        }

        private void UpdateTrapDamage(float deltaTime)
        {
            if (isDead || isInsideHole || gridManager == null || IsImmuneToAcidTiles) return;

            Vector2Int gridPosition = gridManager.WorldToGridPosition(transform.position);
            GridCell cell = gridManager.GetCell(gridPosition);
            if (cell == null || !cell.IsAcidHazard)
            {
                trapDamageTimer = 0f;
                return;
            }

            trapDamageTimer += deltaTime;
            while (trapDamageTimer >= TrapTileRules.DamageIntervalSeconds && !isDead)
            {
                trapDamageTimer -= TrapTileRules.DamageIntervalSeconds;
                AcidDamageFlash.Flash(gameObject);
                AudioManager.Instance?.PlaySfx(SfxId.TileTrapTick, 0.6f);
                TakeTrueDamage(cell.AcidDamagePerTick);
            }
        }
    }
}
