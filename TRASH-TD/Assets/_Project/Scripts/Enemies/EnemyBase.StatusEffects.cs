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
    // Speed modifiers, chill, stun, sluggish and their visuals.
    public partial class EnemyBase
    {
        // ========================
        // Speed Modifiers
        // ========================

        /// <summary>
        /// Apply a speed modifier (for slows, stuns, etc).
        /// </summary>
        public void ApplySpeedModifier(float multiplier)
        {
            currentMoveSpeed = data.moveSpeed * multiplier;
        }

        /// <summary>
        /// Reset speed to base value.
        /// </summary>
        public void ResetSpeed()
        {
            currentMoveSpeed = data.moveSpeed;
        }

        /// <summary>
        /// Applies a timed movement slow and accumulates chill toward a temporary freeze.
        /// </summary>
        public bool ApplyChill(
            float amount,
            float slowMultiplier,
            float slowDuration,
            float freezeThreshold,
            float freezeDuration)
        {
            if (isDead || isInsideHole || data == null || amount <= 0f || slowDuration <= 0f || freezeThreshold <= 0f)
                return false;

            if (slowTimer <= 0f && !isFrozen)
            {
                speedBeforeChill = currentMoveSpeed;
            }

            chillSlowMultiplier = Mathf.Min(chillSlowMultiplier, Mathf.Clamp01(slowMultiplier));
            slowTimer = Mathf.Max(slowTimer, slowDuration);
            if (!isFrozen)
            {
                currentMoveSpeed = speedBeforeChill * chillSlowMultiplier;
            }

            bool triggeredFreeze = false;
            chillAmount += amount;
            if (chillAmount >= freezeThreshold && freezeDuration > 0f)
            {
                chillAmount = 0f;
                isFrozen = true;
                freezeTimer = Mathf.Max(freezeTimer, freezeDuration);
                currentMoveSpeed = 0f;
                triggeredFreeze = true;
            }

            UpdateChillVisual();
            return triggeredFreeze;
        }

        /// <summary>
        /// Applies a stun to this enemy, completely stopping its movement and attacks for the given duration.
        /// </summary>
        public void ApplyStun(float duration)
        {
            if (isDead || isInsideHole || duration <= 0f) return;

            stunTimer = Mathf.Max(stunTimer, duration);
            if (!isStunned)
            {
                isStunned = true;
                Combat.StunStarsVisual.Show(gameObject, GetPrimaryVisualRenderer());
                AudioManager.Instance?.PlaySfx(SfxId.CombatStun);
            }

            UpdateChillVisual();
        }

        public virtual float GetEffectiveAttackInterval()
        {
            if (data == null) return float.PositiveInfinity;
            float interval = Combat.StageCombatModifiers.GetAttackInterval(data.attackInterval);
            if (isSluggish)
            {
                interval *= sluggishAttackIntervalMultiplier;
            }
            return interval;
        }

        public void ApplySluggish(float duration = 3.0f, float speedMultiplier = 0.6f, float attackIntervalMultiplier = 1.5f)
        {
            if (isDead || isInsideHole) return;

            if (slowTimer <= 0f && sluggishTimer <= 0f && !isFrozen)
            {
                speedBeforeChill = currentMoveSpeed;
            }

            sluggishMoveSpeedMultiplier = Mathf.Min(sluggishMoveSpeedMultiplier, Mathf.Clamp01(speedMultiplier));
            sluggishAttackIntervalMultiplier = Mathf.Max(sluggishAttackIntervalMultiplier, Mathf.Max(1f, attackIntervalMultiplier));
            sluggishTimer = Mathf.Max(sluggishTimer, duration);

            bool wasSluggish = isSluggish;
            isSluggish = true;

            if (!wasSluggish)
            {
                UpdateGlueVisual();
                if (glueVisual != null) glueVisual.PlaySplatBurst();
            }

            if (!isFrozen && !isStunned)
            {
                float mult = (slowTimer > 0f ? chillSlowMultiplier : 1f) * sluggishMoveSpeedMultiplier;
                currentMoveSpeed = speedBeforeChill * mult;
            }
        }

        protected void UpdateChillStatus(float deltaTime)
        {
            bool wasSlowed = slowTimer > 0f;
            bool wasFrozen = isFrozen;

            if (slowTimer > 0f)
            {
                slowTimer = Mathf.Max(0f, slowTimer - deltaTime);
            }

            if (freezeTimer > 0f)
            {
                freezeTimer = Mathf.Max(0f, freezeTimer - deltaTime);
                if (freezeTimer <= 0f)
                {
                    isFrozen = false;
                }
            }

            if (stunTimer > 0f)
            {
                stunTimer = Mathf.Max(0f, stunTimer - deltaTime);
                if (stunTimer <= 0f)
                {
                    isStunned = false;
                    Combat.StunStarsVisual.Hide(gameObject);
                }
            }

            bool wasSluggish = isSluggish;
            if (sluggishTimer > 0f)
            {
                sluggishTimer = Mathf.Max(0f, sluggishTimer - deltaTime);
                if (sluggishTimer <= 0f)
                {
                    isSluggish = false;
                    sluggishMoveSpeedMultiplier = 1f;
                    sluggishAttackIntervalMultiplier = 1f;
                    resicochetCharge = 0;
                }
            }

            if (!isFrozen && !isStunned)
            {
                float speedMultiplier = 1f;
                if (slowTimer > 0f) speedMultiplier *= chillSlowMultiplier;
                if (sluggishTimer > 0f) speedMultiplier *= sluggishMoveSpeedMultiplier;

                if (slowTimer > 0f || sluggishTimer > 0f)
                {
                    currentMoveSpeed = speedBeforeChill * speedMultiplier;
                }
                else if (wasSlowed || wasFrozen || wasSluggish)
                {
                    currentMoveSpeed = speedBeforeChill;
                    chillSlowMultiplier = 1f;
                    sluggishMoveSpeedMultiplier = 1f;
                    sluggishAttackIntervalMultiplier = 1f;
                }
            }

            UpdateChillVisual();
        }

        public static Color GetResicochetChargeColor(int charge)
        {
            return charge switch
            {
                1 => ResicochetOperator.Charge1ProjectileColor, // Orange
                2 => ResicochetOperator.Charge2ProjectileColor, // Green
                3 => ResicochetOperator.Charge3ProjectileColor, // Blue
                _ => ResicochetOperator.Charge1ProjectileColor
            };
        }

        private void CacheChillRenderers()
        {
            if (chillRenderers != null && originalRendererColors != null && chillRenderers.Length > 0)
                return;

            chillRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalRendererColors = new Color[chillRenderers.Length];
            for (int i = 0; i < chillRenderers.Length; i++)
            {
                originalRendererColors[i] = chillRenderers[i] != null ? chillRenderers[i].color : Color.white;
            }
        }

        private readonly List<MonoBehaviour> pausedAnimations = new List<MonoBehaviour>();
        private bool animationsPaused;

        /// <summary>
        /// Freezes the sprite animation on its current frame while frozen. The enemy's sprite-sheet animation
        /// components are switched off and back on (only the ones that were running), so every enemy type holds
        /// its pose without each animation needing to know about freezing.
        /// </summary>
        private void SetAnimationsPaused(bool paused)
        {
            if (paused == animationsPaused) return;
            animationsPaused = paused;

            if (paused)
            {
                MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null || behaviour == this || behaviour is EnemyBase) continue;
                    if (!(behaviour is IEnemySpriteFrames) || !behaviour.enabled) continue;
                    behaviour.enabled = false;
                    pausedAnimations.Add(behaviour);
                }
            }
            else
            {
                for (int i = 0; i < pausedAnimations.Count; i++)
                {
                    if (pausedAnimations[i] != null) pausedAnimations[i].enabled = true;
                }
                pausedAnimations.Clear();
            }
        }

        /// <summary>The ice art: frost while chilled, an ice block while frozen (on top of the blue tint).</summary>
        private void UpdateIceVisual()
        {
            SetAnimationsPaused(isFrozen);

            Combat.IceStatusState desired = isFrozen
                ? Combat.IceStatusState.Ice
                : (slowTimer > 0f ? Combat.IceStatusState.Frost : Combat.IceStatusState.None);
            if (desired == appliedIceState) return;

            appliedIceState = desired;
            if (desired == Combat.IceStatusState.None && iceVisual == null) return;
            iceVisual = Combat.IceStatusVisual.SetState(gameObject, desired, GetPrimaryVisualRenderer(), iceVisual);
        }

        private void UpdateGlueVisual()
        {
            int effectiveCharge = resicochetCharge > 0 ? resicochetCharge : (isSluggish ? 1 : 0);
            if (!isSluggish && glueVisual == null) return;
            glueVisual = Combat.SluggishGlueVisual.SetState(gameObject, isSluggish, effectiveCharge, GetPrimaryVisualRenderer(), glueVisual);
        }

        private void UpdateChillVisual()
        {
            if (chillRenderers == null || originalRendererColors == null)
            {
                CacheChillRenderers();
                if (chillRenderers == null || originalRendererColors == null) return;
            }

            float tintAmount = 0f;
            Color effectColor = Color.white;

            if (isFrozen)
            {
                tintAmount = 0.7f;
                effectColor = new Color(0.1f, 0.6f, 1f);
            }
            else if (isStunned)
            {
                tintAmount = 0.85f;
                effectColor = new Color(1f, 0.9f, 0.15f);
            }
            else if (slowTimer > 0f)
            {
                tintAmount = 0.3f;
                effectColor = new Color(0.1f, 0.6f, 1f);
            }
            else if (isSluggish)
            {
                tintAmount = 0.35f;
                int effectiveCharge = resicochetCharge > 0 ? resicochetCharge : 1;
                effectColor = GetResicochetChargeColor(effectiveCharge);
            }

            UpdateIceVisual();
            UpdateGlueVisual();

            for (int i = 0; i < chillRenderers.Length; i++)
            {
                if (chillRenderers[i] == null) continue;
                if (chillRenderers[i].gameObject.name == "AirShadow" ||
                    chillRenderers[i].gameObject.name.StartsWith("StunStars") ||
                    chillRenderers[i].gameObject.name.StartsWith("EnragedVisual") ||
                    chillRenderers[i].gameObject.name.StartsWith(Combat.IceStatusVisual.ObjectName) ||
                    chillRenderers[i].gameObject.name.StartsWith(Combat.SluggishGlueVisual.ObjectName))
                    continue;

                Color original = originalRendererColors[i];
                Color target = new Color(effectColor.r, effectColor.g, effectColor.b, original.a);
                chillRenderers[i].color = Color.Lerp(original, target, tintAmount);
            }
        }
    }
}
