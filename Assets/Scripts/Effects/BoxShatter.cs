using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 箱子是一次性武器：被甩出去撞到敌人时炸裂，对范围内敌人造成伤害并击退。
    /// 挂在箱子上（P_Box）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BoxShatter : MonoBehaviour
    {
        [Tooltip("撞击时箱子至少要有多快才炸")]
        [SerializeField] private float triggerSpeed = 7f;

        [Tooltip("爆炸半径")]
        [SerializeField] private float radius = 2.4f;

        [Tooltip("对范围内每个敌人的伤害")]
        [SerializeField] private int damage = 35;

        [Tooltip("击退力度")]
        [SerializeField] private float knockback = 9f;

        [Tooltip("向上击飞")]
        [SerializeField] private float knockbackLift = 4f;

        [Tooltip("被施放后多少秒内算“被甩出去”")]
        [SerializeField] private float thrownWindow = 3f;

        private Rigidbody2D rb;
        private Gravity.GravityAffected gravity;
        private bool shattered;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            gravity = GetComponent<Gravity.GravityAffected>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (shattered) return;

            // 只有"被重力甩出去"的箱子才会炸：防止敌人自己撞上来也爆
            bool thrown = gravity != null && gravity.TimeSinceLastCast <= thrownWindow;
            if (!thrown) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < triggerSpeed) return;

            // 只对"敌人"触发
            Enemy.EnemyAI enemy = collision.collider.GetComponentInParent<Enemy.EnemyAI>();
            if (enemy == null) return;

            Vector2 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (Vector2)transform.position;

            Shatter(point);
        }

        private void Shatter(Vector2 point)
        {
            shattered = true;

            // 视觉与音效
            ShockwaveRing.Spawn(point, radius, 0.32f, new Color(1f, 0.85f, 0.55f, 1f));
            ShockwaveRing.Spawn(point, radius * 0.6f, 0.2f, new Color(1f, 0.6f, 0.35f, 0.9f));

            if (CameraSystem.CameraFollow.Instance != null)
                CameraSystem.CameraFollow.Instance.Shake(0.75f);

            Game.Audio.SoundPlayer.Play("slam", 1f);

            // 范围伤害
            Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius);
            var damaged = new HashSet<Health>();

            for (int i = 0; i < hits.Length; i++)
            {
                Enemy.EnemyAI target = hits[i].GetComponentInParent<Enemy.EnemyAI>();
                if (target == null) continue;

                Health health = target.GetComponent<Health>();
                if (health == null || health.IsDead || !damaged.Add(health)) continue;

                var info = new DamageInfo(damage, Vector2.zero, gameObject);
                health.TakeDamage(in info);

                Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
                if (targetRb != null && !targetRb.isKinematic)
                {
                    Vector2 dir = ((Vector2)target.transform.position - point).normalized;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
                    targetRb.velocity = dir * knockback + Vector2.up * knockbackLift;
                    targetRb.WakeUp();
                }

                if (ComboCounter.Instance != null) ComboCounter.Instance.AddHit();
            }

            // 一次性：炸完就消失
            Destroy(gameObject, 0.02f);
        }
    }
}
