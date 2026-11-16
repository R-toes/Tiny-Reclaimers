using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.GameLoop;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;

namespace TrashTD.Operators
{
    // Stun and bind status handling and their visuals.
    public partial class OperatorBase
    {
        // ========================
        // Stun System
        // ========================

        /// <summary>
        /// Applies a stun to this operator. While stunned, an operator cannot attack and block.
        /// </summary>
        public virtual void ApplyStun(float duration)
        {
            if (!isDeployed || currentHP <= 0 || duration <= 0f) return;

            stunTimer = Mathf.Max(stunTimer, duration);
            if (!isStunned)
            {
                isStunned = true;
                AudioManager.Instance?.PlaySfx(SfxId.CombatStun);
            }

            // Immediately release any currently blocked enemies
            for (int i = blockedEnemies.Count - 1; i >= 0; i--)
            {
                ReleaseBlock(blockedEnemies[i]);
            }

            UpdateStunVisual();
        }

        public void UpdateStunStatus(float dt)
        {
            if (stunTimer > 0f)
            {
                stunTimer = Mathf.Max(0f, stunTimer - dt);
                if (stunTimer <= 0f)
                {
                    isStunned = false;
                    UpdateStunVisual();
                }
            }
        }

        // ========================
        // Bind System
        // ========================

        private PersistentStatusText boundStatusText;
        public PersistentStatusText BoundStatusText => boundStatusText;

        /// <summary>
        /// Applies a bind to this operator. While bound, an operator cannot attack, block, or use abilities.
        /// </summary>
        public virtual void ApplyBind(float duration, EnemyBase source = null)
        {
            if (!isDeployed || currentHP <= 0 || duration <= 0f) return;

            bindTimer = Mathf.Max(bindTimer, duration);
            if (!isBound)
            {
                isBound = true;
            }

            // Immediately release any currently blocked enemies
            for (int i = blockedEnemies.Count - 1; i >= 0; i--)
            {
                ReleaseBlock(blockedEnemies[i]);
            }

            UpdateStunVisual();
        }

        public virtual void RemoveBind()
        {
            if (isBound)
            {
                isBound = false;
                bindTimer = 0f;
                ClearBoundStatusText();
                UpdateStunVisual();
            }
        }

        public void UpdateBindStatus(float dt)
        {
            if (bindTimer > 0f)
            {
                bindTimer = Mathf.Max(0f, bindTimer - dt);
                if (bindTimer <= 0f)
                {
                    isBound = false;
                    ClearBoundStatusText();
                    UpdateStunVisual();
                }
            }
        }

        private void ClearBoundStatusText()
        {
            if (boundStatusText != null)
            {
                boundStatusText.Dismiss();
                boundStatusText = null;
            }
            PersistentStatusText.Hide(gameObject, "Bound");
        }

        protected virtual void UpdateStunVisual()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                    spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer != null)
            {
                Color current = spriteRenderer.color;
                if (isBound)
                {
                    spriteRenderer.color = new Color(0.72f, 0.45f, 0.95f, current.a);
                }
                else if (isStunned)
                {
                    spriteRenderer.color = new Color(1f, 0.95f, 0.4f, current.a);
                }
                else
                {
                    spriteRenderer.color = new Color(1f, 1f, 1f, current.a);
                }
            }

            if (isStunned)
            {
                TrashTD.Combat.StunStarsVisual.Show(gameObject, spriteRenderer);
            }
            else
            {
                TrashTD.Combat.StunStarsVisual.Hide(gameObject);
            }
        }
    }
}
