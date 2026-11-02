using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// 挂在"可以被改重力"的物体上：敌人、箱子、石块。
    /// 施放时进入"匀加速"模式：速度从当前值线性加速到上限（由慢到快）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GravityAffected : MonoBehaviour
    {
        [Tooltip("原始重力倍率，Awake 时会自动读取 Rigidbody2D 的值覆盖这里")]
        [SerializeField] private float baseGravityScale = 1f;

        [Tooltip("两次施放间隔小于这个时间，算作一次连段")]
        [SerializeField] private float chainWindow = 2.5f;

        [Header("反重力（往上飘）")]
        [Tooltip("加速度：越大越快达到最高速")]
        [SerializeField] private float antiAcceleration = 14f;

        [Tooltip("上升速度上限")]
        [SerializeField] private float antiMaxSpeed = 5f;

        [Header("加重（往下砸）")]
        [Tooltip("加速度：越大砸得越猛")]
        [SerializeField] private float heavyAcceleration = 45f;

        [Tooltip("下坠速度上限")]
        [SerializeField] private float heavyMaxSpeed = 24f;

        private Rigidbody2D rb;
        private float overrideTimer;
        private float lastCastTime = -999f;

        public bool IsOverridden => overrideTimer > 0f;
        public float CurrentScale => rb != null ? rb.gravityScale : baseGravityScale;
        public float BaseScale => baseGravityScale;
        public float RemainingTime => Mathf.Max(0f, overrideTimer);

        public int ChainCount { get; private set; }
        public GravityMode LastMode { get; private set; }
        public float ChainWindow => chainWindow;
        public bool IsValidTarget => this != null && isActiveAndEnabled;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb != null) baseGravityScale = rb.gravityScale;
            ChainCount = 0;
        }

        private void FixedUpdate()
        {
            if (overrideTimer <= 0f) return;

            overrideTimer -= Time.fixedDeltaTime;
            if (overrideTimer <= 0f)
            {
                ResetGravity();
                return;
            }

            ApplyRamp();
        }

        /// <summary>匀加速：速度线性变化，直到上限。</summary>
        private void ApplyRamp()
        {
            rb.gravityScale = 0f;

            float accel = LastMode == GravityMode.AntiGravity ? antiAcceleration : -heavyAcceleration;
            float maxSpeed = LastMode == GravityMode.AntiGravity ? antiMaxSpeed : heavyMaxSpeed;

            float vy = Mathf.Clamp(rb.velocity.y + accel * Time.fixedDeltaTime, -maxSpeed, maxSpeed);
            rb.velocity = new Vector2(rb.velocity.x, vy);
        }

        /// <summary>施放重力操控，duration 秒后自动恢复。</summary>
        public void ApplyOverride(float duration, GravityMode mode)
        {
            if (rb == null) return;

            ChainCount = (Time.time - lastCastTime) <= chainWindow ? ChainCount + 1 : 1;
            lastCastTime = Time.time;
            LastMode = mode;

            overrideTimer = Mathf.Max(0.01f, duration);
            rb.gravityScale = 0f;
            rb.WakeUp();
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
