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
    // Movement along the path, facing, holes, push back and pull.
    public partial class EnemyBase
    {
        // ========================
        // Movement
        // ========================

        /// <summary>
        /// Move along the assigned path toward the exit.
        /// </summary>
        protected virtual void MoveAlongPath()
        {
            MoveAlongPathBy(currentMoveSpeed * Time.deltaTime);
        }

        /// <summary>Moves up to <paramref name="step"/> world units along the path (for enemies that set their own pace).</summary>
        protected void MoveAlongPathBy(float step)
        {
            if (path == null || currentPathIndex >= path.Count)
                return;

            Vector3 targetPos = path[currentPathIndex];
            UpdateFacing(targetPos.x - transform.position.x);

            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

            if (Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                // Check if ground enemy reached a Hole tile
                if (data != null && data.movementType == EnemyMovementType.Ground && gridManager != null)
                {
                    Vector2Int gridPos = gridManager.WorldToGridPosition(transform.position);
                    GridCell currentCell = gridManager.GetCell(gridPos);
                    if (currentCell != null && currentCell.TileType == TileType.Hole)
                    {
                        Vector3 destWorldPos = transform.position;
                        if (currentPathIndex + 1 < path.Count)
                        {
                            Vector2Int nextPos = gridManager.WorldToGridPosition(path[currentPathIndex + 1]);
                            GridCell nextCell = gridManager.GetCell(nextPos);
                            if (nextCell != null && nextCell.TileType == TileType.Hole)
                            {
                                destWorldPos = path[currentPathIndex + 1];
                            }
                            else
                            {
                                GridCell linked = gridManager.GetLinkedHoleCell(gridPos);
                                destWorldPos = linked != null ? linked.WorldPosition : transform.position;
                            }
                        }
                        else
                        {
                            GridCell linked = gridManager.GetLinkedHoleCell(gridPos);
                            destWorldPos = linked != null ? linked.WorldPosition : transform.position;
                        }

                        if (destWorldPos != transform.position)
                        {
                            currentPathIndex++;
                if (currentPathIndex < path.Count)
                {
                    UpdateFacing(path[currentPathIndex].x - transform.position.x);
                }
                            EnterHole(destWorldPos, DefaultHoleStayDuration);
                            return;
                        }
                    }
                }

                currentPathIndex++;
                if (currentPathIndex < path.Count)
                {
                    UpdateFacing(path[currentPathIndex].x - transform.position.x);
                }

                if (currentPathIndex >= path.Count)
                {
                    ReachExit();
                }
            }
        }

        /// <summary>
        /// Updates the facing direction based on horizontal movement.
        /// Walking left mirrors the sprite (flipX = true).
        /// Walking right unmirrors the sprite (flipX = false).
        /// Moving purely up or down maintains the previous mirrored state.
        /// </summary>
        public virtual void UpdateFacing(float deltaX)
        {
            const float threshold = 0.001f;
            if (deltaX < -threshold)
            {
                SetFacingLeft(true);
            }
            else if (deltaX > threshold)
            {
                SetFacingLeft(false);
            }
        }

        public void SetFacingLeft(bool facingLeft)
        {
            isFacingLeft = facingLeft;
            ApplyFacingVisuals();
        }

        public virtual void ApplyFacingVisuals()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr != null)
                {
                    if (sr.gameObject.name == "AirShadow" || sr.gameObject.name.StartsWith("StunStars") || sr.gameObject.name.StartsWith("EnragedVisual") || sr.gameObject.name.StartsWith(Combat.IceStatusVisual.ObjectName)) continue;
                    sr.flipX = isFacingLeft;
                }
            }

            Transform visualObj = transform.Find("EnemyVisual");
            if (visualObj != null && data != null && data.visualOffset.x != 0f)
            {
                visualObj.localPosition = new Vector3(
                    isFacingLeft ? -data.visualOffset.x : data.visualOffset.x,
                    data.visualOffset.y,
                    data.visualOffset.z);
            }
        }

        /// <summary>
        /// Enter a hole/ladder tile: becomes untargetable, invulnerable, hides visuals,
        /// and teleports to the destination hole after the duration.
        /// </summary>
        public void EnterHole(Vector3 destinationPos, float duration = DefaultHoleStayDuration)
        {
            if (isDead || isInsideHole) return;

            isInsideHole = true;
            holeStayTimer = duration;
            destinationHolePosition = destinationPos;

            if (isBlocked && blockingOperator != null)
            {
                blockingOperator.ReleaseBlock(this);
            }

            SetVisualsActive(false);
            AudioManager.Instance?.PlaySfx(SfxId.TileHoleEnter, 0.7f);
        }

        /// <summary>
        /// Exits the hole tile and reappears at the destination.
        /// </summary>
        public void ExitHole()
        {
            if (!isInsideHole) return;

            transform.position = destinationHolePosition;
            isInsideHole = false;
            SetVisualsActive(true);
            AudioManager.Instance?.PlaySfx(SfxId.TileHoleExit, 0.7f);

            if (path != null && currentPathIndex < path.Count)
            {
                if (Vector3.Distance(transform.position, path[currentPathIndex]) < 0.1f)
                {
                    currentPathIndex++;
                if (currentPathIndex < path.Count)
                {
                    UpdateFacing(path[currentPathIndex].x - transform.position.x);
                }
                }
            }

            if (path != null && currentPathIndex >= path.Count)
            {
                ReachExit();
            }
        }

        private void UpdateHoleStay(float deltaTime)
        {
            if (!isInsideHole) return;

            holeStayTimer -= deltaTime;
            if (holeStayTimer <= 0f)
            {
                ExitHole();
            }
        }

        private void SetVisualsActive(bool active)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = active;
            }

            var healthBar = GetComponent<Combat.WorldHealthBar>();
            if (healthBar != null)
            {
                healthBar.SetVisible(active);
            }
        }

        /// <summary>
        /// Push this enemy backward along its route by the requested number of grid cells.
        /// </summary>
        public bool PushBack(int cells, float duration = 0.45f)
        {
            if (isDead || isInsideHole || isPushingBack || gridManager == null || path == null || cells <= 0 || currentPathIndex <= 0)
                return false;

            float remainingDistance = gridManager.CellSize * cells;
            if (remainingDistance <= 0f)
                return false;

            Vector3 startPosition = transform.position;
            Vector3 pushedPosition;
            int nextPathIndex;
            bool moved;
            Vector3 previousTargetPosition = startPosition;
            bool hasPreviousTarget = false;
            do
            {
                CalculatePushbackTarget(startPosition, remainingDistance, out pushedPosition, out nextPathIndex, out moved);
                if (!moved)
                    return false;

                if (hasPreviousTarget && (pushedPosition - previousTargetPosition).sqrMagnitude <= Mathf.Epsilon)
                    return false;

                GridCell destinationCell = gridManager.GetCell(gridManager.WorldToGridPosition(pushedPosition));
                if (destinationCell == null || !destinationCell.IsOccupied || destinationCell.OccupantOperator == null)
                    break;

                previousTargetPosition = pushedPosition;
                hasPreviousTarget = true;
                remainingDistance += gridManager.CellSize;
            }
            while (remainingDistance > 0f);

            if (isBlocked && blockingOperator != null)
                blockingOperator.ReleaseBlock(this);

            pushbackStartPosition = transform.position;
            pushbackTargetPosition = pushedPosition;
            pushbackTargetPathIndex = nextPathIndex;
            pushbackElapsed = 0f;
            pushbackDuration = Mathf.Max(0.01f, duration);
            isPushingBack = true;
            return true;
        }

        /// <summary>
        /// Pull this enemy to the specified destination, stopping its movement and attacks for stopDuration seconds.
        /// Returns true if the pull was initiated.
        /// </summary>
        public bool PullTo(Vector3 destination, float stopDuration = 2f, float duration = 0.2f)
        {
            if (isDead || isInsideHole)
                return false;

            if (isBlocked && blockingOperator != null)
                blockingOperator.ReleaseBlock(this);

            int nextPathIndex = currentPathIndex;
            if (path != null && path.Count > 0)
            {
                int closestIndex = 0;
                float closestDist = float.MaxValue;
                for (int i = 0; i < path.Count; i++)
                {
                    float d = Vector3.Distance(destination, path[i]);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestIndex = i;
                    }
                }
                nextPathIndex = Mathf.Clamp(closestIndex + 1, 0, path.Count - 1);
            }

            ApplyStun(stopDuration);

            if (!Application.isPlaying)
            {
                transform.position = destination;
                currentPathIndex = nextPathIndex;
                isPushingBack = false;
                return true;
            }

            pushbackStartPosition = transform.position;
            pushbackTargetPosition = destination;
            pushbackTargetPathIndex = nextPathIndex;
            pushbackElapsed = 0f;
            pushbackDuration = Mathf.Max(0.01f, duration);
            isPushingBack = true;
            return true;
        }

        private void CalculatePushbackTarget(
            Vector3 startPosition,
            float distance,
            out Vector3 targetPosition,
            out int targetPathIndex,
            out bool moved)
        {
            targetPosition = startPosition;
            targetPathIndex = currentPathIndex;
            moved = false;
            int waypointIndex = Mathf.Min(currentPathIndex - 1, path.Count - 1);

            while (waypointIndex >= 0 && distance > 0f)
            {
                Vector3 waypoint = path[waypointIndex];
                float segmentDistance = Vector3.Distance(targetPosition, waypoint);
                if (segmentDistance <= Mathf.Epsilon)
                {
                    targetPathIndex = waypointIndex;
                    waypointIndex--;
                    continue;
                }

                if (segmentDistance >= distance)
                {
                    targetPosition = Vector3.MoveTowards(targetPosition, waypoint, distance);
                    moved = true;
                    distance = 0f;
                }
                else
                {
                    targetPosition = waypoint;
                    distance -= segmentDistance;
                    targetPathIndex = waypointIndex;
                    waypointIndex--;
                    moved = true;
                }
            }
        }

        private void UpdatePushback()
        {
            pushbackElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(pushbackElapsed / pushbackDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(pushbackStartPosition, pushbackTargetPosition, easedProgress);

            if (progress >= 1f)
            {
                currentPathIndex = pushbackTargetPathIndex;
                isPushingBack = false;
            }
        }

        /// <summary>
        /// Called when the enemy reaches the exit point.
        /// Costs the player life points (defined in EnemyData.lifePointCost).
        /// </summary>
        protected virtual void ReachExit()
        {
            OnReachedExit?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
