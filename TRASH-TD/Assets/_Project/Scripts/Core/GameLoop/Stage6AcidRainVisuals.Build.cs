using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using Random = UnityEngine.Random;

namespace TrashTD.Core.GameLoop
{
    // Creating the acid rain layers: art mask, tint, fog, bubbles, puddles, rain and particles.
    public partial class Stage6AcidRainVisuals
    {
        /// <summary>Clips everything to the map artwork so nothing draws over the void around it.</summary>
        private void CreateArtMask()
        {
            var maskObject = new GameObject("Stage6StormMask");
            maskObject.transform.SetParent(transform, false);
            maskObject.transform.position = (Vector3)((artMin + artMax) * 0.5f);
            maskObject.transform.localScale = new Vector3(artMax.x - artMin.x, artMax.y - artMin.y, 1f);
            maskObject.AddComponent<SpriteMask>().sprite = whiteSprite;
        }

        private SpriteRenderer CreateRenderer(Transform parent, string objectName, Sprite sprite, bool onGround, int sortingOrder)
        {
            var rendererObject = new GameObject(objectName);
            rendererObject.transform.SetParent(parent, false);
            var renderer = rendererObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (onGround) GridSorting.PlaceOnGround(renderer);
            renderer.sortingOrder = sortingOrder;
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            return renderer;
        }

        private void CreateTintAndFlash()
        {
            Vector3 centre = (Vector3)((artMin + artMax) * 0.5f);
            Vector3 size = new Vector3(artMax.x - artMin.x, artMax.y - artMin.y, 1f);

            tint = CreateRenderer(transform, "Stage6ToxicCast", whiteSprite, true, TintSortingOrder);
            tint.transform.position = centre;
            tint.transform.localScale = size;
            tint.color = Color.clear;

            // The flash comes from the sky: strongest at the top of the map, gone by the middle
            skyFlash = CreateRenderer(transform, "Stage6StormFlash", skyFlashSprite, false, FlashSortingOrder);
            skyFlash.transform.position = centre;
            skyFlash.transform.localScale = new Vector3(
                size.x / skyFlashSprite.bounds.size.x, size.y / skyFlashSprite.bounds.size.y, 1f);
            skyFlash.color = Color.clear;
        }

        private void CreateFog()
        {
            for (int l = 0; l < FogLayers.Length; l++)
            {
                FogLayer layer = FogLayers[l];
                var layerRoot = new GameObject($"Stage6Fog_{layer.Name}");
                layerRoot.transform.SetParent(transform, false);

                for (int i = 0; i < layer.Count; i++)
                {
                    SpriteRenderer renderer = CreateRenderer(layerRoot.transform, $"FogCloud_{i}", null, layer.OnGround, layer.SortingOrder);
                    renderer.transform.localScale = Vector3.one * cellSize; // 32 px per tile: one texture pixel per art pixel

                    var cloud = new FogCloud { Layer = layer, Transform = renderer.transform, Renderer = renderer };
                    ResetFogCloud(cloud, true);
                    fogClouds.Add(cloud);
                }
            }
        }

        private void ResetFogCloud(FogCloud cloud, bool anywhere)
        {
            FogLayer layer = cloud.Layer;
            Sprite sprite = fogSprites[Random.Range(0, fogSprites.Length)];
            cloud.Renderer.sprite = sprite;
            cloud.Renderer.flipX = Random.value < 0.5f;
            cloud.HalfWidth = sprite.bounds.extents.x * cellSize;

            // Drifts right with the storm wind; new clouds roll in from the left edge
            cloud.X = anywhere
                ? Random.Range(artMin.x - cloud.HalfWidth, artMax.x + cloud.HalfWidth)
                : artMin.x - cloud.HalfWidth;
            cloud.BaseY = Random.Range(artMin.y, artMax.y);
            cloud.BaseAlpha = Random.Range(layer.AlphaRange.x, layer.AlphaRange.y);
            cloud.Speed = Random.Range(layer.SpeedRange.x, layer.SpeedRange.y) * cellSize;
            cloud.Phase = Random.Range(0f, Mathf.PI * 2f);
            cloud.FadeIn = anywhere ? 1f : 0f;
        }

