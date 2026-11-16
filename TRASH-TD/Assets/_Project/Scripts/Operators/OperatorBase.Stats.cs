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
    // Range and attack interval, stat modifiers and blocking.
    public partial class OperatorBase
    {
        public virtual Vector2Int[] GetRangePattern() => data != null ? data.rangePattern : null;
        public virtual Vector2Int[] GetTalentRangePattern() => data != null ? data.GetTalentRangePattern() : null;
        public virtual float GetAttackInterval() =>
            Combat.StageCombatModifiers.GetAttackInterval(data.attackInterval) / HarmonyAttackSpeedMultiplier;

        // ========================
        // Harmony (Flurmonica's buff, see HarmonyBuff)
        // ========================

        private int harmonyStacks;
        private float harmonyBonusPerStack = HarmonyBuff.BonusPerStack;
        private int harmonyMaxStacks = HarmonyBuff.MaxStacks;
        private int harmonyDEF;
        private int harmonyRES;
        private int harmonyHP;

        public int HarmonyStacks => harmonyStacks;
        public float HarmonyBonusPerStack => harmonyBonusPerStack;
        public float HarmonyBonus => harmonyBonusPerStack * harmonyStacks;

        /// <summary>Snipers and Medics attack (heal) faster per Harmony stack: the interval is divided by this.</summary>
        public float HarmonyAttackSpeedMultiplier =>
            data != null && (data.operatorClass == OperatorClass.Sniper || data.operatorClass == OperatorClass.Medic)
                ? 1f + HarmonyBonus
                : 1f;

        /// <summary>Casters deal more Arts damage per Harmony stack, after RES (applied in EnemyBase.TryTakeAttackDamage).</summary>
        public float OutgoingArtsDamageMultiplier =>
            data != null && data.operatorClass == OperatorClass.Caster ? 1f + HarmonyBonus : 1f;

        /// <summary>
        /// Sets the Harmony stack count (HarmonyBuff calls this). Each stack adds a percentage of the rarity-scaled base
        /// stat for this operator's class: Guard RES, Defender DEF, Specialist max HP (current HP rises with it
        /// and is clamped when it drops). Snipers, Medics and Casters read the count through the multipliers above.
        /// A Framiliar copy is the copied operator's class, so it gets that class's bonus.
        /// </summary>
        public void SetHarmonyStacks(int stacks)
        {
            SetHarmonyStacks(stacks, harmonyBonusPerStack > 0f ? harmonyBonusPerStack : HarmonyBuff.BonusPerStack, harmonyMaxStacks > 0 ? harmonyMaxStacks : HarmonyBuff.MaxStacks);
        }

        public void SetHarmonyStacks(int stacks, float bonusPerStack, int maxStacks = HarmonyBuff.MaxStacks)
        {
            if (data == null) return;

            harmonyBonusPerStack = bonusPerStack;
            harmonyMaxStacks = Mathf.Max(HarmonyBuff.MaxStacks, maxStacks);
            harmonyStacks = Mathf.Clamp(stacks, 0, harmonyMaxStacks);
            float bonus = HarmonyBonus;
            OperatorClass operatorClass = data.operatorClass;

            int newDEF = operatorClass == OperatorClass.Defender ? Mathf.RoundToInt(data.GetScaledDEF(currentRarity) * bonus) : 0;
            int newRES = operatorClass == OperatorClass.Guard ? Mathf.RoundToInt(data.GetScaledRES(currentRarity) * bonus) : 0;
            int newHP = operatorClass == OperatorClass.Specialist ? Mathf.RoundToInt(data.GetScaledHP(currentRarity) * bonus) : 0;

            currentDEF += newDEF - harmonyDEF;
            currentRES += newRES - harmonyRES;
            harmonyDEF = newDEF;
            harmonyRES = newRES;

            int hpDelta = newHP - harmonyHP;
            harmonyHP = newHP;
            if (hpDelta == 0) return;

            maxHP += hpDelta;
            if (currentHP <= 0) return;
            currentHP = hpDelta > 0 ? currentHP + hpDelta : Mathf.Max(1, Mathf.Min(currentHP, maxHP));
            if (currentHP < maxHP || GetComponent<WorldHealthBar>() != null)
                TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
        }

        /// <summary>
        /// Modifies this operator's maximum HP by delta (e.g. Coalesce aura, buffs).
        /// If delta > 0, current HP increases by the same amount. If delta < 0, current HP is clamped to new maxHP.
        /// </summary>
        public void ModifyMaxHP(int delta, string source = null)
        {
            if (delta == 0) return;
            TrackExternalStat(source, OperatorStat.HP, delta);
            maxHP = Mathf.Max(1, maxHP + delta);
            if (currentHP > 0)
            {
                currentHP = delta > 0 ? currentHP + delta : Mathf.Max(1, Mathf.Min(currentHP, maxHP));
                TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
            }
        }

        /// <summary>
        /// Modifies this operator's DEF by delta (e.g. Bubblets aura, buffs).
        /// </summary>
        public void ModifyDEF(int delta, string source = null)
        {
            if (delta == 0) return;
            TrackExternalStat(source, OperatorStat.DEF, delta);
            currentDEF += delta;
        }

        /// <summary>
        /// Modifies this operator's RES by delta (e.g. Bubblets aura, buffs).
        /// </summary>
        public void ModifyRES(int delta, string source = null)
        {
            if (delta == 0) return;
            TrackExternalStat(source, OperatorStat.RES, delta);
            currentRES += delta;
        }
        public DamageType GetDamageType() => data.damageType;

        /// <summary>
        /// Get the relevant mitigation stat from the target based on damage type.
        /// Physical → DEF, Arts → RES.
        /// </summary>
        protected int GetTargetMitigation(EnemyBase target)
        {
            return data.damageType == DamageType.Physical ? target.CurrentDEF : target.CurrentRES;
        }

        // ========================
        // IBlocker Implementation
        // ========================

        public virtual bool TryBlock(EnemyBase enemy)
        {
            if (enemy == null || !isDeployed || isStunned || isBound) return false;
            int freeSlots = GetBlockCount() - GetCurrentBlockCount();
            if (freeSlots < GetBlockWeight(enemy)) return false;
            if (enemy.Data != null && enemy.Data.isUnblockable) return false;
            if (enemy is Enemies.OculoidCarrierEnemy carrier && freeSlots <= carrier.BlockCountReduction) return false;

            blockedEnemies.Add(enemy);
            enemy.OnBlocked(this);
            return true;
        }

        public void ReleaseBlock(EnemyBase enemy)
        {
            if (blockedEnemies.Remove(enemy))
            {
                enemy.OnUnblocked();
            }
        }

        private int blockCountModifier = 0;
        public int BlockCountModifier => blockCountModifier;

        public void ModifyBlockCount(int delta, string source = null)
        {
            TrackExternalStat(source, OperatorStat.Block, delta);
            blockCountModifier += delta;
            while (GetCurrentBlockCount() > GetBlockCount() && blockedEnemies.Count > 0)
            {
                ReleaseBlock(blockedEnemies[blockedEnemies.Count - 1]);
            }
        }

        public virtual int GetBlockCount() => (isStunned || isBound || data == null) ? 0 : GetMaxBlockCount();

        /// <summary>
        /// Block slots this operator has when it is not stunned or bound: the data value plus every modifier and rank
        /// upgrade (operators with such upgrades override this, not <see cref="GetBlockCount"/>). The info panel shows it.
        /// </summary>
        public virtual int GetMaxBlockCount() => data == null ? 0 : Mathf.Max(0, data.blockCount + blockCountModifier);

        /// <summary>Block slots in use: each blocked enemy takes its EnemyData.blockWeight (heavy units take 2).</summary>
        public int GetCurrentBlockCount()
        {
            int used = 0;
            for (int i = 0; i < blockedEnemies.Count; i++)
                used += GetBlockWeight(blockedEnemies[i]);
            return used;
        }

        public static int GetBlockWeight(EnemyBase enemy) =>
            enemy != null && enemy.Data != null ? Mathf.Max(1, enemy.Data.blockWeight) : 1;
    }
}
