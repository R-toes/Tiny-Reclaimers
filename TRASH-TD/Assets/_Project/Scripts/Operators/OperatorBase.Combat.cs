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
    // The per-frame update, targeting, taking damage, healing and temporary HP.
    public partial class OperatorBase
    {
        // ========================
        // MonoBehaviour Lifecycle
        // ========================

        protected virtual void Update()
        {
            if (!isDeployed) return;

            if (IsCombatPaused)
            {
                // Between waves nothing ticks: no trap damage, skills or attacks. The attack stays charged
                // so the first attack of the next wave isn't delayed.
                trapDamageTimer = 0f;
                attackTimer = Mathf.Min(attackTimer + Time.deltaTime, GetAttackInterval());
                return;
            }

            if (deployedCell != null && deployedCell.IsAcidHazard)
            {
                trapDamageTimer += Time.deltaTime;
                while (trapDamageTimer >= AcidTileRules.DamageIntervalSeconds && isDeployed)
                {
                    trapDamageTimer -= AcidTileRules.DamageIntervalSeconds;
                    TrashTD.Combat.AcidDamageFlash.Flash(gameObject);
                    AudioManager.Instance?.PlaySfx(SfxId.TileTrapTick, 0.6f);
                    TakeTrueDamage(deployedCell.AcidDamagePerTick);
                }
                if (!isDeployed) return;
            }
            else
            {
                trapDamageTimer = 0f;
            }

            if (isStunned || isBound)
            {
                if (isStunned) UpdateStunStatus(Time.deltaTime);
                if (isBound) UpdateBindStatus(Time.deltaTime);
                return;
            }

            // Update skill cooldowns
            equippedSkill?.UpdateSkill(Time.deltaTime);

            // Attack timing: once charged, the attack stays ready until a target is available,
            // so an enemy entering range is hit immediately instead of waiting a full interval.
            float interval = GetAttackInterval();
            attackTimer = Mathf.Min(attackTimer + Time.deltaTime, interval);
            if (attackTimer >= interval)
            {
                currentTarget = FindTarget();
                if (currentTarget != null && CanAttackTarget(currentTarget))
                {
                    attackTimer = 0f;
                    Attack(currentTarget);
                }
            }
        }

        public virtual bool CanAttackTarget(EnemyBase target)
        {
            if (isStunned || isBound || target == null || target.IsDead || target.IsInsideHole) return false;
            if (data != null && !target.CanBeTargetedBy(data.position)) return false;
            if (!target.CanReceiveDamageFrom(this)) return false;
            return target.MovementType != EnemyMovementType.Air || CanHitAir || target.IsWithinMeleeReach;
        }

        // ========================
        // Targeting (overridden by subclasses)
        // ========================

        /// <summary>
        /// Find the best target to attack. Subclasses override this for
        /// class-specific targeting priority (e.g., lowest HP, closest, etc).
        /// </summary>
        protected abstract EnemyBase FindTarget();

        // ========================
        // Damage Handling
        // ========================

        /// <summary>
        /// Take damage from an enemy or effect.
        /// </summary>
        public virtual void TakeDamage(int rawATK, DamageType damageType, EnemyBase attacker = null)
        {
            if (IsCombatPaused || currentHP <= 0 || rawATK <= 0) return;

            var shield = GetComponent<BubbleShield>();
            if (shield != null && shield.TryConsume(rawATK, damageType))
            {
                return;
            }

            int calculatedDamage = Combat.DamageCalculator.CalculateDamage(rawATK, CurrentDEF, CurrentRES, damageType);
            int damage = Mathf.Max(0, ModifyDamageAfterMitigation(calculatedDamage, damageType, attacker));
            damage = AbsorbWithTemporaryHP(damage, damageType);
            int previousHP = currentHP;
            currentHP = Mathf.Max(0, currentHP - damage);
            int actualDamage = previousHP - currentHP;
            if (actualDamage > 0)
            {
                TrashTD.Combat.FloatingCombatNumber.Show(transform.position, actualDamage, damageType, false, this);
                TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
                TrashTD.Combat.HitReaction.Play(gameObject, transform.position, actualDamage, maxHP);
                OnDamageApplied(calculatedDamage, actualDamage, damageType, attacker);
            }

            if (currentHP <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Take true damage directly (ignoring DEF and RES).
        /// If ignoreShieldNegation is true (e.g. Stage 5 traffic collision),
        /// damage bypasses shield negation but still pops the bubble.
        /// If canDodge is true (e.g. Stage 2 falling items), operators with dodge (e.g. Stag-ger) can roll to dodge.
        /// Returns true if damage was dealt, false if prevented, dodged or absorbed.
        /// </summary>
        public virtual bool TakeTrueDamage(int damage, bool ignoreShieldNegation = false, bool canDodge = false)
        {
            if (IsCombatPaused || currentHP <= 0 || damage <= 0) return false;

            var shield = GetComponent<BubbleShield>();
            if (shield != null && shield.IsActive)
            {
                if (ignoreShieldNegation)
                {
                    shield.Pop(false);
                }
                else if (shield.TryConsume(damage, DamageType.True))
                {
                    return false;
                }
            }

            damage = AbsorbWithTemporaryHP(damage, DamageType.True);
            if (damage <= 0) return false;

            int previousHP = currentHP;
            currentHP = Mathf.Max(0, currentHP - damage);
            int actualDamage = previousHP - currentHP;
            if (actualDamage > 0)
            {
                TrashTD.Combat.FloatingCombatNumber.Show(transform.position, actualDamage, DamageType.True, false, this);
                TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
                TrashTD.Combat.HitReaction.Play(gameObject, transform.position, actualDamage, maxHP);
            }

            if (currentHP <= 0)
            {
                Die();
            }

            return actualDamage > 0;
        }

        public virtual bool TryTakeAttackDamage(int rawATK, DamageType damageType, EnemyBase attacker = null)
        {
            if (IsCombatPaused) return false;
            if (!Combat.StageCombatModifiers.TryAttackHit(transform.position))
                return false;

            TakeDamage(rawATK, damageType, attacker);
            return true;
        }

        public virtual bool TryTakeAreaAttackDamage(int rawATK, DamageType damageType, EnemyBase attacker = null)
        {
            if (IsCombatPaused) return false;
            if (!Combat.StageCombatModifiers.TryAttackHit(transform.position))
                return false;

            TakeDamage(rawATK, damageType, attacker);
            return true;
        }

        /// <summary>
        /// Heal this operator.
        /// </summary>
        public void Heal(int amount)
        {
            if (IsCombatPaused || !CanReceiveHealing) return;
            ApplyHealing(amount);
        }

        protected void HealSelf(int amount)
        {
            ApplyHealing(amount);
        }

        // ========================
        // Temporary HP
        // ========================

        private int temporaryHP;

        /// <summary>
        /// Temporary HP a buff has given this operator. It soaks up damage (every type, true damage included) before
        /// real HP does, can't be healed back, and is discarded by whatever granted it (see <see cref="ClearTemporaryHP"/>).
        /// </summary>
        public int TemporaryHP => temporaryHP;

        public void GrantTemporaryHP(int amount)
        {
            if (amount <= 0) return;
            temporaryHP += amount;
            RefreshHealthBar();
        }

        public void ClearTemporaryHP()
        {
            if (temporaryHP == 0) return;
            temporaryHP = 0;
            RefreshHealthBar();
        }

        /// <summary>Temporary HP shows as a yellow segment on the health bar (WorldHealthBar reads it from here).</summary>
        private void RefreshHealthBar()
        {
            if (maxHP > 0 && currentHP > 0) TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
        }

        /// <summary>Takes as much of the damage as the temporary HP can cover and returns what is left for real HP.</summary>
        private int AbsorbWithTemporaryHP(int damage, DamageType damageType)
        {
            if (temporaryHP <= 0 || damage <= 0) return damage;

            int absorbed = Mathf.Min(temporaryHP, damage);
            temporaryHP -= absorbed;
            TrashTD.Combat.FloatingCombatNumber.Show(transform.position, absorbed, damageType, false, this);
            RefreshHealthBar();
            return damage - absorbed;
        }

        private void ApplyHealing(int amount)
        {
            if (IsCombatPaused || amount <= 0 || currentHP <= 0) return;

            int previousHP = currentHP;
            currentHP = Mathf.Min(currentHP + amount, maxHP);
            int actualHealing = currentHP - previousHP;
            if (actualHealing > 0)
            {
                TrashTD.Combat.FloatingCombatNumber.Show(transform.position, actualHealing, DamageType.Physical, true, this);
                TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
                OnHealed(actualHealing);
            }
        }

        /// <summary>Called after this operator receives healing and HP increases.</summary>
        protected virtual void OnHealed(int amount) { }
    }
}
