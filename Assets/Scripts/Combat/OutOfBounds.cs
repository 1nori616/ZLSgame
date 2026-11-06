using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 掉出地图时自动送回出生点，防止角色/敌人穿透地板后永久卡死。
    /// 挂在玩家和敌人身上（可选，但演示版强烈建议）。
    /// </summary>
    public class OutOfBounds : MonoBehaviour
    {
        [Tooltip("低于这个 Y 就判定掉出地图")]
        [SerializeField] private float minY = -8f;

        [Tooltip("高于这个 Y 也判定飞出地图")]
        [SerializeField] private float maxY = 12f;

        private Rigidbody2D rb;
        private Vector3 spawnPosition;

        /// <summary>给关卡生成器用：按关卡尺寸设置上下边界。</summary>
        public void Configure(float newMinY, float newMaxY)
        {
            minY = newMinY;
            maxY = newMaxY;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spawnPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (transform.position.y > minY && transform.position.y < maxY) return;

            transform.position = spawnPosition;
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }
}
