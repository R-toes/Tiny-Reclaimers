using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Systems;

namespace TrashTD.Operators
{
    /// <summary>
    /// Basurocket — 3-Star Sniper.
    /// Long-range explosive sniper. It can target only cells two to seven tiles
    /// in front of it; its rocket blasts a circle around the target, a bit bigger than a caster's splash.
    /// Upgrades:
    /// - Base (3★): fires every 2.8 s, 2 to 7 tiles forward; the blast deals 100% ATK.
    /// - 1x Upgraded (4★): fires every 2.3 s and reaches 1 tile further (2 to 8 tiles).
    /// - 2x Upgraded (5★): after the blast, cluster bombs go off in a ring 1 tile wide around it,
    ///   dealing 60% ATK to enemies the main blast did not hit.
    /// </summary>
    public sealed class BasurocketOperator : SniperOperator
    {
        /// <summary>Blast radius in tiles (caster splash is 1.5).</summary>
        public const float BlastRadiusTiles = 1.75f;
        private const float BlastDamageMultiplier = 1f;
        private static readonly Color BlastColor = new Color(1f, 0.62f, 0.12f); // Yellow-orange: never the Eradicator's crimson

        /// <summary>1st upgrade: seconds between rockets (2.8 in the data asset before it).</summary>
        public const float UpgradedAttackInterval = 2.3f;

        /// <summary>2nd upgrade: the cluster ring reaches this many tiles beyond the main blast.</summary>
        public const float ClusterRingWidthTiles = 1f;
        public const float ClusterRadiusTiles = BlastRadiusTiles + ClusterRingWidthTiles;
        public const float ClusterDamageMultiplier = 0.6f;
        public const float ClusterDelaySeconds = 0.18f;
        public const int ClusterBombCount = 8;

        /// <summary>1st upgrade range: the base 2 to 7 tiles forward, plus the 8th.</summary>
        private static readonly Vector2Int[] UpgradedRangePattern =
        {
            new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0),
            new Vector2Int(6, 0), new Vector2Int(7, 0), new Vector2Int(8, 0)
        };

        public bool HasFirstUpgrade => currentRarity >= OperatorRarity.Star4;
        public bool HasSecondUpgrade => currentRarity >= OperatorRarity.Star5;

        public void SetRarityForTesting(OperatorRarity rarity)
        {
            currentRarity = rarity;
        }

        public static Vector2Int[] GetRangePatternForRarity(OperatorData operatorData, OperatorRarity rarity)
        {
            if (operatorData == null || operatorData.rangePattern == null) return null;
            return rarity >= OperatorRarity.Star4 ? UpgradedRangePattern : operatorData.rangePattern;
        }

        public override Vector2Int[] GetRangePattern()
        {
            if (data == null || data.rangePattern == null) return null;
            return HasFirstUpgrade ? UpgradedRangePattern : data.rangePattern;
        }

        public override float GetAttackInterval()
        {
            float baseInterval = HasFirstUpgrade ? UpgradedAttackInterval : (data != null ? data.attackInterval : 2.8f);
            return StageCombatModifiers.GetAttackInterval(baseInterval) / HarmonyAttackSpeedMultiplier;
        }

        /// <summary>
        /// Whether a point at this distance from the impact is hit by the cluster bombs: outside the main blast,
        /// and no further than the cluster radius. Distances and radii in the same unit.
        /// </summary>
        public static bool IsInClusterRing(float distance, float blastRadius, float clusterRadius) =>
            distance > blastRadius && distance <= clusterRadius;

        public override void Attack(EnemyBase target)
        {
            if (target == null || !isDeployed || EnemyManager.Instance == null)
                return;

            GridManager gridManager = FindAnyObjectByType<GridManager>();
            if (gridManager == null)
                return;

            float cellSize = gridManager.CellSize;
            float radius = BlastRadiusTiles * cellSize;
            Vector3 aimPosition = target.transform.position;
            if (EnemyManager.Instance.GetEnemiesInRadius(aimPosition, radius, data.position).Count == 0)
                return;

            PlayAttackSound();
            CombatProjectileVisual.Fire(
                ProjectileOrigin,
                target,
                BlastColor,
                14f,
                0.18f,
                0.1f,
                true,
                // The rocket homes on its target, so it blows up where the target is when it lands.
                () => ApplyBlastDamage(target != null && !target.IsDead ? target.transform.position : aimPosition, radius, cellSize),
                ProjectileStyle.Rocket);
        }

