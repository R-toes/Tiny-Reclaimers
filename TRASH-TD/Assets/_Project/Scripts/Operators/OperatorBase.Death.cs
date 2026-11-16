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
    // Dying, skills, upgrades, death stats and stat effect collection.
    public partial class OperatorBase
    {
        /// <summary>
        /// Handle operator death — retreat and destroy.
        /// </summary>
        protected virtual void Die()
        {
            if (!UnitSfx.Play(data, UnitSfxEvent.Death))
                AudioManager.Instance?.PlaySfx(SfxId.GameplayOperatorDeath);
            Retreat();
            TrashTD.Systems.OperatorManager.Instance?.HandleOperatorDeath(this);
            OnDeath();
            // TODO: Death animation, respawn cooldown timer
            Destroy(gameObject);
        }

        // ========================
        // Skill System
        // ========================

        /// <summary>
        /// Equip a skill (composition — any ISkill implementation can be attached).
        /// </summary>
        public void EquipSkill(ISkill skill)
        {
            equippedSkill = skill;
        }

        /// <summary>
        /// Activate the equipped skill if ready.
        /// </summary>
        public void ActivateSkill()
        {
            if (isStunned || isBound) return;
            if (equippedSkill != null && equippedSkill.IsReady())
            {
                equippedSkill.Activate(this);
            }
        }

        // ========================
        // Virtual Hooks (for subclass-specific behavior)
        // ========================

        /// <summary>Adjusts damage after normal mitigation and before HP is changed.</summary>
        protected virtual int ModifyDamageAfterMitigation(int damage, DamageType damageType, EnemyBase attacker) => damage;

        /// <summary>Called after a hit has actually reduced this operator's HP.</summary>
        protected virtual void OnDamageApplied(int calculatedDamage, int actualDamage, DamageType damageType, EnemyBase attacker) { }

        /// <summary>Called after successful deployment.</summary>
        protected virtual void OnDeployed() { }

        /// <summary>Called after retreat.</summary>
        protected virtual void OnRetreated()
        {
            HarmonyBuff harmony = GetComponent<HarmonyBuff>();
            if (harmony != null) harmony.Clear();
            isStunned = false;
            stunTimer = 0f;
            isBound = false;
            bindTimer = 0f;
            UpdateStunVisual();
        }

        /// <summary>
        /// Cards granted by an upgraded Drawgoo on death (2 cards).
        /// </summary>
        public static int RollUpgradedDrawgooCardCount(float roll = -1f)
        {
            return 2;
        }

        /// <summary>
        /// Gets the skill description for this operator, which may vary depending on rarity/upgrades.
        /// </summary>
        public virtual string GetSkillDescription() => GetSkillDescription(data, currentRarity);

        /// <summary>The skill text for an operator at a rarity, for cards not on the field (deck details).</summary>
        public static string GetSkillDescription(OperatorData data, OperatorRarity currentRarity)
        {
            if (data == null) return string.Empty;
            if (data.drawTwoCardsOnDeath)
            {
                if (currentRarity >= OperatorRarity.Star3)
                {
                    return "When Drawgoo dies, draw 2 2x Upgraded operator cards from the pool. Cannot be healed by other operators.";
                }
                if (currentRarity >= OperatorRarity.Star2)
                {
                    return "When Drawgoo dies, draw 2 1x Upgraded operator cards from the pool. Cannot be healed by other operators.";
                }
                return "When Drawgoo dies, draw 2 random operator cards from the pool. Cannot be healed by other operators.";
            }
            return data.skillDescription;
        }

        /// <summary>Called when the operator dies.</summary>
        protected virtual void OnDeath()
        {
            if (data == null || !data.drawTwoCardsOnDeath) return;

            TrashTD.Systems.CardDraftSystem draftSystem =
                TrashTD.Systems.CardDraftSystem.Instance != null
                    ? TrashTD.Systems.CardDraftSystem.Instance
                    : FindAnyObjectByType<TrashTD.Systems.CardDraftSystem>();
            if (draftSystem == null)
            {
                Debug.LogError($"{data.operatorName} died, but no CardDraftSystem is active to resolve its death skill.");
                return;
            }

            PlayAbilitySound();

            if (currentRarity >= OperatorRarity.Star3)
            {
                draftSystem.DrawRandomCardsToDeck(2, upgradeTiers: 2);
            }
            else if (currentRarity >= OperatorRarity.Star2)
            {
                draftSystem.DrawRandomCardsToDeck(2, upgradeTiers: 1);
            }
            else
            {
                draftSystem.DrawRandomCardsToDeck(2, upgradeTiers: 0);
            }
        }
        // ========================
        // Stat effects (the SHOW MORE panel)
        // ========================

        private struct ExternalStat
        {
            public string Source;
            public OperatorStat Stat;
            public int Amount;
        }

        // What other units have added to or taken from this operator's stats, per source (auras, hazards)
        private readonly List<ExternalStat> externalStats = new List<ExternalStat>();

        private void TrackExternalStat(string source, OperatorStat stat, int delta)
        {
            if (delta == 0) return;
            if (string.IsNullOrEmpty(source)) source = "Buff";

            for (int i = 0; i < externalStats.Count; i++)
            {
                if (externalStats[i].Source != source || externalStats[i].Stat != stat) continue;

                ExternalStat entry = externalStats[i];
                entry.Amount += delta;
                if (entry.Amount == 0) externalStats.RemoveAt(i);
                else externalStats[i] = entry;
                return;
            }

            externalStats.Add(new ExternalStat { Source = source, Stat = stat, Amount = delta });
        }

        /// <summary>What a rank-up added over the rank the card was drafted at (null when it has not been upgraded).</summary>
        public static StatEffect? GetRankUpEffect(OperatorData operatorData, OperatorRarity rarity)
        {
            if (operatorData == null || rarity <= operatorData.baseRarity) return null;

            OperatorRarity from = operatorData.baseRarity;
            int dHP = operatorData.GetScaledHP(rarity) - operatorData.GetScaledHP(from);
            int dATK = operatorData.GetScaledATK(rarity) - operatorData.GetScaledATK(from);
            int dDEF = operatorData.GetScaledDEF(rarity) - operatorData.GetScaledDEF(from);
            int dRES = operatorData.GetScaledRES(rarity) - operatorData.GetScaledRES(from);
            return new StatEffect(
                $"RANK UP  {(int)from}\u2605 > {(int)rarity}\u2605",
                $"+{dHP} HP  +{dATK} ATK  +{dDEF} DEF  +{dRES} RES",
                "HP ATK DEF RES");
        }

        /// <summary>The ATK this operator hits with right now, including conditions on its own attack (e.g. being isolated).</summary>
        public virtual int GetDisplayATK() => CurrentATK;

        /// <summary>
        /// Lists everything currently changing this operator's stats and where it comes from: a rank-up, Harmony, other
        /// operators' auras, the stage's hazards, and (in overrides) the operator's own passives and skills. Only effects
        /// that are active right now are listed. Overrides call base first, then add their own.
        /// </summary>
        public virtual void CollectStatEffects(List<StatEffect> effects)
        {
            if (data == null) return;

            // Rank-up: what merging gained over the rank this card was drafted at
            StatEffect? rankUp = GetRankUpEffect(data, currentRarity);
            if (rankUp.HasValue) effects.Add(rankUp.Value);

            // Harmony (Flurmonica)
            if (harmonyStacks > 0)
            {
                string percent = (HarmonyBonus * 100f).ToString("0.#") + "%";
                string description;
                string stats;
                switch (data.operatorClass)
                {
                    case OperatorClass.Guard: description = $"+{percent} RES"; stats = "RES"; break;
                    case OperatorClass.Defender: description = $"+{percent} DEF"; stats = "DEF"; break;
                    case OperatorClass.Specialist: description = $"+{percent} HP"; stats = "HP"; break;
                    case OperatorClass.Sniper:
                    case OperatorClass.Medic: description = $"+{percent} speed"; stats = "ATK SPD"; break;
                    case OperatorClass.Caster: description = $"+{percent} Arts dmg"; stats = "ATK"; break;
                    default: description = $"+{percent}"; stats = ""; break;
                }
                effects.Add(new StatEffect($"HARMONY x{harmonyStacks}", description, stats));
            }

            // Auras and hazards from other units: Bubblets (DEF/RES), Coalesce (max HP), Oculoid Carrier (block)
            foreach (ExternalStat external in externalStats)
            {
                string label = external.Stat switch
                {
                    OperatorStat.HP => "HP",
                    OperatorStat.DEF => "DEF",
                    OperatorStat.RES => "RES",
                    OperatorStat.Block => "block",
                    OperatorStat.ATK => "ATK",
                    _ => "ATK SPD"
                };
                string signed = external.Amount > 0 ? $"+{external.Amount}" : external.Amount.ToString();
                effects.Add(new StatEffect(external.Source, $"{signed} {label}", label.ToUpperInvariant(), external.Amount > 0));
            }

            // Stage hazards
            if (Combat.StageCombatModifiers.IsSandstormActive)
                effects.Add(new StatEffect("SANDSTORM", "Interval x0.95", "ATK SPD"));
        }
    }
}
