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
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Deck slots, class badges and class icon loading, cooldown and heat badges.
    public partial class GameplayHUDUI
    {
        private Button CreateDeckSlot(Transform parent, string objectName)
        {
            // Size and position are set by the caller to match a bay of the Deck BG sprite. The slot image stays
            // transparent (it only catches clicks and carries the hover/selection glow outline), so the bay
            // art shows through behind the operator.
            var button = CreateButton(parent, objectName, "", new Vector2(102f, 117f));
            var image = button.GetComponent<Image>();
            image.sprite = null;
            image.color = Color.clear;
            image.type = Image.Type.Simple;

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 11;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 8;
                label.resizeTextMaxSize = 11;
                label.color = Color.white;

                // Name + stars sit on the dark strip under the green bracket frame (bay y = 36..46 of 7..46).
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0.03f, 0.01f);
                labelRect.anchorMax = new Vector2(0.97f, 0.27f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            // Portrait sits inside the bay's green bracket frame (bay y = 8..34 of 7..46), with a small inset.
            var portraitObject = new GameObject("OperatorPortrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(button.transform, false);
            RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.1f, 0.33f);
            portraitRect.anchorMax = new Vector2(0.9f, 0.94f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            Image portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            if (label != null)
            {
                label.transform.SetAsLastSibling();
            }

            return button;
        }

        private GameObject CreateClassBadge(Transform parent, out Image backgroundImage, out Image iconImage)
        {
            var badge = new GameObject("ClassBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 0.36f);
            badgeRect.anchorMax = new Vector2(1f, 0.36f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(26f, 26f);
            badgeRect.anchoredPosition = new Vector2(-6f, 0f);

            backgroundImage = badge.GetComponent<Image>();
            backgroundImage.raycastTarget = false;
            backgroundImage.color = Color.clear;

            var iconObject = new GameObject("ClassIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(badge.transform, false);
            iconImage = iconObject.GetComponent<Image>();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            badge.SetActive(false);
            return badge;
        }

        private static bool classIconsLoaded = false;

        public static void ReloadClassIcons()
        {
            classIconsLoaded = false;
            ClassIconSprites.Clear();
            EnsureClassIconsLoaded();
        }

        private static void EnsureClassIconsLoaded()
        {
            if (classIconsLoaded && ClassIconSprites.Count >= 6) return;

            Sprite[] sprites = null;

#if UNITY_EDITOR
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/class-icons.png");
            if (assets != null && assets.Length > 0)
            {
                var list = new List<Sprite>();
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite s) list.Add(s);
                }
                sprites = list.ToArray();
            }
#endif

            if (sprites == null || sprites.Length == 0)
            {
                sprites = Resources.LoadAll<Sprite>("UI/icons/class-icons");
            }

            if (sprites != null && sprites.Length > 0)
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite s = sprites[i];
                    if (s == null) continue;
                    switch (s.name)
                    {
                        case "class-icons_0": ClassIconSprites[OperatorClass.Sniper] = s; break;
                        case "class-icons_1": ClassIconSprites[OperatorClass.Medic] = s; break;
                        case "class-icons_2": ClassIconSprites[OperatorClass.Caster] = s; break;
                        case "class-icons_3": ClassIconSprites[OperatorClass.Defender] = s; break;
                        case "class-icons_4": ClassIconSprites[OperatorClass.Guard] = s; break;
                        case "class-icons_5": ClassIconSprites[OperatorClass.Specialist] = s; break;
                    }
                }
            }

            // Fallback: If sub-sprites are not found or sliced, load Texture2D and slice them manually
            // (0 - sniper, 1 - medic, 2 - caster, 3 - defender, 4 - guard, 5 - specialist)
            if (ClassIconSprites.Count < 6)
            {
                Texture2D tex = null;
#if UNITY_EDITOR
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/class-icons.png");
#endif
                if (tex == null)
                {
                    tex = Resources.Load<Texture2D>("UI/icons/class-icons");
                }
                if (tex == null)
                {
                    string diskPath = System.IO.Path.Combine(Application.dataPath, "_Project/Art/class-icons.png");
                    if (System.IO.File.Exists(diskPath))
                    {
                        byte[] raw = System.IO.File.ReadAllBytes(diskPath);
                        tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(raw);
                    }
                }

                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    if (!ClassIconSprites.ContainsKey(OperatorClass.Sniper))
                        ClassIconSprites[OperatorClass.Sniper] = Sprite.Create(tex, new Rect(0f, 32f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!ClassIconSprites.ContainsKey(OperatorClass.Medic))
                        ClassIconSprites[OperatorClass.Medic] = Sprite.Create(tex, new Rect(32f, 32f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!ClassIconSprites.ContainsKey(OperatorClass.Caster))
                        ClassIconSprites[OperatorClass.Caster] = Sprite.Create(tex, new Rect(64f, 32f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!ClassIconSprites.ContainsKey(OperatorClass.Defender))
                        ClassIconSprites[OperatorClass.Defender] = Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!ClassIconSprites.ContainsKey(OperatorClass.Guard))
                        ClassIconSprites[OperatorClass.Guard] = Sprite.Create(tex, new Rect(32f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!ClassIconSprites.ContainsKey(OperatorClass.Specialist))
                        ClassIconSprites[OperatorClass.Specialist] = Sprite.Create(tex, new Rect(64f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                }
            }

            classIconsLoaded = true;
        }

        public static Sprite GetClassIconSprite(OperatorClass opClass)
        {
            EnsureClassIconsLoaded();
            if (ClassIconSprites.TryGetValue(opClass, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            return null;
        }

        private static bool enemyClassIconsLoaded = false;
        private static readonly Dictionary<EnemyClassIcon, Sprite> EnemyClassIconSprites = new Dictionary<EnemyClassIcon, Sprite>();

        public static void ReloadEnemyClassIcons()
        {
            enemyClassIconsLoaded = false;
            EnemyClassIconSprites.Clear();
            EnsureEnemyClassIconsLoaded();
        }

        private static void EnsureEnemyClassIconsLoaded()
        {
            if (enemyClassIconsLoaded && EnemyClassIconSprites.Count >= 5) return;

            Sprite[] sprites = null;

#if UNITY_EDITOR
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/enemy-class-icons.png");
            if (assets != null && assets.Length > 0)
            {
                var list = new List<Sprite>();
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is Sprite s) list.Add(s);
                }
                sprites = list.ToArray();
            }
#endif

            if (sprites == null || sprites.Length == 0)
            {
                sprites = Resources.LoadAll<Sprite>("UI/icons/enemy-class-icons");
            }

            if (sprites != null && sprites.Length > 0)
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite s = sprites[i];
                    if (s == null) continue;
                    switch (s.name)
                    {
                        case "enemy-class-icons_0": EnemyClassIconSprites[EnemyClassIcon.Air] = s; break;
                        case "enemy-class-icons_1": EnemyClassIconSprites[EnemyClassIcon.Rusher] = s; break;
                        case "enemy-class-icons_2": EnemyClassIconSprites[EnemyClassIcon.Caster] = s; break;
                        case "enemy-class-icons_3": EnemyClassIconSprites[EnemyClassIcon.Tank] = s; break;
                        case "enemy-class-icons_4": EnemyClassIconSprites[EnemyClassIcon.Grunt] = s; break;
                    }
                }
            }

            // Fallback: If sub-sprites are not found or sliced, load Texture2D and slice them manually
            // (0 - air, 1 - rusher, 2 - caster, 3 - tank, 4 - grunt)
            if (EnemyClassIconSprites.Count < 5)
            {
                Texture2D tex = null;
#if UNITY_EDITOR
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/enemy-class-icons.png");
#endif
                if (tex == null)
                {
                    tex = Resources.Load<Texture2D>("UI/icons/enemy-class-icons");
                }
                if (tex == null)
                {
                    string diskPath = System.IO.Path.Combine(Application.dataPath, "_Project/Art/enemy-class-icons.png");
                    if (System.IO.File.Exists(diskPath))
                    {
                        byte[] raw = System.IO.File.ReadAllBytes(diskPath);
                        tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(raw);
                    }
                }

                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    if (!EnemyClassIconSprites.ContainsKey(EnemyClassIcon.Air))
                        EnemyClassIconSprites[EnemyClassIcon.Air] = Sprite.Create(tex, new Rect(0f, 32f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!EnemyClassIconSprites.ContainsKey(EnemyClassIcon.Rusher))
                        EnemyClassIconSprites[EnemyClassIcon.Rusher] = Sprite.Create(tex, new Rect(32f, 32f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!EnemyClassIconSprites.ContainsKey(EnemyClassIcon.Caster))
                        EnemyClassIconSprites[EnemyClassIcon.Caster] = Sprite.Create(tex, new Rect(64f, 32f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!EnemyClassIconSprites.ContainsKey(EnemyClassIcon.Tank))
                        EnemyClassIconSprites[EnemyClassIcon.Tank] = Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                    if (!EnemyClassIconSprites.ContainsKey(EnemyClassIcon.Grunt))
                        EnemyClassIconSprites[EnemyClassIcon.Grunt] = Sprite.Create(tex, new Rect(32f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
                }
            }

            enemyClassIconsLoaded = true;
        }

        public static Sprite GetEnemyClassIconSprite(EnemyClassIcon iconType)
        {
            EnsureEnemyClassIconsLoaded();
            if (EnemyClassIconSprites.TryGetValue(iconType, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            return null;
        }

        public static Sprite GetEnemyClassIconSprite(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Flyer: return GetEnemyClassIconSprite(EnemyClassIcon.Air);
                case EnemyArchetype.Rusher: return GetEnemyClassIconSprite(EnemyClassIcon.Rusher);
                case EnemyArchetype.Caster: return GetEnemyClassIconSprite(EnemyClassIcon.Caster);
                case EnemyArchetype.Tank: return GetEnemyClassIconSprite(EnemyClassIcon.Tank);
                case EnemyArchetype.Grunt: return GetEnemyClassIconSprite(EnemyClassIcon.Grunt);
                default: return GetEnemyClassIconSprite(EnemyClassIcon.Grunt);
            }
        }

        public static Sprite GetEnemyClassIconSprite(EnemyData enemy)
        {
            if (enemy == null) return null;
            if (enemy.movementType == EnemyMovementType.Air || enemy.archetype == EnemyArchetype.Flyer)
            {
                return GetEnemyClassIconSprite(EnemyClassIcon.Air);
            }
            return GetEnemyClassIconSprite(enemy.archetype);
        }

        private GameObject CreateCooldownBadge(Transform parent, out Image clockImage, out Text roundsText)
        {
            var badge = new GameObject("CooldownBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(48f, 28f);
            badgeRect.anchoredPosition = new Vector2(6f, -6f);

            Image background = badge.GetComponent<Image>();
            background.color = new Color(0.16f, 0.045f, 0.025f, 0.96f);
            background.raycastTarget = false;

            Outline outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.58f, 0.18f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            var clockObject = new GameObject("ClockIcon", typeof(RectTransform), typeof(Image));
            clockObject.transform.SetParent(badge.transform, false);
            RectTransform clockRect = clockObject.GetComponent<RectTransform>();
            clockRect.anchorMin = new Vector2(0f, 0.5f);
            clockRect.anchorMax = new Vector2(0f, 0.5f);
            clockRect.pivot = new Vector2(0f, 0.5f);
            clockRect.sizeDelta = new Vector2(18f, 18f);
            clockRect.anchoredPosition = new Vector2(4f, 0f);

            clockImage = clockObject.GetComponent<Image>();
            clockImage.sprite = CreateCooldownClockSprite();
            clockImage.color = new Color(1f, 0.72f, 0.35f, 1f);
            clockImage.preserveAspect = true;
            clockImage.raycastTarget = false;

            roundsText = CreateText(badge.transform, "Rounds", "1", 14, TextAnchor.MiddleCenter);
            RectTransform roundsRect = roundsText.GetComponent<RectTransform>();
            roundsRect.anchorMin = new Vector2(0.43f, 0f);
            roundsRect.anchorMax = new Vector2(1f, 1f);
            roundsRect.offsetMin = Vector2.zero;
            roundsRect.offsetMax = new Vector2(-2f, 0f);
            roundsText.color = Color.white;
            roundsText.raycastTarget = false;

            badge.SetActive(false);
            return badge;
        }

        private GameObject CreateDeckHeatBadge(Transform parent, out Text heatText)
        {
            var badge = new GameObject("DeckHeatBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(1f, 0f);
            badgeRect.sizeDelta = new Vector2(38f, 22f);
            badgeRect.anchoredPosition = new Vector2(-4f, 4f);

            Image background = badge.GetComponent<Image>();
            background.color = new Color(0.24f, 0.05f, 0.02f, 0.96f);
            background.raycastTarget = false;

            Outline outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.35f, 0.08f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            heatText = CreateText(badge.transform, "HeatStacks", "🔥0", 12, TextAnchor.MiddleCenter);
            RectTransform textRect = heatText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            heatText.color = new Color(1f, 0.82f, 0.35f, 1f);
            heatText.fontStyle = FontStyle.Bold;
            heatText.raycastTarget = false;

            badge.SetActive(false);
            return badge;
        }

        private static Sprite CreateCooldownClockSprite()
        {
            if (cooldownClockSprite != null) return cooldownClockSprite;

            cooldownClockSprite = LoadIconSprite("icon_clock");
            if (cooldownClockSprite != null) return cooldownClockSprite;

            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            void Pixel(int x, int y)
            {
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    pixels[y * size + x] = Color.white;
                }
            }

            for (int y = 1; y < size - 1; y++)
            {
                for (int x = 1; x < size - 1; x++)
                {
                    float dx = x - 7.5f;
                    float dy = y - 7.5f;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= 31f && distanceSquared <= 49f)
                    {
                        Pixel(x, y);
                    }
                }
            }

            Pixel(7, 7);
            Pixel(7, 8);
            Pixel(7, 9);
            Pixel(8, 7);
            Pixel(9, 7);

            texture.SetPixels(pixels);
            texture.Apply();
            cooldownClockSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return cooldownClockSprite;
        }
    }
}