        private void ApplyBlastDamage(Vector3 center, float radius, float cellSize)
        {
            if (EnemyManager.Instance == null)
                return;

            AoeBlastVisual.PlayCircle(center, radius, BlastColor, AoeStyle.JunkRocket);
            PlayImpactSound(); // the explosion, even if the target moved out of the blast

            List<EnemyBase> blastTargets = EnemyManager.Instance.GetEnemiesInRadius(center, radius, data.position);
            HashSet<EnemyBase> uniqueTargets = new HashSet<EnemyBase>();
            for (int i = 0; i < blastTargets.Count; i++)
            {
                EnemyBase blastTarget = blastTargets[i];
                if (blastTarget == null || blastTarget.IsDead || !uniqueTargets.Add(blastTarget))
                    continue;

                int damage = Mathf.RoundToInt(currentATK * BlastDamageMultiplier);
                damage = DamageCalculator.CalculateDamage(damage, blastTarget.CurrentDEF);
                blastTarget.TryTakeAttackDamage(damage, DamageType.Physical, this);
            }

            // 2nd upgrade: cluster bombs around the blast.
            if (!HasSecondUpgrade) return;

            float clusterRadius = ClusterRadiusTiles * cellSize;
            if (Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(ClusterRoutine(center, radius, clusterRadius, uniqueTargets));
            }
            else
            {
                // Not playing (EditMode tests): no delay.
                ApplyClusterDamage(center, radius, clusterRadius, uniqueTargets);
            }
        }

        private IEnumerator ClusterRoutine(Vector3 center, float blastRadius, float clusterRadius, HashSet<EnemyBase> alreadyHit)
        {
            yield return new WaitForSeconds(ClusterDelaySeconds);
            if (!isDeployed) yield break;

            PlayClusterVisuals(center, blastRadius, clusterRadius);
            PlayImpactSound();
            ApplyClusterDamage(center, blastRadius, clusterRadius, alreadyHit);
        }

        /// <summary>
        /// The cluster bombs hit every enemy in the ring between the main blast and the cluster radius once,
        /// for less than the main blast. Enemies the main blast already hit are not hit again, even if they
        /// have moved into the ring since.
        /// </summary>
        public void ApplyClusterDamage(Vector3 center, float blastRadius, float clusterRadius, HashSet<EnemyBase> alreadyHit)
        {
            if (EnemyManager.Instance == null || data == null)
                return;

            List<EnemyBase> candidates = EnemyManager.Instance.GetEnemiesInRadius(center, clusterRadius, data.position);
            HashSet<EnemyBase> clusterHit = new HashSet<EnemyBase>();
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyBase enemy = candidates[i];
                if (enemy == null || enemy.IsDead || (alreadyHit != null && alreadyHit.Contains(enemy)) || !clusterHit.Add(enemy))
                    continue;

                float distance = Vector2.Distance(enemy.transform.position, center);
                if (!IsInClusterRing(distance, blastRadius, clusterRadius))
                    continue;

                int damage = Mathf.RoundToInt(currentATK * ClusterDamageMultiplier);
                damage = DamageCalculator.CalculateDamage(damage, enemy.CurrentDEF);
                enemy.TryTakeAttackDamage(damage, DamageType.Physical, this);
            }
        }

        /// <summary>
        /// A ring at the cluster radius shows the exact area hit; the small explosions sit in the middle of the
        /// ring, going off one after another around the impact.
        /// </summary>
        private static void PlayClusterVisuals(Vector3 center, float blastRadius, float clusterRadius)
        {
            AoeBlastVisual.PlayCircle(center, clusterRadius, BlastColor, AoeStyle.Pulse);

            float ringDistance = (blastRadius + clusterRadius) * 0.5f;
            float bombRadius = (clusterRadius - blastRadius) * 0.5f;
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < ClusterBombCount; i++)
            {
                float angle = (startAngle + i * 360f / ClusterBombCount) * Mathf.Deg2Rad;
                Vector2 position = (Vector2)center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringDistance;
                CombatFx.Explosion(position, bombRadius, i * 0.03f, true);
            }
        }

        public override void CollectStatEffects(List<StatEffect> effects)
        {
            base.CollectStatEffects(effects);

            if (HasFirstUpgrade && data != null)
            {
                effects.Add(new StatEffect("4★ UPGRADE", $"Interval {data.attackInterval:0.#}s > {UpgradedAttackInterval:0.#}s", "ATK SPD"));
                effects.Add(new StatEffect("4★ UPGRADE", "Range +1 tile", "RANGE"));
            }

            if (HasSecondUpgrade)
                effects.Add(new StatEffect("5★ UPGRADE", $"Cluster bombs: {ClusterDamageMultiplier * 100f:0}% ATK around the blast", "ATK"));
        }
    }
}
