using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Enemies;
using TrashTD.Operators;

namespace TrashTD.Combat
{
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private const float BarWidth = 0.8f;
        private const float BarThickness = 0.055f;
        private const float BarOffset = 0.15f;

        /// <summary>Temporary HP (see OperatorBase.TemporaryHP) is the yellow segment after the real HP.</summary>
        private static readonly Color TemporaryHealthColor = new Color(1f, 0.95f, 0.35f);

        private static Material sharedMaterial;

        private LineRenderer background;
        private LineRenderer fill;
        private LineRenderer tempFill;
        private float verticalOffset = 0.55f;
        private float healthRatio = 1f;
        private float tempRatio;
        private int sortingLayerId;
        private int sortingOrder = 30;
        private EnemyBase enemy;

        public static void UpdateFor(GameObject target, int currentHP, int maxHP)
        {
            if (target == null || maxHP <= 0) return;

            WorldHealthBar healthBar = target.GetComponent<WorldHealthBar>();
            if (healthBar == null) healthBar = target.AddComponent<WorldHealthBar>();
            healthBar.UpdateHealth(currentHP, maxHP);
        }

        private void Awake()
        {
            EnsureLines();
        }

        // Also called lazily from UpdateHealth, since Awake does not run for components added in edit mode (tests).
        private void EnsureLines()
        {
            if (background != null && fill != null) return;

            enemy = GetComponent<EnemyBase>();
            SpriteRenderer spriteRenderer = null;
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled && renderers[i].sprite != null)
                {
                    spriteRenderer = renderers[i];
                    break;
                }
            }

            OperatorBase op = GetComponent<OperatorBase>();
            if (op != null)
            {
                verticalOffset = op.BodyExtents.y + BarOffset;
                sortingLayerId = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
                sortingOrder = Mathf.Max(sortingOrder, (spriteRenderer != null ? spriteRenderer.sortingOrder : 0) + 1);
            }
            else if (spriteRenderer != null)
            {
                verticalOffset = spriteRenderer.bounds.extents.y + BarOffset;
                MonoBehaviour[] components = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    if (!(components[i] is IOperatorSpriteFrames animation) ||
                        animation.Frames == null || animation.Frames.Length == 0 || animation.Frames[0] == null)
                        continue;

                    verticalOffset = spriteRenderer.transform.TransformVector(
                        Vector3.up * animation.Frames[0].bounds.extents.y).magnitude + BarOffset;
                    break;
                }

                sortingLayerId = spriteRenderer.sortingLayerID;
                sortingOrder = Mathf.Max(sortingOrder, spriteRenderer.sortingOrder + 1);
            }

            background = CreateLine("HealthBarBackground", new Color(0.035f, 0.045f, 0.05f, 0.95f), BarThickness);
            fill = CreateLine("HealthBarFill", Color.green, BarThickness * 0.62f);
            fill.sortingOrder = sortingOrder + 1;
            tempFill = CreateLine("HealthBarTemporary", TemporaryHealthColor, BarThickness * 0.62f);
            tempFill.sortingOrder = sortingOrder + 1;
            GridSorting.SortAboveUnits(background.gameObject, sortingOrder);
            GridSorting.SortAboveUnits(fill.gameObject, sortingOrder + 1);
            GridSorting.SortAboveUnits(tempFill.gameObject, sortingOrder + 1);
        }

        private void LateUpdate()
        {
            if (background == null || !background.gameObject.activeSelf) return;

            OperatorBase op = GetComponent<OperatorBase>();
            Vector3 basePos = enemy != null ? enemy.VisualPosition : (op != null ? op.VisualCenter : transform.position);
            Vector3 center = basePos + Vector3.up * verticalOffset;
            center.z = transform.position.z - 0.05f;
            background.SetPosition(0, center + Vector3.left * (BarWidth * 0.5f));
            background.SetPosition(1, center + Vector3.right * (BarWidth * 0.5f));

            Vector3 fillStart = center + Vector3.left * (BarWidth * 0.5f);
            fill.SetPosition(0, fillStart);
            fill.SetPosition(1, fillStart + Vector3.right * (BarWidth * healthRatio));

            if (tempFill != null)
            {
                Vector3 tempStart = fillStart + Vector3.right * (BarWidth * healthRatio);
                tempFill.SetPosition(0, tempStart);
                tempFill.SetPosition(1, tempStart + Vector3.right * (BarWidth * tempRatio));
            }
        }

        private LineRenderer CreateLine(string objectName, Color color, float width)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.sortingLayerID = sortingLayerId;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = GetSharedMaterial();
            line.gameObject.SetActive(false);
            return line;
        }

        private void UpdateHealth(int currentHP, int maxHP)
        {
            EnsureLines();

            // Temporary HP is read from the operator itself, so every caller that refreshes the bar shows it
            OperatorBase op = GetComponent<OperatorBase>();
            int temporaryHP = op != null ? Mathf.Max(0, op.TemporaryHP) : 0;

            if (currentHP >= maxHP && temporaryHP <= 0)
            {
                healthRatio = 1f;
                tempRatio = 0f;
                background.gameObject.SetActive(false);
                fill.gameObject.SetActive(false);
                tempFill.gameObject.SetActive(false);
                return;
            }

            // The bar is max HP wide; with temporary HP on top of a full health bar the whole thing is shared out
            float total = Mathf.Max(maxHP, currentHP + temporaryHP);
            healthRatio = Mathf.Clamp01(currentHP / total);
            tempRatio = Mathf.Clamp01(temporaryHP / total);
            fill.startColor = GetHealthColor(Mathf.Clamp01((float)currentHP / maxHP));
            fill.endColor = fill.startColor;
            background.gameObject.SetActive(true);
            fill.gameObject.SetActive(true);
            tempFill.gameObject.SetActive(tempRatio > 0f);
        }

        public void SetVisible(bool visible)
        {
            EnsureLines();
            bool needed = healthRatio < 1f || tempRatio > 0f;
            if (background != null) background.gameObject.SetActive(visible && needed);
            if (fill != null) fill.gameObject.SetActive(visible && needed);
            if (tempFill != null) tempFill.gameObject.SetActive(visible && tempRatio > 0f);
        }

        private static Color GetHealthColor(float ratio)
        {
            if (ratio <= 0.3f) return new Color(1f, 0.2f, 0.18f);
            if (ratio <= 0.6f) return new Color(1f, 0.75f, 0.15f);
            return new Color(0.25f, 0.95f, 0.36f);
        }

        private static Material GetSharedMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            sharedMaterial = new Material(shader);
            if (sharedMaterial.HasProperty("_Color")) sharedMaterial.SetColor("_Color", Color.white);
            if (sharedMaterial.HasProperty("_BaseColor")) sharedMaterial.SetColor("_BaseColor", Color.white);
            return sharedMaterial;
        }
    }
}