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
    /// - 2x Upgraded (5★): after the blast, cluster bombs go off together on and around the impact, each with
    ///   its own blast circle. A bomb deals 30% of the main blast's damage, and each enemy takes at most one bomb.
    /// </summary>
    public sealed class BasurocketOperator : SniperOperator
    {
        /// <summary>Blast radius in tiles (caster splash is 1.5).</summary>
        public const float BlastRadiusTiles = 1.75f;
        private const float BlastDamageMultiplier = 1f;
        private static readonly Color BlastColor = new Color(1f, 0.62f, 0.12f); // Yellow-orange: never the Eradicator's crimson

        /// <summary>1st upgrade: seconds between rockets (2.8 in the data asset before it).</summary>
        public const float UpgradedAttackInterval = 2.3f;

        /// <summary>2nd upgrade: each cluster bomb blasts its own circle of this radius.</summary>
        public const float ClusterBombRadiusTiles = 1f;

        /// <summary>How far from the impact a cluster bomb reaches: bombs land on the rim of the main blast.</summary>
        public const float ClusterRadiusTiles = BlastRadiusTiles + ClusterBombRadiusTiles;

        /// <summary>A bomb's share of the damage the main blast deals to the same enemy.</summary>
        public const float ClusterDamageMultiplier = 0.3f;
        public const float ClusterDelaySeconds = 0.18f;

        /// <summary>One bomb on the impact, the rest evenly around the rim of the main blast.</summary>
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
        /// Where the cluster bombs land: one on the impact, the rest around the rim of the main blast.
        /// Together their circles cover the whole main blast.
        /// </summary>
        public static Vector3[] GetClusterBombPositions(Vector3 center, float cellSize, float startAngleDegrees)
        {
            var positions = new Vector3[ClusterBombCount];
            positions[0] = center;

            float distance = BlastRadiusTiles * cellSize;
            int bombsAround = ClusterBombCount - 1;
            for (int i = 0; i < bombsAround; i++)
            {
                float angle = (startAngleDegrees + i * 360f / bombsAround) * Mathf.Deg2Rad;
                positions[i + 1] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
            }
            return positions;
        }

        /// <summary>What the main blast deals to an enemy with this DEF.</summary>
        public static int GetBlastDamage(int atk, int def) =>
            DamageCalculator.CalculateDamage(Mathf.RoundToInt(atk * BlastDamageMultiplier), def);

        /// <summary>What a cluster bomb deals to the same enemy: a share of the main blast's damage, at least 1.</summary>
        public static int GetClusterBombDamage(int atk, int def) =>
            Mathf.Max(1, Mathf.RoundToInt(GetBlastDamage(atk, def) * ClusterDamageMultiplier));

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

                int damage = GetBlastDamage(currentATK, blastTarget.CurrentDEF);
                blastTarget.TryTakeAttackDamage(damage, DamageType.Physical, this);
            }

            // 2nd upgrade: cluster bombs on and around the blast.
            if (!HasSecondUpgrade) return;

            Vector3[] bombs = GetClusterBombPositions(center, cellSize, Random.Range(0f, 360f));
            float bombRadius = ClusterBombRadiusTiles * cellSize;
            if (Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(ClusterRoutine(bombs, bombRadius));
            }
            else
            {
                // Not playing (EditMode tests): no delay.
                var hitByBomb = new HashSet<EnemyBase>();
                for (int i = 0; i < bombs.Length; i++)
                    ApplyClusterBomb(bombs[i], bombRadius, hitByBomb);
            }
        }

        private IEnumerator ClusterRoutine(Vector3[] bombs, float bombRadius)
        {
            yield return new WaitForSeconds(ClusterDelaySeconds);
            if (!isDeployed) yield break;

            PlayImpactSound(); // one sound for the whole cluster
            var hitByBomb = new HashSet<EnemyBase>();
            for (int i = 0; i < bombs.Length; i++)
            {
                PlayClusterBombVisuals(bombs[i], bombRadius);
                ApplyClusterBomb(bombs[i], bombRadius, hitByBomb);
            }
        }

        /// <summary>
        /// One bomb hits every enemy in its circle. Enemies the main blast hit can be hit again, but each enemy
        /// takes only one bomb per cluster, so overlapping circles do not stack.
        /// </summary>
        private void ApplyClusterBomb(Vector3 position, float bombRadius, HashSet<EnemyBase> hitByBomb)
        {
            if (EnemyManager.Instance == null || data == null)
                return;

            List<EnemyBase> targets = EnemyManager.Instance.GetEnemiesInRadius(position, bombRadius, data.position);
            for (int i = 0; i < targets.Count; i++)
            {
                EnemyBase enemy = targets[i];
                if (enemy == null || enemy.IsDead || !hitByBomb.Add(enemy))
                    continue;

                int damage = GetClusterBombDamage(currentATK, enemy.CurrentDEF);
                enemy.TryTakeAttackDamage(damage, DamageType.Physical, this);
            }
        }

        /// <summary>The bomb's own circle shows the exact area it hits.</summary>
        private static void PlayClusterBombVisuals(Vector3 position, float bombRadius)
        {
            AoeBlastVisual.PlayCircle(position, bombRadius, BlastColor, AoeStyle.Pulse);
            CombatFx.Explosion(position, bombRadius * 0.5f, 0f, true);
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
                effects.Add(new StatEffect("5★ UPGRADE", $"{ClusterBombCount} cluster bombs: {ClusterDamageMultiplier * 100f:0}% blast damage each", "ATK"));
        }
    }
}
