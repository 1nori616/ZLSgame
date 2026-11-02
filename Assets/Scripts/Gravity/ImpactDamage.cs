using Game.Combat;
using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// 摔伤：被重力甩出去、撞到地面/天花板/墙时扣血。
    /// 连段越高，伤害越高（由 GravityAffected.ChainCount 提供）。
    /// </summary>
    public class ImpactDamage : MonoBehaviour
    {
        [Tooltip("低于这个撞击速度不受伤")]
        [SerializeField] private float minImpactSpeed = 6f;

        [SerializeField] private int baseDamage = 1;

        [Tooltip("每超出 1 单位速度，伤害 +1/damagePerSpeed")]
        [SerializeField] private float damagePerSpeed = 6f;

        [Tooltip("每多一段连击，摔伤乘 (1 + ChainDamageBonus × (段数-1))")]
        [SerializeField] private float chainDamageBonus = 0.5f;

        [Tooltip("两次摔伤之间的最短间隔，防止一次碰撞触发多次")]
        [SerializeField] private float cooldown = 0.2f;

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
            if (damageable == null || damageable.IsDead || damageable.IsInvincible) return;
            if (cooldownTimer > 0f) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            float baseAmount = baseDamage + (speed - minImpactSpeed) / Mathf.Max(0.01f, damagePerSpeed);

            float chain = 1f;
            if (gravityAffected != null && gravityAffected.ChainCount > 1)
                chain = 1f + chainDamageBonus * (gravityAffected.ChainCount - 1);

            int damage = Mathf.Max(1, Mathf.RoundToInt(baseAmount * chain));
            cooldownTimer = cooldown;

            DamageInfo info = new DamageInfo(damage, Vector2.zero, gameObject);
            damageable.TakeDamage(in info);
        }
    }
}
