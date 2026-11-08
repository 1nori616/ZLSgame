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

        [Header("失重")]
        [Tooltip("失重时敌人的移动能力，越小越迟钝（玩家是 0.45）")]
        [Range(0.05f, 1f)]
        [SerializeField] private float zeroGMoveScale = 0.25f;

        [Header("调试")]
        [SerializeField] private bool showDebug = false;

        [Header("贴近玩家")]
        [Tooltip("离玩家这么近就停下，避免一直往里挤")]
        [SerializeField] private float stopDistance = 0.55f;

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
        [Tooltip("受击动画播放时长")]
        [SerializeField] private float hurtDuration = 0.12f;
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
        private float hurtTimer;
        private float airStuckTimer;
        private string currentState;   // 当前动画状态，避免每帧重复 Play
        private Vector2 lastPosition;
        private float facing = 1f;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public float Facing => facing;

        private int Sign => GravityWorld.Instance != null ? GravityWorld.Instance.Sign : 1;

        private static readonly System.Collections.Generic.List<EnemyAI> allEnemies =
            new System.Collections.Generic.List<EnemyAI>();

        private void Awake()
        {
            // 敌人之间互相忽略碰撞：否则它们会挤成一团互相顶住，走不动也跳不起来
            IgnoreOtherEnemies();

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

        private void IgnoreOtherEnemies()
        {
            Collider2D mine = GetComponent<Collider2D>();
            if (mine == null) return;

            for (int i = 0; i < allEnemies.Count; i++)
            {
                EnemyAI other = allEnemies[i];
                if (other == null || other == this) continue;

                Collider2D theirs = other.GetComponent<Collider2D>();
                if (theirs == null) continue;

                // 恢复碰撞：敌人之间会互相推挤，也能用敌人砸敌人
                Physics2D.IgnoreCollision(mine, theirs, false);
            }

            allEnemies.Add(this);
        }

        private void OnGUI()
        {
            if (!showDebug) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.4f);
            if (sp.z < 0f) return;

            bool grounded = IsGrounded();
            bool blocked = BlockedAhead();
            float step = ObstacleHeight();

            string info =
                $"{(grounded ? "地✓" : "地✗")} {(blocked ? "挡✓" : "挡✗")} " +
                $"高={step:0.0} 跳={jumpTimer:0.0} 卡={stuckTimer:0.0} " +
                $"速度={rb.velocity.x:0.0} 允许={allowJump}";

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };

            GUI.Label(new Rect(sp.x - 260f, Screen.height - sp.y - 22f, 520f, 24f), info, style);
        }

        private void OnDestroy()
        {
            allEnemies.Remove(this);
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

            // 被重力甩飞期间完全交给物理，AI 不许改速度（否则会被"拽住"甩不动）
            if (gravity != null && gravity.IsOverridden)
            {
                if (animator != null) UpdateAnimator();
                return;
            }

            if (jumpTimer > 0f) jumpTimer -= Time.fixedDeltaTime;
            if (hurtTimer > 0f) hurtTimer -= Time.fixedDeltaTime;

            int sign = Sign;
            bool falling = rb.velocity.y * sign < 0f;
            rb.gravityScale = gravityScale * (falling ? fallGravityMultiplier : 1f);

            if (climbTimer > 0f) climbTimer -= Time.fixedDeltaTime;

            bool chasing = State == EnemyState.Chase && (gravity == null || !gravity.IsOverridden);

            bool tooClose = player != null &&
                            Vector2.Distance(transform.position, player.position) < stopDistance;

            // 失重时敌人比玩家更迟钝：移动速度和加速度都打折
            bool zeroG = Game.Gravity.GravityWorld.Instance != null
                         && Game.Gravity.GravityWorld.Instance.IsZeroG;
            float controlScale = zeroG ? zeroGMoveScale : 1f;

            float targetSpeed = chasing && !tooClose ? facing * moveSpeed * controlScale : 0f;

            if (climbTimer > 0f && chasing)
            {
                // 攀爬期：强行保持向前速度，避免撞到箱子侧面被物理清零
                rb.velocity = new Vector2(facing * moveSpeed * controlScale, rb.velocity.y);
            }
            else
            {
                // 空中的横向加速度更大，落点更准
                float accel = (IsGrounded() ? acceleration : acceleration * 3f) * controlScale;
                float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, accel * Time.fixedDeltaTime);
                rb.velocity = new Vector2(newX, rb.velocity.y);
            }

            if (chasing) DetectStuck();

            EscapeIfWedged();

            UpdateAnimator();
        }

        /// <summary>按状态切换动画：受击 &gt; 空中（上/下）&gt; 走路/待机。</summary>
        private void UpdateAnimator()
        {
            if (animator == null) return;

            if (hurtTimer > 0f)
            {
                PlayState("Enemy_Hurt");
                return;
            }

            if (!IsGrounded())
            {
                bool rising = rb.velocity.y * Sign > 0f;
                PlayState(rising ? "Enemy_Jump" : "Enemy_Fall");
                return;
            }

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

        /// <summary>卡在半空（钻进平台侧面）时脱困：推离墙面并让它落下去。</summary>
        private void EscapeIfWedged()
        {
            bool almostStill = Mathf.Abs(rb.velocity.x) < 0.3f && Mathf.Abs(rb.velocity.y) < 0.3f;

            if (almostStill && !IsGrounded()) airStuckTimer += Time.fixedDeltaTime;
            else airStuckTimer = 0f;

            if (airStuckTimer < 0.4f) return;

            airStuckTimer = 0f;

            // 反方向推开 + 轻微上抬，随后重力会把它拉下来
            rb.velocity = new Vector2(-facing * 2.5f, 1.5f * Sign);
            rb.WakeUp();
        }

        /// <summary>追着追着几乎没位移 → 认为被卡住，直接跳。</summary>
        private void DetectStuck()
        {
            // 不看位移，看"有没有在动"：
            // 敌人挤在一起互相推挤时位置一直在变，位移判定永远不成立，
            // 但水平速度会一直很低——这才是真正的"走不动"。
            bool blocked = Mathf.Abs(rb.velocity.x) < moveSpeed * 0.45f;

            if (blocked) stuckTimer += Time.fixedDeltaTime;
            else stuckTimer = 0f;

            if (stuckTimer > 0.3f)
            {
                stuckTimer = 0f;

                // 只有站在地上才能跳，避免空中蹭着墙往上爬
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

            // 用 OverlapBox 而不是射线：射线从碰撞体内部/贴地处出发经常打空，
            // 会把"站着"误判成"空中"，表现就是贴地却一直播下落动画。
            float footY = sign > 0 ? b.min.y : b.max.y;
            Vector2 center = new Vector2(b.center.x, footY - sign * 0.04f);
            Vector2 size = new Vector2(b.size.x * 0.8f, 0.12f);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D c = hits[i];
                if (c == null || c == body || IsIgnorable(c)) continue;
                if (c.transform.IsChildOf(transform)) continue;
                return true;
            }

            return false;
        }

        private bool BlockedAhead()
        {
            if (body == null) return false;

            int sign = Sign;
            Bounds b = body.bounds;

            // 用脚前方的一块区域检测，而不是从身体中心打水平射线：
            // 一格高的箱子比身体中心低，水平射线会从箱子顶上穿过去，
            // 结果就是"被挡住却检测不到"，敌人永远不跳。
            float footY = sign > 0 ? b.min.y : b.max.y;
            // 提前 0.75 格发现障碍：留出起跳距离，避免"先撞上去推箱子再跳"
            Vector2 boxCenter = new Vector2(b.center.x + facing * 0.75f, footY + sign * 0.35f);
            Vector2 boxSize = new Vector2(0.45f, 0.8f);

            Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, groundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D c = hits[i];
                if (c == null || c == body || IsIgnorable(c)) continue;
                if (c.transform.IsChildOf(transform)) continue;
                return true;
            }

            // 兜底：更高处的墙
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
            hurtTimer = hurtDuration;
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
