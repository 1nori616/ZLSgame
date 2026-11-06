using Game.CameraSystem;
using Game.Combat;
using Game.Gravity;
using UnityEngine;

namespace Game.Enemy
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Stunned,
        Dead
    }

    /// <summary>
    /// 基础敌人：看到玩家就水平追，遇到墙或坎会跳，被重力操控时失去行动能力。
    /// 伤害靠身上的 Trigger + DamageSource（玩家自己检测）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("感知")]
        [SerializeField] private float detectRange = 25f;
        [SerializeField] private float verticalTolerance = 8f;

        [Header("移动")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float acceleration = 30f;

        [Header("重力手感")]
        [Tooltip("重力倍率，越大下落越快、越沉")]
        [SerializeField] private float gravityScale = 3.5f;

        [Tooltip("下落时的重力倍率，>1 落得更干脆")]
        [SerializeField] private float fallGravityMultiplier = 1.5f;

        [Header("跳跃")]
        [Tooltip("关掉后敌人只会在地面追，遇到台阶/墙就停下（最稳，不会爬墙）")]
        [SerializeField] private bool allowJump = false;
        [SerializeField] private float jumpForce = 11f;

        [Tooltip("前方多远处检测墙")]
        [SerializeField] private float wallCheckDistance = 0.8f;

        [Tooltip("前方多远处检测是不是悬崖")]
        [SerializeField] private float ledgeCheckDistance = 0.9f;

        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float jumpCooldown = 0.5f;

        /// <summary>理论最大跳跃高度：v² / (2g)，用来判断"跳不跳得上去"。</summary>
        private float MaxJumpHeight => (jumpForce * jumpForce) / (2f * 9.81f * Mathf.Max(0.1f, gravityScale));

        [Header("受击")]
        [SerializeField] private float stunDuration = 0.12f;
        [SerializeField] private float hurtShake = 0.2f;

        [Header("引用（留空自动找）")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        [Tooltip("素材朝向和代码相反时勾上")]
        [SerializeField] private bool invertFacing;

        private Rigidbody2D rb;
        private Collider2D body;
        private GravityAffected gravity;
        private Health health;
        private Transform player;

        private float stunTimer;
        private float jumpTimer;
        private float climbTimer;
        private float stuckTimer;
        private int airJumps;
        private string currentState;   // 当前动画状态，避免每帧重复 Play
        private Vector2 lastPosition;
        private float facing = 1f;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public float Facing => facing;

        private int Sign => GravityWorld.Instance != null ? GravityWorld.Instance.Sign : 1;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            body = GetComponent<Collider2D>();
            gravity = GetComponent<GravityAffected>();
            health = GetComponent<Health>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            rb.freezeRotation = true;
            rb.gravityScale = gravityScale;
            lastPosition = transform.position;

            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDied += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
        }

        private void Update()
        {
            if (State == EnemyState.Dead) return;

            if (player == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
                return;
            }

            if (gravity != null && gravity.IsOverridden)
            {
                State = EnemyState.Stunned;
                return;
            }

            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                State = EnemyState.Stunned;
                return;
            }

            float dx = player.position.x - transform.position.x;
            float dy = player.position.y - transform.position.y;

            bool inSight = Mathf.Abs(dx) <= detectRange && Mathf.Abs(dy) <= verticalTolerance;
            State = inSight ? EnemyState.Chase : EnemyState.Idle;

            if (inSight && !Mathf.Approximately(dx, 0f))
            {
                facing = Mathf.Sign(dx);
                if (spriteRenderer != null) spriteRenderer.flipX = (facing < 0f) ^ invertFacing;
            }
        }

        private void FixedUpdate()
        {
            if (State == EnemyState.Dead || rb == null) return;

            if (jumpTimer > 0f) jumpTimer -= Time.fixedDeltaTime;

            int sign = Sign;
            bool falling = rb.velocity.y * sign < 0f;
            rb.gravityScale = gravityScale * (falling ? fallGravityMultiplier : 1f);

            if (climbTimer > 0f) climbTimer -= Time.fixedDeltaTime;

            bool chasing = State == EnemyState.Chase && (gravity == null || !gravity.IsOverridden);

            float targetSpeed = chasing ? facing * moveSpeed : 0f;

            if (climbTimer > 0f && chasing)
            {
                // 攀爬期：强行保持向前速度，避免撞到箱子侧面被物理清零
                rb.velocity = new Vector2(facing * moveSpeed, rb.velocity.y);
            }
            else
            {
                // 空中的横向加速度更大，落点更准
                float accel = IsGrounded() ? acceleration : acceleration * 3f;
                float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
                rb.velocity = new Vector2(newX, rb.velocity.y);
            }

            if (chasing) DetectStuck();

            UpdateAnimator();
        }

        /// <summary>按水平速度切换待机 / 走路。</summary>
        private void UpdateAnimator()
        {
            if (animator == null) return;

            bool moving = Mathf.Abs(rb.velocity.x) > 0.2f;
            PlayState(moving ? "Enemy_Walk" : "Enemy_Idle");
        }

        private void PlayState(string stateName)
        {
            if (currentState == stateName) return;

            if (!animator.HasState(0, Animator.StringToHash(stateName)))
                stateName = "Enemy_Idle";

            if (currentState == stateName) return;

            currentState = stateName;
            animator.Play(stateName, 0, 0f);
        }

        /// <summary>追着追着几乎没位移 → 认为被卡住，直接跳。</summary>
        private void DetectStuck()
        {
            Vector2 pos = transform.position;
            float moved = Vector2.Distance(pos, lastPosition);
            lastPosition = pos;

            if (moved < 0.01f) stuckTimer += Time.fixedDeltaTime;
            else stuckTimer = 0f;

            if (stuckTimer > 0.25f)
            {
                stuckTimer = 0f;

                // 严格：只有站在地上才能跳。
                // 只要允许空中起跳，贴着墙就能每 0.25 秒蹭一次，最终一定会爬上去。
                if (allowJump && IsGrounded() && jumpTimer <= 0f) DoJump();
                return;
            }

            TryJump();
        }

        /// <summary>前方有墙，或者前方没有地面 → 跳过去。</summary>
        private void TryJump()
        {
            if (!allowJump) return;
            if (jumpTimer > 0f) return;
            if (!IsGrounded()) return;

            if (BlockedAhead())
            {
                // 前方是墙/箱子：先看这个高度跳不跳得上去，跳不上就别白跳
                float step = ObstacleHeight();
                if (step > MaxJumpHeight * 1.05f) return;
                DoJump();
                return;
            }

            if (NoGroundAhead()) DoJump();
        }

        /// <summary>前方障碍物的顶部相对脚底的高度差（正数=在上方）。</summary>
        private float ObstacleHeight()
        {
            if (body == null) return 999f;

            int sign = Sign;
            Bounds b = body.bounds;
            Vector2 origin = new Vector2(b.center.x + facing * 0.5f, b.center.y + 2f);
            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(0f, -1f), 4f, groundLayer);

            if (IsIgnorable(hit.collider) || hit.collider == body) return 999f;

            float feetY = sign > 0 ? b.min.y : b.max.y;
            return (hit.point.y - feetY) * sign;
        }

        private void DoJump()
        {
            jumpTimer = jumpCooldown;
            climbTimer = 0.6f;

            int sign = Sign;
            rb.velocity = new Vector2(facing * moveSpeed, jumpForce * sign);
            rb.WakeUp();
        }

        /// <summary>自己的（含子物体）碰撞体、或者 Trigger，都不能当作地面/墙。</summary>
        private bool IsIgnorable(Collider2D c)
        {
            if (c == null) return true;
            if (c.isTrigger) return true;                        // AttackHitbox、hurtbox 等
            if (c.transform.IsChildOf(transform)) return true;   // 自己的子碰撞体
            return false;
        }

        private bool IsGrounded()
        {
            if (body == null) return false;

            int sign = Sign;
            Bounds b = body.bounds;
            float y = sign > 0 ? b.min.y : b.max.y;
            Vector2 origin = new Vector2(b.center.x, y);

            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(0f, -sign), 0.18f, groundLayer);
            if (IsIgnorable(hit.collider) || hit.collider == body) return false;

            // 只认“朝上”的面：贴着竖直墙时射线会打到墙侧面，
            // 若把它当成地面，敌人就会在墙上连续起跳，看起来像踩着空气台阶往上爬。
            return hit.normal.y * sign > 0.5f;
        }

        private bool BlockedAhead()
        {
            if (body == null) return false;

            Bounds b = body.bounds;
            Vector2 origin = new Vector2(b.center.x, b.center.y);
            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(facing, 0f), wallCheckDistance, groundLayer);
            return !IsIgnorable(hit.collider) && hit.collider != body;
        }

        private bool NoGroundAhead()
        {
            if (body == null) return false;

            int sign = Sign;
            Bounds b = body.bounds;
            Vector2 origin = new Vector2(b.center.x + facing * ledgeCheckDistance, sign > 0 ? b.min.y : b.max.y);
            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(0f, -sign), 0.35f, groundLayer);
            return hit.collider == null || hit.collider == body;
        }

        private void HandleDamaged(DamageInfo info)
        {
            stunTimer = Mathf.Max(stunTimer, stunDuration);

            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(hurtShake);
        }

        private void HandleDied()
        {
            State = EnemyState.Dead;
            if (rb != null) rb.velocity = Vector2.zero;
            enabled = false;
        }

        /// <summary>外部施加僵直（被重力压住等）。</summary>
        public void ApplyStun(float duration)
        {
            stunTimer = Mathf.Max(stunTimer, duration);
            State = EnemyState.Stunned;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * verticalTolerance * 0.5f,
                new Vector3(detectRange * 2f, verticalTolerance, 0.1f));
        }
    }
}
