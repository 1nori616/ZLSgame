using System;
using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// 挂在"可以被改重力"的物体上：敌人、箱子、石块。
    /// 施放时进入"匀加速"模式；同时检测是否站在地面（跟随世界重力方向）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GravityAffected : MonoBehaviour
    {
        [Tooltip("原始重力倍率，Awake 时会自动读取 Rigidbody2D 的值覆盖这里")]
        [SerializeField] private float baseGravityScale = 1f;

        [Tooltip("两次施放间隔小于这个时间，算作一次连段")]
        [SerializeField] private float chainWindow = 2.5f;

        [Header("反重力（往上飘）")]
        [SerializeField] private float antiAcceleration = 14f;
        [SerializeField] private float antiMaxSpeed = 5f;

        [Header("加重（往下砸）")]
        [SerializeField] private float heavyAcceleration = 45f;
        [SerializeField] private float heavyMaxSpeed = 24f;

        [Header("接地检测")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [Range(0.5f, 1f)] [SerializeField] private float groundCheckWidthScale = 0.9f;
        [SerializeField] private float groundCheckDistance = 0.06f;

        private Rigidbody2D rb;
        private Collider2D body;
        private float overrideTimer;
        private float lastCastTime = -999f;

        public bool IsOverridden => overrideTimer > 0f;
        public bool IsGrounded { get; private set; }
        public float CurrentScale => rb != null ? rb.gravityScale : baseGravityScale;
        public float BaseScale => baseGravityScale;
        public float RemainingTime => Mathf.Max(0f, overrideTimer);

        public int ChainCount { get; private set; }
        public GravityMode LastMode { get; private set; }
        public float ChainWindow => chainWindow;
        public bool IsValidTarget => this != null && isActiveAndEnabled;

        private int Sign => GravityWorld.Instance != null ? GravityWorld.Instance.Sign : 1;

        /// <summary>站在地面（相对当前重力方向）时被"加重"，触发"被压住"的反馈。</summary>
        public event Action<GravityAffected> OnGroundSlam;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            body = GetComponent<Collider2D>();
            if (rb != null) baseGravityScale = rb.gravityScale;
            ChainCount = 0;
        }

        private void FixedUpdate()
        {
            UpdateGroundCheck();

            if (overrideTimer <= 0f) return;

            overrideTimer -= Time.fixedDeltaTime;
            if (overrideTimer <= 0f)
            {
                ResetGravity();
                return;
            }

            ApplyRamp();
        }

        private void UpdateGroundCheck()
        {
            if (body == null) { IsGrounded = false; return; }

            int sign = Sign;
            Bounds b = body.bounds;
            float probeY = sign > 0
                ? b.min.y - groundCheckDistance
                : b.max.y + groundCheckDistance;

            Vector2 center = new Vector2(b.center.x, probeY);
            Vector2 size = new Vector2(b.size.x * groundCheckWidthScale, groundCheckDistance * 2f);

            IsGrounded = false;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == body || hits[i].isTrigger) continue;
                IsGrounded = true;
                break;
            }
        }

        private void ApplyRamp()
        {
            if (rb == null) return;

            rb.gravityScale = 0f;

            int sign = Sign;
            float accel = (LastMode == GravityMode.AntiGravity ? antiAcceleration : -heavyAcceleration) * sign;
            float maxSpeed = LastMode == GravityMode.AntiGravity ? antiMaxSpeed : heavyMaxSpeed;

            float vy = Mathf.Clamp(rb.velocity.y + accel * Time.fixedDeltaTime, -maxSpeed, maxSpeed);
            rb.velocity = new Vector2(rb.velocity.x, vy);
        }

        public void ApplyOverride(float duration, GravityMode mode)
        {
            if (rb == null) return;

            ChainCount = (Time.time - lastCastTime) <= chainWindow ? ChainCount + 1 : 1;
            lastCastTime = Time.time;
            LastMode = mode;

            overrideTimer = Mathf.Max(0.01f, duration);
            rb.gravityScale = 0f;
            rb.WakeUp();

            if (mode == GravityMode.Heavy && IsGrounded)
                OnGroundSlam?.Invoke(this);
        }

        public void ResetGravity()
        {
            overrideTimer = 0f;
            if (rb != null) rb.gravityScale = baseGravityScale;
        }

        public void SetPermanent(float scale)
        {
            baseGravityScale = scale;
            overrideTimer = 0f;
            if (rb != null) rb.gravityScale = scale;
        }

        public void ResetChain()
        {
            ChainCount = 0;
            lastCastTime = -999f;
        }
    }
}
