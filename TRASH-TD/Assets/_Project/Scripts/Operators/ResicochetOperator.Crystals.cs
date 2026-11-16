using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Systems;

namespace TrashTD.Operators
{
    // Charges and the crystal auras.
    public partial class ResicochetOperator
    {
        public void SetCharges(int amount)
        {
            charges = Mathf.Clamp(amount, 1, MaxChargeCount);
            chargeTimer = 0f;
            UpdateChargeVisuals();
        }

        public void AddCharge()
        {
            if (charges < MaxChargeCount)
            {
                charges++;
                chargeTimer = 0f;
                UpdateChargeVisuals();
                FloatingCombatNumber.ShowText(transform.position, $"CHARGE {charges}!", new Color(0.4f, 0.9f, 1f));
                PlayAbilitySound();
            }
        }

        public void UpdateChargeVisuals()
        {
            Sprite[] activeFrames = charges switch
            {
                1 => charge1Frames,
                2 => charge2Frames,
                3 => charge3Frames,
                _ => charge1Frames
            };

            if (activeFrames == null || activeFrames.Length == 0) return;

            if (spriteAnim == null)
            {
                spriteAnim = GetComponent<OperatorSpriteAnimation>();
            }

            if (spriteAnim != null)
            {
                spriteAnim.Configure(activeFrames);
            }

            if (opSpriteRenderer == null)
            {
                opSpriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (opSpriteRenderer != null && activeFrames[0] != null)
            {
                opSpriteRenderer.sprite = activeFrames[0];
            }

            if (crystalAuraOuter != null && isDeployed)
            {
                UpdateCrystalAuras();
            }
        }

        #region Shadeslice-Style Crystal Aura & Outline Effect

        private static Material s_CrystalAuraMaterial;
        public static Material GetCrystalAuraMaterial()
        {
            if (s_CrystalAuraMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("TrashTD/SpriteFlash");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    s_CrystalAuraMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
            }
            return s_CrystalAuraMaterial;
        }

        public void CreateCrystalAuras()
        {
            if (opSpriteRenderer == null)
            {
                opSpriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (opSpriteRenderer == null) return;

            // Ensure SortingGroup exists on the unit root so child order offsets sort strictly within this unit
            var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sg == null)
            {
                sg = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                sg.sortingLayerID = GridSorting.UnitsLayerId;
            }

            int baseOrder = Mathf.Max(5, opSpriteRenderer.sortingOrder);
            opSpriteRenderer.sortingOrder = baseOrder;

            // Shadeslice outer & inner aura layers (strictly behind body sprite: -2, -1)
            crystalAuraOuter = CreateAuraLayer("CrystalAuraOuter", baseOrder - 2, Charge1ProjectileColor);
            crystalAuraInner = CreateAuraLayer("CrystalAuraInner", baseOrder - 1, Charge1ProjectileColor);

            // 1-pixel cardinal outline layers (strictly behind body sprite: -1)
            crystalOutlineLeft = CreateAuraLayer("CrystalOutlineLeft", baseOrder - 1, Charge1ProjectileColor);
            crystalOutlineRight = CreateAuraLayer("CrystalOutlineRight", baseOrder - 1, Charge1ProjectileColor);
            crystalOutlineUp = CreateAuraLayer("CrystalOutlineUp", baseOrder - 1, Charge1ProjectileColor);
            crystalOutlineDown = CreateAuraLayer("CrystalOutlineDown", baseOrder - 1, Charge1ProjectileColor);
        }

        private SpriteRenderer CreateAuraLayer(string objectName, int sortingOrder, Color color)
        {
            Transform existing = transform.Find(objectName);
            GameObject auraObj;
            SpriteRenderer sr;

            if (existing != null)
            {
                auraObj = existing.gameObject;
                sr = auraObj.GetComponent<SpriteRenderer>();
                if (sr == null) sr = auraObj.AddComponent<SpriteRenderer>();
            }
            else
            {
                auraObj = new GameObject(objectName);
                auraObj.transform.SetParent(transform, false);
                auraObj.transform.localPosition = Vector3.zero;
                sr = auraObj.AddComponent<SpriteRenderer>();
            }

            sr.color = color;
            sr.sortingOrder = sortingOrder;
            if (opSpriteRenderer != null)
            {
                sr.sortingLayerID = opSpriteRenderer.sortingLayerID;
                sr.flipX = opSpriteRenderer.flipX;
                sr.flipY = opSpriteRenderer.flipY;
            }
            var auraMat = GetCrystalAuraMaterial();
            if (auraMat != null) sr.sharedMaterial = auraMat;
            else if (opSpriteRenderer != null && opSpriteRenderer.sharedMaterial != null) sr.sharedMaterial = opSpriteRenderer.sharedMaterial;
            sr.enabled = isDeployed;
            return sr;
        }

        public void SetCrystalAuraEnabled(bool enabled)
        {
            if (crystalAuraOuter != null) crystalAuraOuter.enabled = enabled;
            if (crystalAuraInner != null) crystalAuraInner.enabled = enabled;
            if (crystalOutlineLeft != null) crystalOutlineLeft.enabled = enabled;
            if (crystalOutlineRight != null) crystalOutlineRight.enabled = enabled;
            if (crystalOutlineUp != null) crystalOutlineUp.enabled = enabled;
            if (crystalOutlineDown != null) crystalOutlineDown.enabled = enabled;
        }

        public void UpdateCrystalAuras()
        {
            if (!isDeployed) return;
            if (crystalAuraOuter == null)
            {
                CreateCrystalAuras();
                SetCrystalAuraEnabled(true);
            }
            if (crystalAuraOuter == null) return;

            // Don't show while Framiliar's glitch transition is actively playing
            if (TryGetComponent<FramiliarTransformTransition>(out var trans) && trans != null && !trans.IsFinished) return;

            if (opSpriteRenderer == null)
            {
                opSpriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (opSpriteRenderer == null) return;

            if (!AreFramesValid(crystalCharge1Frames) || !AreFramesValid(crystalCharge2Frames) || !AreFramesValid(crystalCharge3Frames))
            {
                EnsureCrystalFramesLoaded(opSpriteRenderer.sprite != null ? opSpriteRenderer.sprite.texture : null);
            }

            float time = Time.time;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 5f);
            float flicker = Mathf.Sin(time * 13f) * 0.025f;

            // Determine current animation frame index (0..3) from the operator sprite
            int frameIndex = 0;
            Sprite currentOpSprite = opSpriteRenderer.sprite;
            Sprite[] activeOpFrames = charges switch
            {
                1 => charge1Frames,
                2 => charge2Frames,
                3 => charge3Frames,
                _ => charge1Frames
            };
            if (activeOpFrames != null && currentOpSprite != null)
            {
                for (int i = 0; i < activeOpFrames.Length; i++)
                {
                    if (activeOpFrames[i] == currentOpSprite)
                    {
                        frameIndex = i;
                        break;
                    }
                }
            }

            // Pick the crystal-only sprite for current charges and animation frame
            Sprite[] activeCrystalFrames = charges switch
            {
                1 => crystalCharge1Frames,
                2 => crystalCharge2Frames,
                3 => crystalCharge3Frames,
                _ => crystalCharge1Frames
            };

            Sprite crystalSprite = (AreFramesValid(activeCrystalFrames) && frameIndex < activeCrystalFrames.Length)
                ? activeCrystalFrames[frameIndex]
                : (AreFramesValid(activeCrystalFrames) ? activeCrystalFrames[0] : null);

            bool isFlipped = opSpriteRenderer.flipX;

            Color outerColor;
            Color innerColor;

            if (charges == 1)
            {
                // Vibrant Pulsing Amber/Orange (Charge 1)
                outerColor = new Color(
                    1.0f,
                    Mathf.Lerp(0.40f, 0.70f, pulse),
                    0.05f,
                    0.45f + pulse * 0.20f + flicker);
                innerColor = new Color(
                    1.0f,
                    Mathf.Lerp(0.60f, 0.90f, pulse),
                    0.15f,
                    0.75f + pulse * 0.20f);
            }
            else if (charges == 2)
            {
                // Vibrant Emerald Green (Charge 2)
                outerColor = new Color(
                    0.08f,
                    Mathf.Lerp(0.80f, 1.0f, pulse),
                    0.25f,
                    0.45f + pulse * 0.20f + flicker);
                innerColor = new Color(
                    0.35f,
                    1.0f,
                    Mathf.Lerp(0.50f, 0.80f, pulse),
                    0.75f + pulse * 0.20f);
            }
            else
            {
                // Radiant Azure/Blue (Charge 3)
                outerColor = new Color(
                    0.15f,
                    Mathf.Lerp(0.55f, 0.85f, pulse),
                    1.0f,
                    0.45f + pulse * 0.20f + flicker);
                innerColor = new Color(
                    0.40f,
                    Mathf.Lerp(0.80f, 1.0f, pulse),
                    1.0f,
                    0.75f + pulse * 0.20f);
            }

            if (IsCopiedFromFramiliar)
            {
                // Radiant ethereal silver crystal aura when copied by Framiliar
                float grayOuter = Mathf.Clamp01((outerColor.r * 0.299f + outerColor.g * 0.587f + outerColor.b * 0.114f) * 1.3f + 0.25f);
                float grayInner = Mathf.Clamp01((innerColor.r * 0.299f + innerColor.g * 0.587f + innerColor.b * 0.114f) * 1.2f + 0.35f);
                outerColor = new Color(grayOuter, grayOuter, grayOuter, 0.55f + pulse * 0.25f + flicker);
                innerColor = new Color(grayInner, grayInner, grayInner, 0.85f + pulse * 0.15f);
            }

            // Sync sprite, flip and colors (strictly behind body sprite: -2, -1)
            SyncAuraRenderer(crystalAuraOuter, crystalSprite, isFlipped, outerColor, -2);
            SyncAuraRenderer(crystalAuraInner, crystalSprite, isFlipped, innerColor, -1);
            SyncAuraRenderer(crystalOutlineLeft, crystalSprite, isFlipped, innerColor, -1);
            SyncAuraRenderer(crystalOutlineRight, crystalSprite, isFlipped, innerColor, -1);
            SyncAuraRenderer(crystalOutlineUp, crystalSprite, isFlipped, innerColor, -1);
            SyncAuraRenderer(crystalOutlineDown, crystalSprite, isFlipped, innerColor, -1);

            // crystalAuraInner sits directly on the crystals (0 offset)
            crystalAuraInner.transform.localScale = Vector3.one;
            crystalAuraInner.transform.localPosition = Vector3.zero;

            // crystalAuraOuter ("the other one") has a small, clean offset (~1px)
            crystalAuraOuter.transform.localScale = Vector3.one * (1.08f + pulse * 0.015f);
            crystalAuraOuter.transform.localPosition = Vector3.zero;

            // 1-pixel cardinal outline offsets (1 / 32 = 0.03125 world units)
            crystalOutlineLeft.transform.localScale = Vector3.one;
            crystalOutlineRight.transform.localScale = Vector3.one;
            crystalOutlineUp.transform.localScale = Vector3.one;
            crystalOutlineDown.transform.localScale = Vector3.one;

            float pixelOffset = 1.0f / 32f;
            float flipDir = isFlipped ? -1f : 1f;

            crystalOutlineLeft.transform.localPosition = new Vector3(-pixelOffset * flipDir, 0f, 0f);
            crystalOutlineRight.transform.localPosition = new Vector3(pixelOffset * flipDir, 0f, 0f);
            crystalOutlineUp.transform.localPosition = new Vector3(0f, pixelOffset, 0f);
            crystalOutlineDown.transform.localPosition = new Vector3(0f, -pixelOffset, 0f);
        }

        private void SyncAuraRenderer(SpriteRenderer r, Sprite s, bool flipX, Color c, int orderOffset)
        {
            if (r == null) return;
            r.sprite = s;
            r.flipX = flipX;
            r.color = c;
            r.enabled = isDeployed && (s != null);
            if (opSpriteRenderer != null)
            {
                r.sortingLayerID = opSpriteRenderer.sortingLayerID;
                int baseOrder = Mathf.Max(5, opSpriteRenderer.sortingOrder);
                r.sortingOrder = baseOrder + orderOffset;
            }
            var auraMat = GetCrystalAuraMaterial();
            if (auraMat != null)
            {
                if (r.sharedMaterial != auraMat) r.sharedMaterial = auraMat;
            }
            else if (opSpriteRenderer != null && opSpriteRenderer.sharedMaterial != null)
            {
                if (r.sharedMaterial != opSpriteRenderer.sharedMaterial) r.sharedMaterial = opSpriteRenderer.sharedMaterial;
            }
        }

        #endregion

        private TrashTD.Core.GameLoop.WaveManager cachedWaveManager;
    }
}
