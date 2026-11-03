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
    /// 最基础的敌人：看到玩家就水平追，被重力操控时失去行动能力。
    /// 伤害靠身上的 Trigger + DamageSource（玩家自己检测），这里不写攻击逻辑。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("感知")]
        [Tooltip("水平多少距离内能发现玩家")]
        [SerializeField] private float detectRange = 8f;

        [Tooltip("高度差超过这个值就看不见（防止隔着楼层追）")]
        [SerializeField] private float verticalTolerance = 1.5f;

        [Header("移动")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float acceleration = 30f;

        [Header("受击")]
        [Tooltip("被打后失去行动的时间")]
        [SerializeField] private float stunDuration = 0.12f;

        [SerializeField] private float hurtShake = 0.2f;

        [Header("引用（留空自动找）")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Rigidbody2D rb;
        private GravityAffected gravity;
        private Health health;
        private Transform player;

        private float stunTimer;
        private float facing = 1f;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public float Facing => facing;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            gravity = GetComponent<GravityAffected>();
            health = GetComponent<Health>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            rb.freezeRotation = true;

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

            // 被重力操控期间：不思考，只挨摔
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
                if (spriteRenderer != null) spriteRenderer.flipX = facing < 0f;
            }
        }

        private void FixedUpdate()
        {
            if (State == EnemyState.Dead || rb == null) return;

            float targetSpeed = 0f;
            if (State == EnemyState.Chase && (gravity == null || !gravity.IsOverridden))
                targetSpeed = facing * moveSpeed;

            float newX = Mathf.MoveTowards(rb.velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
            rb.velocity = new Vector2(newX, rb.velocity.y);
        }

        private void HandleDamaged(DamageInfo info)
        {
            stunTimer = Mathf.Max(stunTimer, stunDuration);

            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(hurtShake);
        }

        /// <summary>外部施加僵直（比如被重力压住）。</summary>
        public void ApplyStun(float duration)
        {
            stunTimer = Mathf.Max(stunTimer, duration);
            State = EnemyState.Stunned;
        }

        private void HandleDied()
        {
            State = EnemyState.Dead;
            if (rb != null) rb.velocity = Vector2.zero;
            enabled = false;
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