        /// <summary>
        /// Bubbles for the acid pool painted in the map art (the Trap tiles). The art itself is left alone; the
        /// barrels and sign in the middle are midground art drawn over these, so bubbles never show on top of them.
        /// </summary>
        private void CreatePoolBubbles()
        {
            for (int x = 0; x < stageData.gridWidth; x++)
            {
                for (int y = 0; y < stageData.gridHeight; y++)
                {
                    GridCell cell = gridManager.GetCell(x, y);
                    if (cell != null && cell.TileType == TileType.Trap) poolCentres.Add(cell.WorldPosition);
                }
            }

            var bubbleRoot = new GameObject("Stage6AcidBubbles");
            bubbleRoot.transform.SetParent(transform, false);
            for (int i = 0; i < MaxBubbles; i++)
            {
                SpriteRenderer renderer = CreateRenderer(bubbleRoot.transform, $"Bubble_{i}", ringSprites[0], true, GroundFxSortingOrder);
                renderer.transform.localScale = Vector3.one * cellSize;
                renderer.gameObject.SetActive(false);
                // Staggered so the pool doesn't all bubble at once
                bubbles.Add(new Bubble { Transform = renderer.transform, Renderer = renderer, Delay = Random.Range(0f, 2.5f) });
            }
        }

        /// <summary>Downpour puddles: the candidate tiles and a small pool of renderers.</summary>
        private void CreatePuddles()
        {
            for (int x = 0; x < stageData.gridWidth; x++)
            {
                for (int y = 0; y < stageData.gridHeight; y++)
                {
                    GridCell cell = gridManager.GetCell(x, y);
                    if (cell == null) continue;
                    // Ground and high ground; never the pool, spawns, exits, holes or blocked tiles
                    if (cell.TileType == TileType.LowGround || cell.TileType == TileType.EnemyPath || cell.TileType == TileType.HighGround)
                        puddleCandidates.Add(cell);
                }
            }

            var puddleRoot = new GameObject("Stage6AcidPuddles");
            puddleRoot.transform.SetParent(transform, false);
            for (int i = 0; i < MaxPuddles; i++)
            {
                SpriteRenderer renderer = CreateRenderer(puddleRoot.transform, $"AcidPuddle_{i}", null, true, PuddleSortingOrder);
                renderer.transform.localScale = Vector3.one * cellSize;
                renderer.gameObject.SetActive(false);
                puddles.Add(new Puddle { Transform = renderer.transform, Renderer = renderer, State = PuddleState.Idle });
            }
        }

        private void CreateRainDrops()
        {
            var rainRoot = new GameObject("Stage6AcidRain");
            rainRoot.transform.SetParent(transform, false);

            for (int i = 0; i < MaxRainDrops; i++)
            {
                SpriteRenderer renderer = CreateRenderer(rainRoot.transform, $"AcidDrop_{i}", streakSprite, false, RainSortingOrder);
                renderer.gameObject.SetActive(false);
                rainDrops.Add(new RainDrop { Transform = renderer.transform, Renderer = renderer });
            }
        }

        private void CreateParticles()
        {
            var sprayRoot = new GameObject("Stage6AcidSpray");
            sprayRoot.transform.SetParent(transform, false);
            for (int i = 0; i < MaxSpray; i++)
            {
                SpriteRenderer renderer = CreateRenderer(sprayRoot.transform, $"Spray_{i}", whiteSprite, false, RainSortingOrder);
                renderer.gameObject.SetActive(false);
                sprays.Add(new Particle { Transform = renderer.transform, Renderer = renderer });
            }

            var fumeRoot = new GameObject("Stage6AcidFumes");
            fumeRoot.transform.SetParent(transform, false);
            for (int i = 0; i < MaxFumes; i++)
            {
                SpriteRenderer renderer = CreateRenderer(fumeRoot.transform, $"Fume_{i}", fumeSprites[0], false, FrontFogSortingOrder);
                renderer.transform.localScale = Vector3.one * cellSize;
                renderer.gameObject.SetActive(false);
                fumes.Add(new Particle { Transform = renderer.transform, Renderer = renderer });
            }
        }

        /// <summary>Scatters drops through the air so the stage doesn't open with an empty sky.</summary>
        private void PrefillRain()
        {
            int wantedDrops = Mathf.RoundToInt(rainIntensity * MaxRainDrops);
            for (int i = 0; i < wantedDrops && i < rainDrops.Count; i++)
            {
                SpawnRainDrop(rainDrops[i], true);
            }
        }

        // ---------- Fog ----------
    }
}
