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
        Attack,
        Stunned,
        Dead
    }

    /// <summary>
    /// 基础敌人：
    ///   - 看到玩家就追，遇到墙/坎会跳，卡住会跳
    ///   - 靠近后进入攻击：前摇 → 判定 → 后摇 → 冷却
    ///   - 伤害只在"判定窗口"内生效，所以玩家能躲
    ///   - 被重力操控时失去行动
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
        [SerializeField] private float gravityScale = 3.5f;
        [SerializeField] private float fallGravityMultiplier = 1.5f;

        [Header("跳跃")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float wallCheckDistance = 0.55f;
        [SerializeField] private float ledgeCheckDistance = 0.7f;
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float jumpCooldown = 0.5f;

        [Header("攻击")]
        [Tooltip("进入攻击的距离")]
        [SerializeField] private float attackRange = 1.3f;

        [Tooltip("前摇：抬手到出手的时间，玩家靠这个躲")]
        [SerializeField] private float attackWindup = 0.35f;

        [Tooltip("判定窗口：这段时间内碰到才掉血")]
        [SerializeField] private float attackActive = 0.15f;

        [Tooltip("后摇：收招，不能动")]
        [SerializeField] private float attackRecovery = 0.3f;

        [Tooltip("两次攻击之间的冷却")]
        [SerializeField] private float attackCooldown = 1f;

        [Tooltip("攻击判定用的 Trigger 碰撞体（留空自动找子物体上的 DamageSource）")]
        [SerializeField] private Collider2D[] attackHitboxes;

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
        private float attackTimer;      // 当前攻击阶段剩余时间
        private float cooldownTimer;
        private int attackPhase;        // 0=前摇 1=判定 2=后摇

        private Vector2 lastPosition;

        // 动画参数（不存在就跳过，避免刷警告）
        private bool hasSpeedParam, hasAttackParam, hasHurtParam, hasDeadParam;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HurtHash = Animator.StringToHash("Hurt");
        private static readonly int DeadHash = Animator.StringToHash("Dead");

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public float Facing => facing;
        private float facing = 1f;

        private int Sign => GravityWorld.Instance != null ? GravityWorld.Instance.Sign : 1;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            body = GetComponent<Collider2D>();
            gravity = GetComponent<GravityAffected>();
            health = GetComponent<Health>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (attackHitboxes == null || attackHitboxes.Length == 0)
            {
                // 只收 Trigger 碰撞体：万一 DamageSource 挂在敌人本体上，
                // 关掉它会把敌人的物理身体也关掉（直接掉穿地板）。
                DamageSource[] sources = GetComponentsInChildren<DamageSource>(true);
                System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>();

                for (int i = 0; i < sources.Length; i++)
                {
                    Collider2D c = sources[i].GetComponent<Collider2D>();
                    if (c != null && c.isTrigger) hits.Add(c);
                }

                attackHitboxes = hits.ToArray();

                if (attackHitboxes.Length == 0)
                    Debug.LogWarning($"[EnemyAI] {name} 没有找到 Trigger 型的 DamageSource，攻击不会造成伤害。" +
                                     "请把 DamageSource 放到子物体（带 Is Trigger 的碰撞体）上。", this);
            }

            rb.freezeRotation = true;
            rb.gravityScale = gravityScale;
            lastPosition = transform.position;

            CacheAnimatorParams();
            SetHitboxes(false);

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

        private void CacheAnimatorParams()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            AnimatorControllerParameter[] ps = animator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].nameHash == SpeedHash) hasSpeedParam = true;
                else if (ps[i].nameHash == AttackHash) hasAttackParam = true;
                else if (ps[i].nameHash == HurtHash) hasHurtParam = true;
                else if (ps[i].nameHash == DeadHash) hasDeadParam = true;
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
                CancelAttack();
                State = EnemyState.Stunned;
                return;
            }

            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                CancelAttack();
                State = EnemyState.Stunned;
                return;
            }

            float dx = player.position.x - transform.position.x;
            float dy = player.position.y - transform.position.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);

            bool inSight = Mathf.Abs(dx) <= detectRange && Mathf.Abs(dy) <= verticalTolerance;

            if (State == EnemyState.Attack)
            {
                TickAttack();
                return;
            }

            if (!inSight)
            {
                State = EnemyState.Idle;
                return;
            }

            // 死区：玩家几乎在正上方/正下方时不要来回翻转朝向（会看起来在抖）
            if (Mathf.Abs(dx) > 0.2f)
            {
                facing = Mathf.Sign(dx);
                if (spriteRenderer != null) spriteRenderer.flipX = (facing < 0f) ^ invertFacing;
            }

            if (distance <= attackRange && cooldownTimer <= 0f && IsGrounded())
            {
                BeginAttack();
                return;
            }

            State = EnemyState.Chase;
        }

        private void BeginAttack()
        {
            State = EnemyState.Attack;
            attackPhase = 0;
            attackTimer = attackWindup;
            SetHitboxes(false);
            PlayAttackAnim();
        }

        private void TickAttack()
        {
            attackTimer -= Time.deltaTime;
            cooldownTimer -= Time.deltaTime;

            if (attackTimer > 0f) return;

            attackPhase++;

            if (attackPhase == 1)
            {
                // 进入判定窗口
                attackTimer = attackActive;
                SetHitboxes(true);
                return;
            }

            if (attackPhase == 2)
            {
                // 收招
                attackTimer = attackRecovery;
                SetHitboxes(false);
                return;
            }

            // 结束
            SetHitboxes(false);
            cooldownTimer = attackCooldown;
            State = EnemyState.Chase;
        }

        private void CancelAttack()
        {
            if (State == EnemyState.Attack) SetHitboxes(false);
            attackPhase = 0;
            attackTimer = 0f;
        }

        private void SetHitboxes(bool on)
        {
            if (attackHitboxes == null) return;

            for (int i = 0; i < attackHitboxes.Length; i++)
                if (attackHitboxes[i] != null) attackHitboxes[i].enabled = on;
        }

        private void FixedUpdate()
        {
            if (State == EnemyState.Dead || rb == null) return;

            if (cooldownTimer > 0f) cooldownTimer -= Time.fixedDeltaTime;
            if (jumpTimer > 0f) jumpTimer -= Time.fixedDeltaTime;

            int sign = Sign;

            // 被重力操控期间不要把重力倍率改回去，否则会覆盖 GravityAffected 的 0，
            // 表现为"飘一下就掉下来"。
            if (gravity == null || !gravity.IsOverridden)
            {
                bool falling = rb.velocity.y * sign < 0f;
                rb.gravityScale = gravityScale * (falling ? fallGravityMultiplier : 1f);
            }

            bool canMove = State == EnemyState.Chase && (gravity == null || !gravity.IsOverridden);

            if (climbTimer > 0f) climbTimer -= Time.fixedDeltaTime;

            float targetSpeed = canMove ? facing * moveSpeed : 0f;

            if (climbTimer > 0f && canMove)
            {
                rb.velocity = new Vector2(facing * moveSpeed, rb.velocity.y);
            }
            else
            {
                float accel = IsGrounded() ? acceleration : acceleration * 3f;
                float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
                rb.velocity = new Vector2(newX, rb.velocity.y);
            }

            if (hasSpeedParam && animator != null) animator.SetFloat(SpeedHash, Mathf.Abs(rb.velocity.x));

            if (canMove) DetectStuck();
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
                if (jumpTimer <= 0f) DoJump();
                return;
            }

            TryJump();
        }

        private void TryJump()
        {
            if (jumpTimer > 0f) return;
            if (!IsGrounded()) return;

            if (BlockedAhead())
            {
                float step = ObstacleHeight();
                if (step > MaxJumpHeight * 1.05f) return;
                DoJump();
                return;
            }

            if (NoGroundAhead()) DoJump();
        }

        private float MaxJumpHeight => (jumpForce * jumpForce) / (2f * 9.81f * Mathf.Max(0.1f, gravityScale));

        private void DoJump()
        {
            jumpTimer = jumpCooldown;
            climbTimer = 0.6f;

            int sign = Sign;
            rb.velocity = new Vector2(facing * moveSpeed, jumpForce * sign);
            rb.WakeUp();
        }

        private float ObstacleHeight()
        {
            if (body == null) return 999f;

            int sign = Sign;
            Bounds b = body.bounds;
            Vector2 origin = new Vector2(b.center.x + facing * 0.5f, b.center.y + 2f);
            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(0f, -1f), 4f, groundLayer);

            if (hit.collider == null || hit.collider == body) return 999f;

            float feetY = sign > 0 ? b.min.y : b.max.y;
            return (hit.point.y - feetY) * sign;
        }

        private bool IsGrounded()
        {
            if (body == null) return false;

            int sign = Sign;
            Bounds b = body.bounds;
            float y = sign > 0 ? b.min.y : b.max.y;
            Vector2 origin = new Vector2(b.center.x, y);

            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(0f, -sign), 0.18f, groundLayer);
            return hit.collider != null && hit.collider != body;
        }

        private bool BlockedAhead()
        {
            if (body == null) return false;

            Bounds b = body.bounds;
            Vector2 origin = new Vector2(b.center.x, b.center.y);
            RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(facing, 0f), wallCheckDistance, groundLayer);
            return hit.collider != null && hit.collider != body;
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

        private void PlayAttackAnim()
        {
            if (hasAttackParam && animator != null) animator.SetTrigger(AttackHash);
        }

        private void HandleDamaged(DamageInfo info)
        {
            stunTimer = Mathf.Max(stunTimer, stunDuration);
            CancelAttack();

            if (hasHurtParam && animator != null) animator.SetTrigger(HurtHash);

            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(hurtShake);
        }

        private void HandleDied()
        {
            State = EnemyState.Dead;
            CancelAttack();
            if (rb != null) rb.velocity = Vector2.zero;
            if (hasDeadParam && animator != null) animator.SetBool(DeadHash, true);
            enabled = false;
        }

        /// <summary>外部施加僵直（被重力压住等）。</summary>
        public void ApplyStun(float duration)
        {
            stunTimer = Mathf.Max(stunTimer, duration);
            CancelAttack();
            State = EnemyState.Stunned;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * verticalTolerance * 0.5f,
                new Vector3(detectRange * 2f, verticalTolerance, 0.1f));

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
