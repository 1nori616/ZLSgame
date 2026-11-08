using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 掉出地图的判定：玩家/敌人掉出边界直接死亡（不再传送回出生点）。
    /// 没有 Health 的对象（比如箱子）直接销毁。
    /// </summary>
    public class OutOfBounds : MonoBehaviour
    {
        [Tooltip("低于这个 Y 就判定掉出地图")]
        [SerializeField] private float minY = -8f;

        [Tooltip("高于这个 Y 也判定飞出地图")]
        [SerializeField] private float maxY = 12f;

        private Health health;
        private bool handled;

        /// <summary>给关卡生成器用：按关卡尺寸设置上下边界。</summary>
        public void Configure(float newMinY, float newMaxY)
        {
            minY = newMinY;
            maxY = newMaxY;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            if (health == null) health = GetComponentInParent<Health>();
        }

        private void FixedUpdate()
        {
            if (handled) return;
            if (transform.position.y > minY && transform.position.y < maxY) return;

            handled = true;

            if (health != null && !health.IsDead)
            {
                // 直接判定死亡：敌人播死亡效果，玩家触发死亡界面
                var info = new DamageInfo(9999, Vector2.zero, gameObject);
                health.TakeDamage(in info);
                return;
            }

            // 没有生命值的东西（箱子等）直接销毁
            Destroy(gameObject);
        }
    }
}
