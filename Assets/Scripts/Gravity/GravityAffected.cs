using System;
using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// 挂在"可以被改重力"的物体上：敌人、箱子、石块。
    /// 施放时进入"匀加速 + 水平抛掷"模式：纵向按加速度走，横向给一个初速度并逐渐衰减。
    /// 这样重力不只是上下，还能把东西"甩"向指定方向。
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

        [Header("水平抛掷")]
        [Tooltip("横向速度每秒衰减多少，越大甩得越近")]
        [SerializeField] private float throwDrag = 18f;

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

        /// <summary>距上一次被重力施放过了多久（秒）。用于判断"摔伤"是否成立。</summary>
        public float TimeSinceLastCast => Time.time - lastCastTime;

        public int ChainCount { get; private set; }
        public GravityMode LastMode { get; private set; }
        public float ChainWindow => chainWindow;
        public bool IsValidTarget => this != null && isActiveAndEnabled;

        private int Sign => GravityWorld.Instance != null ? GravityWorld.Instance.Sign : 1;

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

            // 横向速度逐渐衰减，让"甩出去"有距离感，而不是一直飞
            float vx = Mathf.MoveTowards(rb.velocity.x, 0f, throwDrag * Time.fixedDeltaTime);

            rb.velocity = new Vector2(vx, vy);
        }

        /// <summary>
        /// 施放重力。
        /// impulse 是水平/纵向初速度：Q 用来"拉向身前"，E 用来"甩向前方"。
        /// </summary>
        public void ApplyOverride(float duration, GravityMode mode, Vector2 impulse = default)
        {
            if (rb == null) return;

            ChainCount = (Time.time - lastCastTime) <= chainWindow ? ChainCount + 1 : 1;
            lastCastTime = Time.time;
            LastMode = mode;

            overrideTimer = Mathf.Max(0.01f, duration);
            rb.gravityScale = 0f;

            if (impulse != Vector2.zero)
                rb.velocity = new Vector2(impulse.x, impulse.y);

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
