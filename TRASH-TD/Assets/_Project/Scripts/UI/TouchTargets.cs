using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TrashTD.UI
{
    /// <summary>
    /// Phone only. Many HUD buttons were drawn for a mouse and are only a few millimetres wide on a phone, so this
    /// grows the area that reacts to a tap (Graphic.raycastPadding) around every small button and toggle until it
    /// is about MobileUi.MinTouchTarget canvas units. Nothing changes visually. New buttons are picked up every half
    /// second, including ones that are still hidden, so they are ready the first time they show.
    /// </summary>
    public sealed class TouchTargets : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;

        private readonly HashSet<Selectable> handled = new HashSet<Selectable>();
        private float nextScan;

        /// <summary>Creates the (single) scanner. Called by MobileBootstrap on phones.</summary>
        public static void Create()
        {
            var go = new GameObject("TouchTargets");
            DontDestroyOnLoad(go);
            go.AddComponent<TouchTargets>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanInterval;

            foreach (Selectable selectable in FindObjectsByType<Selectable>(FindObjectsInactive.Include))
            {
                if (!(selectable is Button) && !(selectable is Toggle)) continue;
                if (!handled.Add(selectable)) continue;
                Grow(selectable.targetGraphic);
            }

            if (handled.Count > 2000) handled.RemoveWhere(s => s == null); // forget destroyed buttons
        }

        /// <summary>Expands the graphic's tap area to the minimum size, per axis.</summary>
        public static void Grow(Graphic graphic)
        {
            if (graphic == null || !graphic.raycastTarget) return;
            Canvas canvas = graphic.canvas;
            if (canvas == null) return;

            RectTransform rect = graphic.rectTransform;
            float rootScale = canvas.rootCanvas.transform.lossyScale.x;
            float canvasUnitsPerLocal = rootScale > 0f ? rect.lossyScale.x / rootScale : 1f;
            if (canvasUnitsPerLocal <= 0f) return;

            Vector2 size = rect.rect.size * canvasUnitsPerLocal;
            float x = MobileUi.TouchPadding(size.x, 1f / canvasUnitsPerLocal);
            float y = MobileUi.TouchPadding(size.y, 1f / canvasUnitsPerLocal);

            // Negative padding makes the tap area bigger than the graphic (left, bottom, right, top)
            graphic.raycastPadding = new Vector4(-x, -y, -x, -y);
        }
    }
}
