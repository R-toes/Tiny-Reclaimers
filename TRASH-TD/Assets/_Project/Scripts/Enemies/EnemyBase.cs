using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Core.Pathfinding;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Enemies
{
    /// <summary>
    /// Abstract base class for all enemies (GDD 1.5).
    /// Handles movement along path, taking damage, being blocked, and reaching exits.
    /// Subclasses override behavior for archetype-specific logic.
    /// 
    /// Parallel design to OperatorBase — demonstrates software reuse through
    /// shared enemy lifecycle with archetype-specific overrides.
    /// </summary>
    public abstract partial class EnemyBase : MonoBehaviour
    {
        private struct FrontAttackTarget
        {
            public OperatorBase Operator;
            public int Distance;

            public FrontAttackTarget(OperatorBase target, int distance)
            {
                Operator = target;
                Distance = distance;
            }
        }

        private const float RangedAttackStopDurationSeconds = 0.75f;

        [Header("Enemy Data")]
        [SerializeField] protected EnemyData data;

        // --- Runtime Stats (scaled by difficulty) ---
        protected int currentHP;
        protected int maxHP;
        protected int currentATK;
        protected int currentDEF;
        protected int currentRES;
        protected float currentMoveSpeed;
        private GridManager gridManager;
        private float trapDamageTimer;
        private bool isPushingBack;
        private Vector3 pushbackStartPosition;
        private Vector3 pushbackTargetPosition;
        private float pushbackElapsed;
        private float pushbackDuration;
        private int pushbackTargetPathIndex;

        // --- Pathfinding ---
        protected List<Vector3> path;
        protected int currentPathIndex;
        protected int difficultyLevel;

        public IReadOnlyList<Vector3> Path => path;
        public int CurrentPathIndex => currentPathIndex;
        public int DifficultyLevel => difficultyLevel;
        public virtual bool IsImmuneToAcidRain => data != null && data.isAcidRainImmune;
        public virtual bool IsImmuneToAcidTiles => data != null && data.isAcidTileImmune;

        // --- State ---
        protected bool isBlocked;
        protected OperatorBase blockingOperator;
        protected Vector3 blockedPosition;
        protected Vector2Int blockedAdjacentGridPos;
        protected bool isDead;
        protected bool isFacingLeft;
        protected float attackTimer;
        private float rangedAttackStopTimer;

        private float chillAmount;
        private float slowTimer;
        private float freezeTimer;
        private float stunTimer;
        private float speedBeforeChill;
        private float chillSlowMultiplier = 1f;
        private bool isFrozen;
        private Combat.IceStatusVisual iceVisual;
        private Combat.IceStatusState appliedIceState;
        private Combat.SluggishGlueVisual glueVisual;
        private bool isStunned;
        private float sluggishTimer;
        private float sluggishMoveSpeedMultiplier = 1f;
        private float sluggishAttackIntervalMultiplier = 1f;
        private bool isSluggish;
        private int resicochetCharge;
        public int ResicochetCharge => resicochetCharge;
        private int highestResicochetCharge;
        public int HighestResicochetCharge => highestResicochetCharge;
        public bool IsEnraged { get; set; }

        public void ApplyResicochetHit(int chargeLevel)
        {
            if (isDead) return;
            resicochetCharge = Mathf.Clamp(chargeLevel, 1, 3);
            highestResicochetCharge = Mathf.Max(highestResicochetCharge, resicochetCharge);
            if (sluggishTimer <= 0f && !isSluggish)
            {
                ApplySluggish();
            }
            UpdateChillVisual();
            UpdateGlueVisual();
        }

        public void ModifyATK(int delta)
        {
            currentATK = Mathf.Max(0, currentATK + delta);
        }
        protected SpriteRenderer[] animationRenderers;
        protected int animationFrameIndex;
        protected float animationFrameTimer;
        private SpriteRenderer[] chillRenderers;
        private Color[] originalRendererColors;

        private bool isInsideHole;
        private float holeStayTimer;
        private Vector3 destinationHolePosition;
        private const float DefaultHoleStayDuration = 1.5f;

        // --- Properties ---
        public EnemyData Data => data;
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public int CurrentATK => currentATK;
        public virtual int CurrentDEF => currentDEF;
        public int CurrentRES => currentRES;
        public float CurrentMoveSpeed => currentMoveSpeed;
        public bool IsBlocked => isBlocked;
        public OperatorBase BlockingOperator => blockingOperator;
        public Vector3 BlockedPosition => blockedPosition;
        public Vector2Int BlockedAdjacentGridPos => blockedAdjacentGridPos;
        public bool IsPushingBack => isPushingBack;
        public bool IsDead => isDead;
        public bool IsFacingLeft => isFacingLeft;
        public bool IsFrozen => isFrozen;
        public bool IsStunned => isStunned;
        public bool IsSluggish => isSluggish;
        public float SluggishTimer => sluggishTimer;
        public float SluggishMoveSpeedMultiplier => sluggishMoveSpeedMultiplier;
        public float SluggishAttackIntervalMultiplier => sluggishAttackIntervalMultiplier;
        public bool IsInsideHole => isInsideHole;

        // --- Devoured (Mlem's tongue) ---
        private bool isDevoured;

        /// <summary>
        /// True from the moment Mlem's tongue grabs this enemy until it is swallowed (or let go). It stands still,
        /// can't be blocked, targeted or damaged, and Mlem moves it.
        /// </summary>
        public bool IsBeingDevoured => isDevoured;

        /// <summary>
        /// True while a swallow is killing this enemy and its on-death effects are switched off (an exploding
        /// enemy doesn't explode, a carrier doesn't spawn). Death overrides check it before their own effect.
        /// </summary>
        protected bool DeathEffectsSuppressed { get; private set; }

        /// <summary>Tiles a large enemy is blocked and stops short of the operator's tile boundary (EnemyData.blockStopBackoff).</summary>
        public float BlockStopBackoff => data != null ? Mathf.Max(0f, data.blockStopBackoff) : 0f;

        /// <summary>The point ahead of the enemy along its path that decides which tile it is blocked on.</summary>
        public Vector3 GetBlockProbePosition(float cellSize)
        {
            float backoff = BlockStopBackoff * cellSize;
            if (backoff <= 0f || path == null || currentPathIndex < 0 || currentPathIndex >= path.Count) return transform.position;
            Vector3 toNext = path[currentPathIndex] - transform.position;
            toNext.z = 0f;
            return toNext.sqrMagnitude > 0.0001f ? transform.position + toNext.normalized * backoff : transform.position;
        }
        public virtual bool CanBeTargetedBy(OperatorPosition opPosition) => true;
        public virtual bool HasTaunt => false;

        /// <summary>
        /// Where this enemy's shots and strikes leave its sprite: <see cref="EnemyData.projectileOrigin"/> from the
        /// visible sprite's centre (raised by visualOffset for flyers), mirrored like the sprite (the art faces right).
        /// </summary>
        public Vector3 ProjectileOrigin
        {
            get
            {
                Transform visual = transform.Find("EnemyVisual");
                Vector3 center = visual != null ? visual.position : transform.position;
                Vector2 offset = data != null ? data.projectileOrigin : Vector2.zero;
                if (isFacingLeft) offset.x = -offset.x;
                Vector3 scale = transform.lossyScale;
                return center + new Vector3(offset.x * scale.x, offset.y * scale.y, 0f);
            }
        }

        public Vector3 VisualPosition
        {
            get
            {
                if (animationRenderers == null)
                    animationRenderers = GetComponentsInChildren<SpriteRenderer>(true);

                for (int i = 0; i < animationRenderers.Length; i++)
                {
                    SpriteRenderer renderer = animationRenderers[i];
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                        renderer.sprite != null)
                    {
                        return renderer.bounds.center;
                    }
                }

                return transform.position;
            }
        }

        public SpriteRenderer GetPrimaryVisualRenderer()
        {
            Transform visual = transform.Find("EnemyVisual");
            if (visual != null)
            {
                SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
                if (visualRenderer != null && visualRenderer.enabled && visualRenderer.sprite != null)
                {
                    return visualRenderer;
                }
            }

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null && renderer.enabled && renderer.gameObject.name != "AirShadow" &&
                    !renderer.gameObject.name.StartsWith("StunStars") && !renderer.gameObject.name.StartsWith("EnragedVisual") &&
                    !renderer.gameObject.name.StartsWith(Combat.IceStatusVisual.ObjectName) &&
                    renderer.sprite != null)
                {
                    return renderer;
                }
            }
            return GetComponent<SpriteRenderer>();
        }
        public float ChillAmount => chillAmount;
        public EnemyMovementType MovementType => data.movementType;

        /// <summary>
        /// An air enemy low enough for ground melee operators to hit (Talon Rat during its swoop).
        /// Ground enemies don't need it: they're always targetable by melee.
        /// </summary>
        public virtual bool IsWithinMeleeReach => false;
        public float DistanceToGoal
        {
            get
            {
                if (path == null || currentPathIndex >= path.Count) return 0f;

                float distance = Vector3.Distance(transform.position, path[currentPathIndex]);
                for (int i = currentPathIndex; i < path.Count - 1; i++)
                {
                    distance += Vector3.Distance(path[i], path[i + 1]);
                }

                return distance;
            }
        }

        // --- Events ---
        /// <summary>Fired when this enemy reaches an exit point.</summary>
        public System.Action<EnemyBase> OnReachedExit;

        /// <summary>Fired when this enemy dies.</summary>
        public System.Action<EnemyBase> OnDied;

        /// <summary>
        /// Initialize this enemy with data and difficulty scaling.
        /// Call after instantiation, before path assignment.
        /// </summary>
        public virtual void Initialize(EnemyData enemyData, int difficultyLevel)
        {
            data = enemyData;
            this.difficultyLevel = difficultyLevel;
            animationRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            animationFrameIndex = 0;
            animationFrameTimer = 0f;
            ApplyAnimationFrame();
            maxHP = data.GetScaledHP(difficultyLevel);
            currentHP = maxHP;
            currentATK = data.GetScaledATK(difficultyLevel);
            currentDEF = data.GetScaledDEF(difficultyLevel);
            currentRES = data.baseRES;
            currentMoveSpeed = data.moveSpeed;
            chillAmount = 0f;
            slowTimer = 0f;
            freezeTimer = 0f;
            stunTimer = 0f;
            speedBeforeChill = currentMoveSpeed;
            chillSlowMultiplier = 1f;
            isFrozen = false;
            isStunned = false;
            Combat.StunStarsVisual.Hide(gameObject);
            isSluggish = false;
            sluggishTimer = 0f;
            sluggishMoveSpeedMultiplier = 1f;
            sluggishAttackIntervalMultiplier = 1f;
            resicochetCharge = 0;
            highestResicochetCharge = 0;
            IsEnraged = false;
            appliedIceState = Combat.IceStatusState.None;
            if (glueVisual != null) glueVisual.Apply(false, 0, null);
            CacheChillRenderers();
            UpdateChillVisual();

            isBlocked = false;
            blockingOperator = null;
            isPushingBack = false;
            pushbackElapsed = 0f;
            isDead = false;
            isFacingLeft = false;
            ApplyFacingVisuals();
            // Ground melee enemies are ready to strike on first contact with a blocker
            // (casters keep their wind-up before the first ranged shot).
            attackTimer = data != null && data.movementType == EnemyMovementType.Ground &&
                          data.archetype != EnemyArchetype.Caster
                ? GetEffectiveAttackInterval()
                : 0f;
            rangedAttackStopTimer = 0f;
            currentPathIndex = 0;
            trapDamageTimer = 0f;
            isInsideHole = false;
            holeStayTimer = 0f;
            destinationHolePosition = Vector3.zero;
        }

        public void SetGridManager(GridManager manager)
        {
            gridManager = manager;
        }

        /// <summary>
        /// Assign a movement path (from A* or predefined waypoints).
        /// </summary>
        public void SetPath(List<Vector3> newPath)
        {
            path = newPath;
            currentPathIndex = 0;

            if (path != null && path.Count > 0)
            {
                transform.position = path[0];
                if (path.Count > 1)
                {
                    UpdateFacing(path[1].x - path[0].x);
                }
            }
        }
    }
}
