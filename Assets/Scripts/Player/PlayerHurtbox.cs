using Game.Combat;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 玩家的受击区（一个 Trigger 子物体）。
    /// 按我们定的规则：由"被打的一方"自己检测——碰到带 DamageSource 的东西就掉血。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerHurtbox : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

            DamageSource source = other.GetComponent<DamageSource>();
            if (source == null) source = other.GetComponentInParent<DamageSource>();
            if (source == null) return;

            DamageInfo info = new DamageInfo(source.Amount, source.Knockback, source.gameObject);
            playerHealth.TakeDamage(in info);
            source.Consume();
        }
    }
}
