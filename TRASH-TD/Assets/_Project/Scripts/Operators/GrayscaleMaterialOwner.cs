using UnityEngine;

namespace TrashTD.Operators
{
    /// <summary>
    /// Holds the grayscale material a Framiliar copy's renderer uses and destroys it with the renderer, so each grey
    /// copy has a material of its own (see FramiliarOperator.ApplyGrayscaleToRenderer).
    /// </summary>
    public sealed class GrayscaleMaterialOwner : MonoBehaviour
    {
        public Material Material { get; set; }

        private void OnDestroy()
        {
            if (Material == null) return;
            if (Application.isPlaying) Destroy(Material);
            else DestroyImmediate(Material);
            Material = null;
        }
    }
}
