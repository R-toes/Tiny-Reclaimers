using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Core.Grid;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.UI
{
    // The selected-operator label and the details shown for the selected operator or deck card.
    public partial class GameplayHUDUI
    {
        public void SetSelectedOperatorName(string operatorName, Vector3 worldPosition)
        {
            if (selectedOperatorLabelRoot == null || selectedOperatorNameText == null || canvas == null) return;
            if (string.IsNullOrEmpty(operatorName))
            {
                selectedLabelShown = false;
                selectedOperatorLabelRoot.SetActive(false);
                retreatOperatorButton.gameObject.SetActive(false);
                selectedOperatorDetailsPanel.SetActive(false);
                selectedOperatorDetailsTarget = null;
                return;
            }

            Camera gameCamera = Camera.main;
            if (gameCamera == null) return;

            selectedOperatorNameText.text = operatorName;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(gameCamera, worldPosition);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, GetCanvasCamera(), out Vector2 localPosition))
            {
                PlaceSelectedOperatorLabel(localPosition, true);
            }
        }

        /// <summary>
        /// Shows the info tab with its bottom-centre at a canvas point, clamped on-screen. Deck cards show it
        /// without the retreat button.
        /// </summary>
        private void PlaceSelectedOperatorLabel(Vector2 localPosition, bool showRetreat)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform labelRect = selectedOperatorLabelRoot.GetComponent<RectTransform>();
            RectTransform retreatRect = retreatOperatorButton.GetComponent<RectTransform>();

            float halfW = labelRect.sizeDelta.x * 0.5f;
            float panelH = labelRect.sizeDelta.y;

            // Screen clamping: keep the panel and retreat button entirely on-screen
            float minX = -canvasRect.rect.width * 0.5f + halfW + 10f;
            float maxX = canvasRect.rect.width * 0.5f - halfW - 60f;
            float minY = -canvasRect.rect.height * 0.5f + 10f;
            float maxY = canvasRect.rect.height * 0.5f - panelH - 10f;

            localPosition.x = Mathf.Clamp(localPosition.x, minX, maxX);
            localPosition.y = Mathf.Clamp(localPosition.y, minY, maxY);

            labelRect.anchoredPosition = localPosition;
            selectedOperatorLabelRoot.SetActive(true);
            selectedOperatorShowMoreButton.gameObject.SetActive(true);

            // Retreat button positioned to the right of the tab, vertically aligned with Row 1-4
            float retreatX = localPosition.x + halfW + 28f;
            float retreatY = localPosition.y + panelH - 53f;
            retreatRect.anchoredPosition = new Vector2(retreatX, retreatY);
            retreatOperatorButton.gameObject.SetActive(showRetreat);

            if (!selectedLabelShown)
            {
                selectedLabelShown = true;
                labelPopTime = 0f;
                snapHealthFill = true;
                ApplyLabelPop(0f);
            }
        }

        public void SetSelectedOperatorName(
            string operatorName,
            int currentHP,
            int maxHP,
            int currentATK,
            OperatorRarity rarity,
            OperatorRarity baseRarity,
            Vector3 worldPosition)
        {
            // The skill text lives only in the SHOW MORE details panel (RefreshSelectedOperatorDetails).
            SetSelectedOperatorName(operatorName, worldPosition);
            if (string.IsNullOrEmpty(operatorName)) return;
            ApplySelectedOperatorStats(currentHP, maxHP, currentATK, rarity, baseRarity);
        }

        private void ApplySelectedOperatorStats(int currentHP, int maxHP, int currentATK, OperatorRarity rarity, OperatorRarity baseRarity)
        {
            int upgradeLevels = Mathf.Clamp((int)rarity - (int)baseRarity, 0, 2);
            selectedOperatorUpgradeBadgeGraphic.SetArrowCount(upgradeLevels);
            selectedOperatorUpgradeBadge.SetActive(upgradeLevels > 0);
            float healthRatio = maxHP > 0 ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;
            selectedOperatorHealthText.text = $"HP {currentHP} / {maxHP}";
            if (selectedOperatorAtkText != null)
            {
                selectedOperatorAtkText.text = $"ATK {currentATK}";
            }
            selectedOperatorRarityText.text = new string('★', Mathf.Clamp((int)rarity, 1, 5));

            selectedHealthTarget = healthRatio;
            if (snapHealthFill)
            {
                selectedHealthDisplayed = healthRatio;
                snapHealthFill = false;
                ApplyHealthFill();
            }

            selectedOperatorHealthFill.color = healthRatio <= 0.3f
                ? new Color(1f, 0.2f, 0.18f, 1f)
                : healthRatio <= 0.6f
                    ? new Color(1f, 0.75f, 0.15f, 1f)
                    : new Color(0.25f, 0.95f, 0.36f, 1f);
        }

        public void SetSelectedOperatorName(
            string operatorName,
            int currentHP,
            int maxHP,
            OperatorRarity rarity,
            OperatorRarity baseRarity,
            Vector3 worldPosition)
        {
            SetSelectedOperatorName(operatorName, currentHP, maxHP, 0, rarity, baseRarity, worldPosition);
        }

        private void ApplyHealthFill()
        {
            if (selectedOperatorHealthFillRect == null) return;
            selectedOperatorHealthFillRect.anchorMax = new Vector2(Mathf.Clamp01(selectedHealthDisplayed), 1f);
        }

        private void ApplyLabelPop(float k)
        {
            float s = Mathf.LerpUnclamped(0.8f, 1f, EaseOutBack(k));
            if (selectedOperatorLabelRoot != null) selectedOperatorLabelRoot.transform.localScale = new Vector3(s, s, 1f);
            if (retreatFeedback != null) retreatFeedback.introScale = s;
        }

        private void RefreshSelectedOperatorInfo()
        {
            OperatorBase selectedOperator = operatorManager != null ? operatorManager.SelectedOperator : null;
            if (selectedOperator == null || !selectedOperator.IsDeployed || selectedOperator.DeployedCell == null || gridManager == null)
            {
                HideFramiliarCopyMenu();
                if (!RefreshDeckCardInfo()) SetSelectedOperatorName(string.Empty, Vector3.zero);
                return;
            }

            // A field operator was selected: its tab replaces the deck card's
            if (deckInfoSlot >= 0) ClearDeckCardInfo();

            if (selectedOperator is TrashTD.Operators.FramiliarOperator framiliar && !framiliar.IsCopied)
            {
                ShowFramiliarCopyMenu(framiliar);
            }
            else
            {
                HideFramiliarCopyMenu();
            }

            // The open details panel follows the operator: buffs come and go, so its stats and effects refresh live
            if (selectedOperatorDetailsPanel != null && selectedOperatorDetailsPanel.activeSelf &&
                selectedOperatorDetailsTarget == selectedOperator && Time.unscaledTime >= detailsLiveRefreshAt)
            {
                detailsLiveRefreshAt = Time.unscaledTime + 0.2f;
                UpdateSelectedOperatorDetails(selectedOperator);
            }

            selectedOperatorDetailsTarget = selectedOperator;
            Vector3 labelPosition = selectedOperator.DeployedCell.WorldPosition + Vector3.up * gridManager.CellSize * 0.7f;
            SetSelectedOperatorName(
                selectedOperator.Data.operatorName,
                selectedOperator.CurrentHP,
                selectedOperator.MaxHP,
                selectedOperator.CurrentATK,
                selectedOperator.CurrentRarity,
                selectedOperator.Data.baseRarity,
                labelPosition);
        }

        private void ToggleSelectedOperatorDetails()
        {
            if (selectedOperatorDetailsPanel == null) return;
            OperatorBase selectedOperator = operatorManager != null ? operatorManager.SelectedOperator : null;
            bool hasOperator = selectedOperator != null && selectedOperator.Data != null;
            bool hasDeckCard = !hasOperator && deckInfoCard != null && deckInfoCard.operatorData != null;
            if (!hasOperator && !hasDeckCard) return;

            bool show = !selectedOperatorDetailsPanel.activeSelf;
            selectedOperatorDetailsPanel.SetActive(show);
            if (!show) return;
            detailsSkillShown = null; // reopening starts at the top of the ability text
            if (hasOperator) UpdateSelectedOperatorDetails(selectedOperator);
            else UpdateDeckCardDetails(deckInfoCard);
        }

        private void UpdateSelectedOperatorDetails(OperatorBase op)
        {
            if (op == null || op.Data == null) return;

            if (selectedOperatorDetailsTarget != op) detailsSkillShown = null; // a different operator starts at the top of its text
            selectedOperatorDetailsTarget = op;
            UpdateOperatorDetails(op.Data, op.CurrentRarity, op.GetSkillDescription(), op);
        }

        private void UpdateDeckCardDetails(DraftCard card)
        {
            if (card == null || card.operatorData == null) return;

            selectedOperatorDetailsTarget = null;
            detailsSkillShown = null;
            UpdateOperatorDetails(
                card.operatorData,
                card.rarity,
                OperatorBase.GetSkillDescription(card.operatorData, card.rarity));
        }

        /// <summary>Fills the SHOW MORE panel. Shared by field operators and deck cards.</summary>
        private void UpdateOperatorDetails(OperatorData data, OperatorRarity rarity, string skillDesc, OperatorBase op = null)
        {
            selectedOperatorDetailsPortrait.sprite = data.portrait;
            selectedOperatorDetailsPortrait.enabled = data.portrait != null;
            selectedOperatorDetailsPortrait.color = Color.white;
            selectedOperatorDetailsName.text = data.operatorName;
            selectedOperatorDetailsClassIcon.sprite = GetClassIconSprite(data.operatorClass);
            selectedOperatorDetailsClassIcon.color = Color.white;
            selectedOperatorDetailsClassIcon.enabled = selectedOperatorDetailsClassIcon.sprite != null;
            selectedOperatorDetailsChipLabels[0].text = data.operatorClass.ToString().ToUpperInvariant();
            selectedOperatorDetailsChipLabels[1].text = data.position == OperatorPosition.Melee ? "MELEE" : "RANGED";
            Color classColor = GetDraftClassColor(data.operatorClass);
            Color positionColor = data.position == OperatorPosition.Melee
                ? new Color(0.95f, 0.60f, 0.25f, 1f)
                : new Color(0.35f, 0.78f, 0.92f, 1f);
            Color damageColor = data.damageType == DamageType.Arts
                ? new Color(0.70f, 0.50f, 1.00f, 1f)
                : new Color(0.90f, 0.45f, 0.35f, 1f);
            selectedOperatorDetailsChipLabels[2].text = data.damageType == DamageType.Arts ? "ARTS" : "PHYSICAL";
            Color[] chipColors = { classColor, positionColor, damageColor };
            Color tileColor = new Color(0.07f, 0.085f, 0.115f, 1f);
            for (int i = 0; i < chipColors.Length; i++)
            {
                selectedOperatorDetailsChipLabels[i].color = Color.Lerp(chipColors[i], Color.white, 0.35f);
                selectedOperatorDetailsChipBackgrounds[i].color = Color.Lerp(tileColor, chipColors[i], 0.22f);
                selectedOperatorDetailsChipOutlines[i].effectColor = new Color(chipColors[i].r, chipColors[i].g, chipColors[i].b, 0.55f);
            }

            int stars = Mathf.Clamp((int)rarity, 1, 5);
            Color rarityColor = GetOperatorRarityColor(stars);
            selectedOperatorDetailsAccent.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.35f);
            selectedOperatorDetailsRarity.color = rarityColor;
            selectedOperatorDetailsRarity.text = new string('★', stars) + "<color=#3C424D>" + new string('☆', 5 - stars) + "</color>";
            // Stats: a field operator shows its current maximums (rank-ups, auras, passives and skills included, but not
            // damage taken); a deck card shows what it would have. Deltas compare with the rank it was drafted at.
            OperatorRarity draftedRank = data.baseRarity;
            int hp, atk, def, res, block;
            float interval;
            if (op != null)
            {
                hp = op.MaxHP;
                atk = op.GetDisplayATK();
                def = op.CurrentDEF;
                res = op.CurrentRES;
                block = op.GetMaxBlockCount();
                interval = op.GetAttackInterval();
            }
            else
            {
                hp = data.GetScaledHP(rarity);
                atk = data.GetScaledATK(rarity);
                def = data.GetScaledDEF(rarity);
                res = data.GetScaledRES(rarity);
                block = data.blockCount;
                interval = data.attackInterval;
            }

            SetDetailStat(0, hp, data.GetScaledHP(draftedRank));
            SetDetailStat(1, atk, data.GetScaledATK(draftedRank));
            SetDetailStat(2, def, data.GetScaledDEF(draftedRank));
            SetDetailStat(3, res, data.GetScaledRES(draftedRank));
            SetDetailStat(4, block, data.blockCount);
            SetDetailSpeed(5, interval, data.attackInterval);

            statEffectsBuffer.Clear();
            if (op != null)
            {
                op.CollectStatEffects(statEffectsBuffer);
            }
            else
            {
                StatEffect? rankUp = OperatorBase.GetRankUpEffect(data, rarity);
                if (rankUp.HasValue) statEffectsBuffer.Add(rankUp.Value);
            }
            ShowOperatorEffects(statEffectsBuffer);
            selectedOperatorDetailsRoles.text = data.roleTags != null && data.roleTags.Length > 0
                ? string.Join(" / ", data.roleTags)
                : GetOperatorDraftDescription(data.operatorClass);
            bool hasSkill = !string.IsNullOrWhiteSpace(skillDesc);
            selectedOperatorDetailsSkillHeading.gameObject.SetActive(hasSkill);
            selectedOperatorDetailsSkillScroll.gameObject.SetActive(hasSkill);
            if (hasSkill)
            {
                string skillText = skillDesc.Trim();
                if (skillText != detailsSkillShown)
                {
                    // Only a new text goes back to the top, so the panel can refresh live without losing the reader's place
                    detailsSkillShown = skillText;
                    selectedOperatorDetailsSkill.text = skillText;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(selectedOperatorDetailsSkill.rectTransform);
                    selectedOperatorDetailsSkillScroll.verticalNormalizedPosition = 1f;
                }
            }
        }

        private static readonly Color StatNeutralColor = new Color(0.95f, 0.95f, 0.98f, 1f);
        private static readonly Color StatUpColor = new Color(0.55f, 1f, 0.62f, 1f);
        private static readonly Color StatDownColor = new Color(1f, 0.5f, 0.45f, 1f);

        /// <summary>One whole-number stat tile: the value, and the change against <paramref name="baseline"/> beside it.</summary>
        private void SetDetailStat(int index, int value, int baseline)
        {
            int diff = value - baseline;
            selectedOperatorDetailsStatValues[index].text = value.ToString();
            selectedOperatorDetailsStatValues[index].color = diff > 0 ? StatUpColor : diff < 0 ? StatDownColor : StatNeutralColor;
            selectedOperatorDetailsStatDeltas[index].text = diff == 0 ? string.Empty : (diff > 0 ? "+" : string.Empty) + diff;
            selectedOperatorDetailsStatDeltas[index].color = diff > 0 ? StatUpColor : StatDownColor;
        }

        /// <summary>The attack interval tile: a shorter interval is better, so the change is shown as a faster / slower percentage.</summary>
        private void SetDetailSpeed(int index, float interval, float baseline)
        {
            selectedOperatorDetailsStatValues[index].text = $"{interval:0.##}s";
            int percent = interval > 0.0001f ? Mathf.RoundToInt((baseline / interval - 1f) * 100f) : 0;
            selectedOperatorDetailsStatValues[index].color = percent > 0 ? StatUpColor : percent < 0 ? StatDownColor : StatNeutralColor;
            selectedOperatorDetailsStatDeltas[index].text = percent == 0 ? string.Empty : (percent > 0 ? "+" : string.Empty) + percent + "%";
            selectedOperatorDetailsStatDeltas[index].color = percent > 0 ? StatUpColor : StatDownColor;
        }

        private static string GetOperatorDraftDescription(OperatorClass opClass)
        {
            return opClass switch
            {
                OperatorClass.Guard => "Melee Combatant / Physical DPS",
                OperatorClass.Defender => "Heavy Defense / Blocks 3",
                OperatorClass.Sniper => "Ranged Sniper / High Range",
                OperatorClass.Caster => "Arts Damage / Magic Attacks",
                OperatorClass.Medic => "Combat Support / Restores HP",
                OperatorClass.Specialist => "Specialist / Fast Redeploy & Mimic",
                _ => "Operator"
            };
        }

        private static Color GetDraftClassColor(OperatorClass opClass)
        {
            return opClass switch
            {
                OperatorClass.Guard => new Color(0.80f, 0.28f, 0.28f, 1f),
                OperatorClass.Defender => new Color(0.30f, 0.45f, 0.85f, 1f),
                OperatorClass.Sniper => new Color(0.30f, 0.75f, 0.30f, 1f),
                OperatorClass.Caster => new Color(0.70f, 0.30f, 0.85f, 1f),
                OperatorClass.Medic => new Color(0.80f, 0.78f, 0.30f, 1f),
                OperatorClass.Specialist => new Color(0.20f, 0.75f, 0.70f, 1f),
                _ => new Color(0.5f, 0.7f, 0.9f, 1f)
            };
        }

        private static Color GetOperatorRarityColor(int stars)
        {
            return stars switch
            {
                1 => new Color(0.35f, 0.85f, 0.45f, 1f),
                2 => new Color(0.35f, 0.65f, 1f, 1f),
                3 => new Color(1f, 0.82f, 0.25f, 1f),
                4 => new Color(0.75f, 0.45f, 1f, 1f),
                _ => new Color(1f, 0.3f, 0.3f, 1f)
            };
        }
    }
}
