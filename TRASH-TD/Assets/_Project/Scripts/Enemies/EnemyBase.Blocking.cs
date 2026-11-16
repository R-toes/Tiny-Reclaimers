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
    // Being blocked by an operator and attacking the blocker.
    public partial class EnemyBase
    {
        // ========================
        // Blocking (GDD 1.4.4)
        // ========================

        /// <summary>
        /// Called when this enemy is blocked by an operator.
        /// </summary>
        public virtual void OnBlocked(OperatorBase blocker)
        {
            isBlocked = true;
            blockingOperator = blocker;

            Vector2Int blockerGridPos = blocker != null && blocker.DeployedCell != null
                ? blocker.DeployedCell.GridPosition
                : (gridManager != null && blocker != null ? gridManager.WorldToGridPosition(blocker.transform.position) : Vector2Int.zero);

            blockedAdjacentGridPos = blockerGridPos;
            bool foundInPath = false;

            if (path != null && path.Count > 0)
            {
                int bestIdx = -1;
                if (gridManager != null)
                {
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (gridManager.WorldToGridPosition(path[i]) == blockerGridPos)
                        {
                            if (bestIdx == -1 || Mathf.Abs(i - currentPathIndex) < Mathf.Abs(bestIdx - currentPathIndex))
                            {
                                bestIdx = i;
                            }
                        }
                    }
                }
                else if (blocker != null)
                {
                    float minDist = float.MaxValue;
                    for (int i = 0; i < path.Count; i++)
                    {
                        float d = Vector3.Distance(path[i], blocker.transform.position);
                        if (d < minDist && d <= 1.0f)
                        {
                            minDist = d;
                            bestIdx = i;
                        }
                    }
                }

                if (bestIdx != -1)
                {
                    foundInPath = true;
                    // The enemy approaches the blocker from path[bestIdx - 1] towards path[bestIdx].
                    // When blocked, the enemy is stopped in front of the blocker (on the incoming/entrance boundary).
                    if (bestIdx > 0)
                    {
                        Vector3 normalStopPos = (path[bestIdx - 1] + path[bestIdx]) * 0.5f;
                        Vector3 incomingDir = (path[bestIdx] - path[bestIdx - 1]).normalized;
                        Vector3 blockerPos = blocker != null ? blocker.transform.position : path[bestIdx];

                        bool isPastBlocker = false;
                        if (currentPathIndex > bestIdx)
                        {
                            isPastBlocker = true;
                        }
                        else
                        {
                            float progressPastBlocker = Vector3.Dot(transform.position - blockerPos, incomingDir);
                            float progressPastStop = Vector3.Dot(transform.position - normalStopPos, incomingDir);

                            if (progressPastBlocker > 0.05f || progressPastStop > 0.05f)
                            {
                                isPastBlocker = true;
                            }
                        }

                        if (isPastBlocker)
                        {
                            // Enemy is already past the entrance stopping point or past the operator.
                            // Don't push them backwards; stop them right where they are.
                            blockedPosition = transform.position;
                            blockedAdjacentGridPos = gridManager != null
                                ? gridManager.WorldToGridPosition(transform.position)
                                : blockerGridPos;

                            if (currentPathIndex > bestIdx || Vector3.Dot(transform.position - blockerPos, incomingDir) > 0.05f)
                            {
                                currentPathIndex = Mathf.Min(path.Count - 1, Mathf.Max(currentPathIndex, bestIdx + 1));
                            }
                            else
                            {
                                currentPathIndex = bestIdx;
                            }
                        }
                        else
                        {
                            blockedAdjacentGridPos = gridManager != null
                                ? gridManager.WorldToGridPosition(path[bestIdx - 1])
                                : blockerGridPos;
                            blockedPosition = normalStopPos;
                            float backoff = BlockStopBackoff * (gridManager != null ? gridManager.CellSize : 1f);
                            if (backoff > 0f)
                            {
                                // Large sprites stop short of the tile boundary. Never pull back an enemy already beyond that point.
                                Vector3 backedStopPos = normalStopPos - incomingDir * backoff;
                                blockedPosition = Vector3.Dot(transform.position - backedStopPos, incomingDir) > 0f
                                    ? transform.position
                                    : backedStopPos;
                            }
                            currentPathIndex = bestIdx;
                        }
                    }
                    else
                    {
                        // Blocker is on the very first waypoint (spawn)
                        blockedAdjacentGridPos = blockerGridPos;
                        blockedPosition = transform.position;
                        currentPathIndex = bestIdx;
                    }
                }
            }

            if (!foundInPath)
            {
                if (blocker != null)
                {
                    Vector2Int forwardOffset = blocker.Facing.ToOffset();
                    blockedAdjacentGridPos = blockerGridPos + forwardOffset;
                    Vector3 defaultStop;
                    if (gridManager != null)
                    {
                        Vector3 forwardWorld = gridManager.GridToWorldPosition(blockedAdjacentGridPos);
                        defaultStop = (blocker.transform.position + forwardWorld) * 0.5f;
                    }
                    else
                    {
                        defaultStop = blocker.transform.position + (Vector3)(Vector2)forwardOffset * 0.5f;
                    }

                    Vector3 forwardDir = (Vector3)(Vector2)forwardOffset;
                    if (forwardDir != Vector3.zero) forwardDir.Normalize();

                    if (Vector3.Dot(transform.position - blocker.transform.position, -forwardDir) > 0.05f ||
                        Vector3.Distance(transform.position, blocker.transform.position) < Vector3.Distance(defaultStop, blocker.transform.position))
                    {
                        blockedPosition = transform.position;
                    }
                    else
                    {
                        blockedPosition = defaultStop;
                    }
                }
                else
                {
                    blockedPosition = transform.position;
                }
            }

            if (!Application.isPlaying)
            {
                transform.position = blockedPosition;
            }

            UpdateFacingTowardsBlocker();
        }

        /// <summary>
        /// Called when this enemy is released from blocking.
        /// </summary>
        public virtual void OnUnblocked()
        {
            isBlocked = false;
            blockingOperator = null;
        }

        /// <summary>
        /// Attack the blocking operator while blocked.
        /// </summary>
        protected virtual void AttackBlocker()
        {
            if (blockingOperator == null) return;

            // Casters never melee their blocker; they keep their own ranged attacks (Askalaser, Hextension).
            if (data != null && data.archetype == EnemyArchetype.Caster) return;

            attackTimer += Time.deltaTime;
            if (attackTimer >= GetEffectiveAttackInterval())
            {
                attackTimer = 0f;
                PlayAttackSound();
                MeleeSwipeVisual.Play(VisualPosition, blockingOperator.transform.position, new Color(1f, 0.35f, 0.25f, 1f), 1f,
                    data.archetype == EnemyArchetype.Tank ? MeleeStyle.Bash : MeleeStyle.Claw);
                blockingOperator.TryTakeAttackDamage(currentATK, data.damageType, this);
            }
        }
    }
}
