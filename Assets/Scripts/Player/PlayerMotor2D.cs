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
        private bool jumpPending;
        private float pendingJumpForce;

        private float dashTimer;
        private float dashCooldownTimer;
        private float dashDirection = 1f;
        private float facing = 1f;

        private float knockbackTimer;

        private float gravityOverrideTimer;
        private float gravityOverrideAccel;
        private float gravityOverrideMaxSpeed;

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
            if (gravityOverrideTimer > 0f) gravityOverrideTimer -= Time.fixedDeltaTime;

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

            if (IsGravityOverridden) ApplyGravityRamp(sign);
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

            rb.velocity = new Vector2(rb.velocity.x, vy);
        }

        private void ApplyHorizontal(float input)
        {
            float targetSpeed = input * config.moveSpeed;
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

        public void Jump(float force)
        {
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
