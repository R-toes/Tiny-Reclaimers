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
    // Taking damage, healing, devouring and dying.
    public partial class EnemyBase
    {
        // ========================
        // Damage & Death
        // ========================

        /// <summary>
        /// Take damage from an operator or effect.
        /// Damage formula: max(ATK - DEF, ATK * 0.05) — applied by DamageCalculator before calling this.
        /// </summary>
        public virtual void TakeDamage(int damage, DamageType damageType)
        {
            if (isDead || isInsideHole || isDevoured || damage <= 0) return;

            int previousHP = currentHP;
            currentHP = Mathf.Max(0, currentHP - damage);
            int actualDamage = previousHP - currentHP;
            if (actualDamage > 0)
            {
                FloatingCombatNumber.Show(VisualPosition, actualDamage, damageType, false, this);
                WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
                HitReaction.Play(gameObject, VisualPosition, actualDamage, maxHP);
            }

            if (currentHP <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Take true damage directly (ignoring DEF and RES).
        /// </summary>
        public virtual void TakeTrueDamage(int damage)
        {
            TakeDamage(damage, DamageType.True);
        }

        /// <summary>
        /// Restore health to this enemy up to its maximum HP.
        /// </summary>
        public virtual void Heal(int amount)
        {
            if (isDead || amount <= 0) return;
            int previousHP = currentHP;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            int healed = currentHP - previousHP;
            if (healed > 0)
            {
                WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
            }
        }

        /// <summary>
        /// Check if this enemy can receive damage from the specified attacker.
        /// Overridden by specialized enemies like Gooelist (only receives damage from its blocker when blocked).
        /// </summary>
        public virtual bool CanReceiveDamageFrom(OperatorBase attacker)
        {
            return true;
        }

        public virtual bool TryTakeAttackDamage(int damage, DamageType damageType, OperatorBase attacker = null, bool canHitAir = false)
        {
            if (isDead || isInsideHole || isDevoured) return false;

            if (!CanReceiveDamageFrom(attacker))
            {
                FloatingCombatNumber.ShowText(VisualPosition, "DUEL", Color.white, this);
                return false;
            }
            bool bypassAirImmunity = canHitAir || (attacker != null && attacker.CanHitAir);
            if (!bypassAirImmunity &&
                attacker != null && attacker.Data != null &&
                attacker.Data.position == OperatorPosition.Melee &&
                data != null && data.movementType == EnemyMovementType.Air)
                return false;

            bool ignoresMiss = attacker != null && attacker.IgnoresMissMechanic;
            if (!ignoresMiss)
            {
                if (!Combat.StageCombatModifiers.TryAttackHit(transform.position))
                    return false;

                if (data != null && data.operatorAttackMissChance > 0f &&
                    !Combat.StageCombatModifiers.TryAttackHit(data.operatorAttackMissChance))
                {
                    Combat.DodgeAfterimageVisual.Play(gameObject);
                    FloatingCombatNumber.ShowMiss(VisualPosition, this);
                    AudioManager.Instance?.PlaySfx(SfxId.CombatMiss, 0.6f);
                    return false;
                }
            }

            if (attacker != null)
            {
                // Flurmonica's Harmony on a Caster: +4% Arts damage per stack
                float harmony = damageType == DamageType.Arts ? attacker.OutgoingArtsDamageMultiplier : 1f;
                if (harmony > 1f)
                {
                    damage = Mathf.Max(1, Mathf.RoundToInt(damage * harmony));
                }

                float junkernautReduction = JunkernautEnemy.GetDamageReductionPercent(this);
                if (junkernautReduction > 0f)
                {
                    damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - junkernautReduction)));
                    JunkernautAuraVisual.NotifyProtectedHit(this);
                }

                if (isFrozen && ChillpathOperator.IsFrozenDamageBonusActive)
                {
                    damage = Mathf.Max(1, Mathf.RoundToInt(damage * ChillpathOperator.FrozenEnemyDamageMultiplier));
                }
            }

            int hpBeforeHit = currentHP;
            TakeDamage(damage, damageType);
            if (attacker != null && OperatorDamageDealt != null && hpBeforeHit > currentHP)
                OperatorDamageDealt.Invoke(attacker, hpBeforeHit - currentHP);
            attacker?.PlayImpactSound();
            return true;
        }

        /// <summary>
        /// HP an operator's attack actually removed (after DEF/RES, misses and the Junkernaut aura).
        /// Read by the QA sandbox's damage meter; nothing subscribes in normal play.
        /// </summary>
        public static event System.Action<OperatorBase, int> OperatorDamageDealt;

        /// <summary>
        /// Mlem's tongue grabs this enemy: it is released from its blocker and stops where it is (no moving,
        /// attacking or taking hits) until <see cref="CompleteDevour"/> or <see cref="CancelDevour"/>.
        /// Returns false if it can't be grabbed (dead, in a hole, already grabbed).
        /// </summary>
        public bool BeginDevour()
        {
            if (isDead || isInsideHole || isDevoured) return false;

            if (isBlocked && blockingOperator != null)
            {
                blockingOperator.ReleaseBlock(this);
            }

            isPushingBack = false;
            isDevoured = true;
            Combat.StunStarsVisual.Hide(gameObject);
            return true;
        }

        /// <summary>Lets a grabbed enemy go (Mlem was stunned, retreated or died before the swallow).</summary>
        public void CancelDevour()
        {
            isDevoured = false;
        }

        /// <summary>
        /// The swallow: kills this enemy outright, whatever its HP. It goes through the normal death path, so it
        /// counts as a kill for rewards and score. With <paramref name="triggerDeathEffects"/> off, death overrides
        /// skip their own effect (see <see cref="DeathEffectsSuppressed"/>).
        /// </summary>
        public void CompleteDevour(bool triggerDeathEffects)
        {
            if (isDead) return;

            DeathEffectsSuppressed = !triggerDeathEffects;
            Die();
        }

        /// <summary>
        /// Kill immediately without going through damage (sandbox wave clears).
        /// </summary>
        public virtual void ForceKill()
        {
            if (!isDead) Die();
        }

        /// <summary>
        /// Handle enemy death.
        /// </summary>
        protected virtual void Die()
        {
            isDead = true;
            Combat.StunStarsVisual.Hide(gameObject);
            if (iceVisual != null)
            {
                // A frozen enemy shatters as it dies
                iceVisual.Apply(Combat.IceStatusState.None, null);
                appliedIceState = Combat.IceStatusState.None;
            }
            if (glueVisual != null)
            {
                glueVisual.Apply(false, 0, null);
            }

            // Release from blocker if blocked
            if (isBlocked && blockingOperator != null)
            {
                blockingOperator.ReleaseBlock(this);
            }

            OnDied?.Invoke(this);

            // TODO: Death animation, loot drops
            Destroy(gameObject);
        }
    }
}
