using Game.CameraSystem;
using Game.Combat;
using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// 摔伤：被重力甩出去撞到地面 / 天花板 / 墙时扣血并震屏。
    /// 分方向判定：撞天花板（往上摔）的速度阈值更低，因为上升本来就慢。
    /// </summary>
    public class ImpactDamage : MonoBehaviour
    {
        [Header("阈值")]
        [Tooltip("向下撞（地面、墙）的最低速度")]
        [SerializeField] private float minDownImpactSpeed = 6f;

        [Tooltip("向上撞（天花板）的最低速度——反重力最高速通常只有 5，所以要更低")]
        [SerializeField] private float minUpImpactSpeed = 3.2f;

        [Header("伤害")]
        [SerializeField] private int baseDamage = 1;

        [Tooltip("每超出 1 单位速度，伤害 +1/damagePerSpeed")]
        [SerializeField] private float damagePerSpeed = 6f;

        [Tooltip("撞天花板时的伤害倍率（通常比砸地轻）")]
        [Range(0.1f, 1f)]
        [SerializeField] private float ceilingDamageMultiplier = 0.7f;

        [Tooltip("每多一段连击，摔伤乘 (1 + ChainDamageBonus × (段数-1))")]
        [SerializeField] private float chainDamageBonus = 0.5f;

        [Tooltip("两次摔伤之间的最短间隔，防止一次碰撞触发多次")]
        [SerializeField] private float cooldown = 0.2f;

        [Tooltip("只有在被重力甩过之后这么多秒内，落地才算摔伤（自己跳下来不算）")]
        [SerializeField] private float gravityGrace = 2.5f;

        [Header("受击反馈")]
        [Tooltip("震屏基础强度")]
        [SerializeField] private float hitShake = 0.25f;

        [Tooltip("每点伤害额外增加的震屏强度")]
        [SerializeField] private float shakePerDamage = 0.1f;

        [Tooltip("撞天花板时的额外震屏（哪怕没掉血也给个反馈，可以设 0 关掉）")]
        [SerializeField] private float ceilingShake = 0.25f;

        [Header("打击感")]
        [Tooltip("命中时的顿帧时长（秒），0.05~0.08 比较明显")]
        [SerializeField] private float hitStop = 0.06f;

        [Tooltip("每点伤害额外增加的顿帧时长")]
        [SerializeField] private float hitStopPerDamage = 0.01f;

        [Tooltip("顿帧时长上限")]
        [SerializeField] private float hitStopMax = 0.12f;

        private IDamageable damageable;
        private GravityAffected gravityAffected;
        private float cooldownTimer;

        private void Awake()
        {
            damageable = GetComponent<IDamageable>();
            if (damageable == null) damageable = GetComponentInParent<IDamageable>();

            gravityAffected = GetComponent<GravityAffected>();
            if (gravityAffected == null) gravityAffected = GetComponentInParent<GravityAffected>();
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (damageable == null || damageable.IsDead) return;
            if (cooldownTimer > 0f) return;

            // 只有"被重力甩过"的东西参与碰撞才算摔伤。
            // 注意：判断双方——箱子砸敌人时，挨砸的敌人自己没被施放过。
            GravityAffected otherGravity = collision.collider.GetComponent<GravityAffected>();
            if (otherGravity == null) otherGravity = collision.collider.GetComponentInParent<GravityAffected>();

            bool selfThrown = gravityAffected != null && gravityAffected.TimeSinceLastCast <= gravityGrace;
            bool otherThrown = otherGravity != null && otherGravity.TimeSinceLastCast <= gravityGrace;

            if (!selfThrown && !otherThrown) return;

            float speed = collision.relativeVelocity.magnitude;
            bool isCeiling = collision.contactCount > 0 && collision.GetContact(0).normal.y < -0.5f;

            float threshold = isCeiling ? minUpImpactSpeed : minDownImpactSpeed;
            bool overThreshold = speed >= threshold;

            // 撞天花板即使没到伤害阈值，也给一点震动反馈
            if (isCeiling && !overThreshold && ceilingShake > 0f)
            {
                Shake(ceilingShake * Mathf.Clamp01(speed / Mathf.Max(0.01f, threshold)));
            }

            if (!overThreshold) return;
            if (damageable.IsInvincible) return;

            float amount = baseDamage + (speed - threshold) / Mathf.Max(0.01f, damagePerSpeed);
            if (isCeiling) amount *= ceilingDamageMultiplier;

            float chain = 1f;
            if (gravityAffected != null && gravityAffected.ChainCount > 1)
                chain = 1f + chainDamageBonus * (gravityAffected.ChainCount - 1);

            int damage = Mathf.Max(1, Mathf.RoundToInt(amount * chain));
            cooldownTimer = cooldown;

            DamageInfo info = new DamageInfo(damage, Vector2.zero, gameObject);
            damageable.TakeDamage(in info);

            if (Game.Effects.ComboCounter.Instance != null)
                Game.Effects.ComboCounter.Instance.AddHit();

            Shake(hitShake + shakePerDamage * damage);

            float freeze = Mathf.Min(hitStopMax, hitStop + hitStopPerDamage * damage);
            Game.Effects.HitStop.Freeze(freeze);

            // 音效：撞得狠用重砸音，普通用打击音
            bool heavy = speed >= threshold * 2f;
            Game.Audio.SoundPlayer.Play(heavy ? "slam" : "hit", heavy ? 1f : 0.85f);

        }

        private static void Shake(float amount)
        {
            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(Mathf.Clamp01(amount));
        }
    }
}
