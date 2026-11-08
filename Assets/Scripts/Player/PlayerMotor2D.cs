using Game.Gravity;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 只负责"让身体动起来"：水平速度、重力、跳跃、冲刺、接地检测。
    /// 支持房间级重力翻转（GravityWorld），翻转后"下"变成 +Y。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        [SerializeField] private Collider2D body;

        private Rigidbody2D rb;
        private float targetHorizontal;
        private float targetVertical;
        private bool jumpPending;
        private float pendingJumpForce;

        private float dashTimer;
        private float dashCooldownTimer;
        private float dashDirection = 1f;
        private float facing = 1f;

        private float knockbackTimer;

        [Tooltip("自己飘起来时的横向操控倍率")]
        [Range(0.1f, 1f)]
        [SerializeField] private float selfFloatControlScale = 0.5f;
        private float speedMultiplier = 1f;

        private float gravityOverrideTimer;
        private float gravityOverrideAccel;
        private float gravityOverrideMaxSpeed;
        private float selfLiftMaxHeight;   // >0 时限制"自锁上浮"的最大高度
        private float selfLiftStartY;


        public bool IsGrounded { get; private set; }
        public bool IsDashing => dashTimer > 0f;
        public bool IsKnockedBack => knockbackTimer > 0f;
        public bool IsGravityOverridden => gravityOverrideTimer > 0f;
        public bool CanDash => dashCooldownTimer <= 0f && !IsDashing;
        public float VerticalVelocity => rb != null ? rb.velocity.y : 0f;
        public float HorizontalSpeed => rb != null ? Mathf.Abs(rb.velocity.x) : 0f;
        public float Facing => facing;
        public PlayerConfig Config => config;

        /// <summary>当前重力方向：+1 正常，-1 翻转。</summary>
        public int GravitySign => GravityWorld.Instance != null ? GravityWorld.Instance.Sign : 1;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (body == null) body = GetComponent<Collider2D>();

            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (config != null) rb.gravityScale = config.gravityScale;
        }

        private void FixedUpdate()
        {
            if (config == null || rb == null) return;

            int sign = GravitySign;

            UpdateGroundCheck(sign);

            if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.fixedDeltaTime;
            if (knockbackTimer > 0f) knockbackTimer -= Time.fixedDeltaTime;
            if (gravityOverrideTimer > 0f)
            {
                gravityOverrideTimer -= Time.fixedDeltaTime;
                if (gravityOverrideTimer <= 0f) selfLiftMaxHeight = 0f;   // 覆盖结束，取消高度限制
            }

            if (dashTimer > 0f)
            {
                dashTimer -= Time.fixedDeltaTime;
                rb.velocity = new Vector2(dashDirection * config.dashSpeed, 0f);

                if (dashTimer <= 0f)
                {
                    rb.velocity = new Vector2(rb.velocity.x * 0.5f, rb.velocity.y);
                }
                return;
            }

            bool zeroG = GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG;

            if (zeroG) ApplyZeroGControl();
            else if (IsGravityOverridden) ApplyGravityRamp(sign);
            else ApplyGravityFeel(sign);

            if (knockbackTimer <= 0f) ApplyHorizontal(targetHorizontal);

            if (jumpPending)
            {
                if (IsGrounded && knockbackTimer <= 0f)
                {
                    // 翻转后，跳跃方向也要跟着反
                    rb.velocity = new Vector2(rb.velocity.x, pendingJumpForce * sign);
                }
                jumpPending = false;
            }
        }

        /// <summary>失重：纵向靠上下输入推进，横向加速度打折（行动不便但可控）。</summary>
        private void ApplyZeroGControl()
        {
            rb.gravityScale = 0f;

            float maxSpeed = config != null ? config.zeroGVerticalSpeed : 3.5f;
            float vy = Mathf.MoveTowards(rb.velocity.y, targetVertical * maxSpeed, 20f * Time.fixedDeltaTime);
            rb.velocity = new Vector2(rb.velocity.x, vy);
        }

        private void ApplyGravityFeel(int sign)
        {
            bool falling = rb.velocity.y * sign < 0f;
            float scale = falling
                ? config.gravityScale * config.fallGravityMultiplier
                : config.gravityScale;


            rb.gravityScale = scale;
        }

        private void ApplyGravityRamp(int sign)
        {
            rb.gravityScale = 0f;

            float accel = gravityOverrideAccel * sign;
            float vy = Mathf.Clamp(
                rb.velocity.y + accel * Time.fixedDeltaTime,
                -gravityOverrideMaxSpeed,
                gravityOverrideMaxSpeed);

            // 自锁上浮：超过允许高度就不再往上
            if (selfLiftMaxHeight > 0f && accel > 0f)
            {
                float nextY = rb.position.y + vy * Time.fixedDeltaTime;
                if (nextY - selfLiftStartY > selfLiftMaxHeight)
                    vy = Mathf.Min(vy, 0f);
            }

            rb.velocity = new Vector2(rb.velocity.x, vy);
        }

        private void ApplyHorizontal(float input)
        {
            float scale = (GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG && config != null)
                ? config.zeroGControlScale
                : 1f;

            // 自己飘起来的时候横向操控减半，避免"飘着横穿地图"
            float selfFloat = IsGravityOverridden ? selfFloatControlScale : 1f;

            float targetSpeed = input * config.moveSpeed * speedMultiplier * scale * selfFloat;
            float rate = Mathf.Approximately(input, 0f) ? config.deceleration : config.acceleration;
            float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, rate * Time.fixedDeltaTime);
            rb.velocity = new Vector2(newX, rb.velocity.y);

            if (!Mathf.Approximately(input, 0f)) facing = Mathf.Sign(input);
        }

        private void UpdateGroundCheck(int sign)
        {
            if (body == null) { IsGrounded = false; return; }

            Bounds b = body.bounds;
            float probeY = sign > 0
                ? b.min.y - config.groundCheckDistance
                : b.max.y + config.groundCheckDistance;

            Vector2 center = new Vector2(b.center.x, probeY);
            Vector2 size = new Vector2(b.size.x * config.groundCheckWidthScale, config.groundCheckDistance * 2f);

            IsGrounded = false;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, config.groundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == body || hits[i].isTrigger) continue;
                IsGrounded = true;
                break;
            }
        }

        public void SetHorizontal(float input)
        {
            targetHorizontal = Mathf.Clamp(input, -1f, 1f);
        }

        /// <summary>失重时的上下推进输入（-1 下 / +1 上）。</summary>
        public void SetVertical(float input)
        {
            targetVertical = Mathf.Clamp(input, -1f, 1f);
        }

        /// <summary>调试用：当前读到的纵向输入。</summary>
        public float TargetVertical => targetVertical;

        public void Jump(float force)
        {
            // 失重状态：跳跃键 = 向上推进（像喷气背包，可连点）
            if (GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG)
            {
                float thrust = config != null ? config.zeroGThrust : 4.5f;
                rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y + thrust);
                return;
            }

            if (!IsGrounded || IsDashing) return;
            jumpPending = true;
            pendingJumpForce = force;
        }

        public void CutJump()
        {
            int sign = GravitySign;
            if (rb == null || rb.velocity.y * sign <= 0f) return;
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * config.jumpCutMultiplier);
        }

        public void Dash(float direction)
        {
            if (config == null || !CanDash) return;

            dashDirection = Mathf.Approximately(direction, 0f) ? facing : Mathf.Sign(direction);
            facing = dashDirection;
            dashTimer = config.dashDuration;
            dashCooldownTimer = config.dashCooldown;
        }

        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            if (rb == null) return;
            rb.velocity = velocity;
            knockbackTimer = Mathf.Max(0.01f, duration);

            if (!Mathf.Approximately(velocity.x, 0f)) facing = -Mathf.Sign(velocity.x);
        }


        /// <summary>慢动作补偿：世界变慢时让玩家仍保持正常移动速度。</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 10f);
        }

        /// <summary>开始一次"自锁上浮"，限制最高能升多少。</summary>
        public void StartSelfLift(float maxHeight)
        {
            selfLiftStartY = rb != null ? rb.position.y : transform.position.y;
            selfLiftMaxHeight = Mathf.Max(0.1f, maxHeight);
        }

        public void ApplyGravityOverride(float acceleration, float maxSpeed, float duration)
        {
            if (rb == null) return;

            gravityOverrideAccel = acceleration;
            gravityOverrideMaxSpeed = Mathf.Abs(maxSpeed);
            gravityOverrideTimer = Mathf.Max(0.01f, duration);
            rb.gravityScale = 0f;
            rb.WakeUp();
        }
    }
}
