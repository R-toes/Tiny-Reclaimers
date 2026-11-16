using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TrashTD.Audio;
using TrashTD.Data;
using StageProgress = TrashTD.Core.GameLoop.StageProgress;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // The ambient motes, canvas setup and shared UI building helpers.
    public partial class MainMenuController
    {
        private void CreateAmbientLayer(Transform parent, int count)
        {
            var layerGo = new GameObject("AmbientLayer", typeof(RectTransform));
            layerGo.transform.SetParent(parent, false);
            var layer = layerGo.GetComponent<RectTransform>();
            StretchFill(layer);
            layer.SetAsFirstSibling();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(layer, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                float size = Random.Range(4f, 11f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(0f, 1080f));
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0f);

                motes.Add(new Mote
                {
                    rt = rt,
                    layer = layer,
                    image = image,
                    speed = Random.Range(10f, 42f),
                    sway = Random.Range(6f, 22f),
                    phase = Random.Range(0f, Mathf.PI * 2f),
                    alpha = Random.Range(0.05f, 0.18f)
                });
            }
        }

        private void UpdateMotes(float dt, float time)
        {
            for (int i = 0; i < motes.Count; i++)
            {
                Mote m = motes[i];
                if (m.rt == null || !m.layer.gameObject.activeInHierarchy) continue;

                Vector2 size = m.layer.rect.size;
                if (size.x < 1f || size.y < 1f) size = new Vector2(1920f, 1080f);

                Vector2 pos = m.rt.anchoredPosition;
                pos.y += m.speed * dt;
                pos.x += Mathf.Sin(time * 0.6f + m.phase) * m.sway * dt;

                if (pos.y > size.y + 20f)
                {
                    pos.y = -20f;
                    pos.x = Random.value * size.x;
                }

                m.rt.anchoredPosition = pos;

                Color c = m.image.color;
                c.a = m.alpha * (0.6f + 0.4f * Mathf.Sin(time * 1.3f + m.phase * 2f));
                m.image.color = c;
            }
        }

        // ─────────────────────────────────────────────────────
        //  Canvas / event system helpers
        // ─────────────────────────────────────────────────────

        private GameObject GetOrCreateCanvas()
        {
            var existingCanvas = CrtDisplay.FindGameCanvas();
            if (existingCanvas != null)
            {
                EnsureEventSystem();
                return existingCanvas.gameObject;
            }

            var canvas = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasComponent = canvas.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        // ─────────────────────────────────────────────────────
        //  UI building helpers
        // ─────────────────────────────────────────────────────

        private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        /// <summary>Full-width strip hanging from the top edge of its parent.</summary>
        private static void StretchTop(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Full-width strip sitting on the bottom edge of its parent.</summary>
        private static void StretchBottom(RectTransform rt, float left, float right, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
        }

        private static Text MakeText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            var t = CreateTextChild(parent, name, text, fontSize, alignment).GetComponent<Text>();
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
            return t;
        }

        private static Button CreateButtonChild(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 60f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.15f, 0.18f, 0.24f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = UIFontHelper.GetPixelFont();
            text.fontSize = 24;
            text.color = Color.white;
            text.raycastTarget = false;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static GameObject CreateTextChild(Transform parent, string name, string text, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var textComp = go.GetComponent<Text>();
            textComp.text = text;
            textComp.font = UIFontHelper.GetPixelFont();
            textComp.fontSize = fontSize;
            textComp.alignment = alignment;
            textComp.color = Color.white;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return go;
        }
    }
}
