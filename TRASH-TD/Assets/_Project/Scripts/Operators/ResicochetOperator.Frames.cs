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
    // Loading and slicing the operator and crystal animation frames.
    public partial class ResicochetOperator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCrystalCache()
        {
            cachedCrystalTexture = null;
            cachedCrystal1Frames = null;
            cachedCrystal2Frames = null;
            cachedCrystal3Frames = null;
            cachedCharge1Frames = null;
            cachedCharge2Frames = null;
            cachedCharge3Frames = null;
            s_CrystalAuraMaterial = null;
        }

        private static bool AreFramesValid(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null || frames[i].texture == null) return false;
            }
            return true;
        }

        /// <summary>
        /// Identifies whether a 32x32 frame pixel belongs to the floating crystals.
        /// Evaluated in Unity texture coordinates where localY=0 is bottom and localY=31 is top.
        /// Isolates Crystal 1 (Mid), Crystal 2 (Top-Left), and Crystal 3 (Top) without including body or flame pixels.
        /// </summary>
        public static bool IsCrystalPixelInUnityCoords(int chargeLevel, int localX, int localY)
        {
            int yTop = 31 - localY;

            // Crystal 1 (Mid): present at all charges
            if (localX >= 0 && localX <= 5 && yTop >= 9 && yTop <= 15) return true;

            // Crystal 2 (Top-Left): present at charge 2 and 3. Exclude shoulder outline at yTop=8, localX>=6
            if (chargeLevel >= 2)
            {
                if (yTop >= 1 && yTop <= 7 && localX >= 2 && localX <= 7) return true;
                if (yTop == 8 && localX >= 3 && localX <= 5) return true;
            }

            // Crystal 3 (Top): present at charge 3 only (x=10..13 strictly avoids any head flame overlap)
            if (chargeLevel >= 3 && localX >= 10 && localX <= 13 && yTop >= 1 && yTop <= 6)
            {
                return true;
            }

            return false;
        }

        private void EnsureFramesLoaded()
        {
            if (spriteAnim == null)
            {
                spriteAnim = GetComponent<OperatorSpriteAnimation>();
            }

            if (AreFramesValid(cachedCharge1Frames) &&
                AreFramesValid(cachedCharge2Frames) &&
                AreFramesValid(cachedCharge3Frames))
            {
                charge1Frames = cachedCharge1Frames;
                charge2Frames = cachedCharge2Frames;
                charge3Frames = cachedCharge3Frames;
            }

            if (AreFramesValid(cachedCrystal1Frames) &&
                AreFramesValid(cachedCrystal2Frames) &&
                AreFramesValid(cachedCrystal3Frames))
            {
                crystalCharge1Frames = cachedCrystal1Frames;
                crystalCharge2Frames = cachedCrystal2Frames;
                crystalCharge3Frames = cachedCrystal3Frames;
            }

            var sr = GetComponent<SpriteRenderer>();
            Texture2D tex = sr != null && sr.sprite != null ? sr.sprite.texture : null;

#if UNITY_EDITOR
            if (tex == null)
            {
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Operators/Resicochet/Resicochet-Sheet.png");
            }
#endif
            if (tex == null)
            {
                string path = System.IO.Path.Combine(Application.dataPath, "_Project/Art/Operators/Resicochet/Resicochet-Sheet.png");
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        byte[] raw = System.IO.File.ReadAllBytes(path);
                        tex = new Texture2D(128, 96, TextureFormat.RGBA32, false);
                        tex.filterMode = FilterMode.Point;
                        tex.LoadImage(raw);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[ResicochetOperator] Could not load raw texture bytes for operator frames: {ex.Message}");
                    }
                }
            }

            if (charge1Frames == null || charge1Frames.Length == 0 ||
                charge2Frames == null || charge2Frames.Length == 0 ||
                charge3Frames == null || charge3Frames.Length == 0)
            {
#if UNITY_EDITOR
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Operators/Resicochet/Resicochet-Sheet.png");
                if (assets != null && assets.Length > 0)
                {
                    var subSprites = new Dictionary<string, Sprite>();
                    foreach (var a in assets)
                    {
                        if (a is Sprite s) subSprites[s.name] = s;
                    }
                    if (subSprites.ContainsKey("Resicochet_0") && subSprites.ContainsKey("Resicochet_4") && subSprites.ContainsKey("Resicochet_8"))
                    {
                        charge1Frames = new[] { subSprites["Resicochet_0"], subSprites["Resicochet_1"], subSprites["Resicochet_2"], subSprites["Resicochet_3"] };
                        charge2Frames = new[] { subSprites["Resicochet_4"], subSprites["Resicochet_5"], subSprites["Resicochet_6"], subSprites["Resicochet_7"] };
                        charge3Frames = new[] { subSprites["Resicochet_8"], subSprites["Resicochet_9"], subSprites["Resicochet_10"], subSprites["Resicochet_11"] };
                        cachedCharge1Frames = charge1Frames;
                        cachedCharge2Frames = charge2Frames;
                        cachedCharge3Frames = charge3Frames;
                    }
                }
#endif
                if ((charge1Frames == null || charge1Frames.Length == 0) && tex != null)
                {
                    SliceFramesFromTexture(tex);
                    cachedCharge1Frames = charge1Frames;
                    cachedCharge2Frames = charge2Frames;
                    cachedCharge3Frames = charge3Frames;
                }
            }

            if (sr != null && sr.sprite == null && charge1Frames != null && charge1Frames.Length > 0)
            {
                sr.sprite = charge1Frames[0];
            }

            EnsureCrystalFramesLoaded(tex);
        }

        private void EnsureCrystalFramesLoaded(Texture2D sourceTex)
        {
            if (AreFramesValid(crystalCharge1Frames) &&
                AreFramesValid(crystalCharge2Frames) &&
                AreFramesValid(crystalCharge3Frames))
            {
                return;
            }

            if (AreFramesValid(cachedCrystal1Frames) &&
                AreFramesValid(cachedCrystal2Frames) &&
                AreFramesValid(cachedCrystal3Frames) &&
                cachedCrystalTexture != null)
            {
                crystalCharge1Frames = cachedCrystal1Frames;
                crystalCharge2Frames = cachedCrystal2Frames;
                crystalCharge3Frames = cachedCrystal3Frames;
                return;
            }

            Texture2D readable = null;

            // 1. First prioritize sourceTex (sr.sprite.texture) directly, as it matches Unity's sprite UV coordinates exactly.
            if (sourceTex != null && sourceTex.isReadable)
            {
                readable = sourceTex;
            }

#if UNITY_EDITOR
            // 2. In editor, load the texture asset directly via AssetDatabase
            if (readable == null)
            {
                readable = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Operators/Resicochet/Resicochet-Sheet.png");
            }
#endif

            bool isRawBytes = false;

            // 3. Fall back to reading file bytes if not yet loaded
            if (readable == null)
            {
                string path = System.IO.Path.Combine(Application.dataPath, "_Project/Art/Operators/Resicochet/Resicochet-Sheet.png");
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        byte[] raw = System.IO.File.ReadAllBytes(path);
                        readable = new Texture2D(128, 96, TextureFormat.RGBA32, false);
                        readable.filterMode = FilterMode.Point;
                        readable.LoadImage(raw);
                        isRawBytes = true;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[ResicochetOperator] Could not load raw texture bytes: {ex.Message}");
                    }
                }
            }

            if (readable == null) return;

            cachedCrystalTexture = new Texture2D(128, 96, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontSave
            };

            Color32[] clearColors = new Color32[128 * 96];
            cachedCrystalTexture.SetPixels32(clearColors);

            for (int col = 0; col < 4; col++)
            {
                int xOffset = col * 32;

                for (int y = 0; y < 96; y++)
                {
                    int row = y / 32;
                    int localY = y % 32;
                    int chargeLevel = row switch
                    {
                        2 => 1,
                        1 => 2,
                        0 => 3,
                        _ => 1
                    };

                    for (int x = 0; x < 32; x++)
                    {
                        int texX = xOffset + x;
                        int texY = y;
                        int srcTexY = isRawBytes ? (95 - y) : y;

                        if (IsCrystalPixelInUnityCoords(chargeLevel, x, localY))
                        {
                            Color srcPixel = readable.GetPixel(texX, srcTexY);
                            if (srcPixel.a > 0.05f)
                            {
                                cachedCrystalTexture.SetPixel(texX, texY, Color.white);
                            }
                        }
                    }
                }
            }

            cachedCrystalTexture.Apply(false);

            float ppu = 32f;
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            cachedCrystal1Frames = new Sprite[4];
            cachedCrystal2Frames = new Sprite[4];
            cachedCrystal3Frames = new Sprite[4];

            for (int col = 0; col < 4; col++)
            {
                cachedCrystal1Frames[col] = Sprite.Create(cachedCrystalTexture, new Rect(col * 32, 64, 32, 32), pivot, ppu);
                cachedCrystal1Frames[col].hideFlags = HideFlags.DontSave;

                cachedCrystal2Frames[col] = Sprite.Create(cachedCrystalTexture, new Rect(col * 32, 32, 32, 32), pivot, ppu);
                cachedCrystal2Frames[col].hideFlags = HideFlags.DontSave;

                cachedCrystal3Frames[col] = Sprite.Create(cachedCrystalTexture, new Rect(col * 32, 0, 32, 32), pivot, ppu);
                cachedCrystal3Frames[col].hideFlags = HideFlags.DontSave;
            }

            crystalCharge1Frames = cachedCrystal1Frames;
            crystalCharge2Frames = cachedCrystal2Frames;
            crystalCharge3Frames = cachedCrystal3Frames;
        }

        private void SliceFramesFromTexture(Texture2D tex)
        {
            float ppu = 32f;
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            // Sprite sheet is 128x96 (4 columns, 3 rows of 32x32)
            // Row 1 (y = 64..96): 1 charge
            // Row 2 (y = 32..64): 2 charges
            // Row 3 (y = 0..32): 3 charges
            charge1Frames = new Sprite[4];
            charge2Frames = new Sprite[4];
            charge3Frames = new Sprite[4];

            for (int col = 0; col < 4; col++)
            {
                charge1Frames[col] = Sprite.Create(tex, new Rect(col * 32, 64, 32, 32), pivot, ppu);
                charge2Frames[col] = Sprite.Create(tex, new Rect(col * 32, 32, 32, 32), pivot, ppu);
                charge3Frames[col] = Sprite.Create(tex, new Rect(col * 32, 0, 32, 32), pivot, ppu);
            }
        }
    }
}
