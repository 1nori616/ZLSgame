using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Level
{
    /// <summary>
    /// 地砖碰撞体修复：
    /// 脚本生成的 TilemapCollider2D + CompositeCollider2D 几何体不会自动烘焙，
    /// 表现为"地砖看着在、人掉下去"或"走两步卡在接缝上"。
    /// 这个组件在运行时补一次 GenerateGeometry()。
    /// </summary>
    [RequireComponent(typeof(CompositeCollider2D))]
    public class TilemapColliderBaker : MonoBehaviour
    {
        [Tooltip("启动后延迟几帧再烘焙，等 Tilemap 数据就绪")]
        [SerializeField] private int delayFrames = 1;

        private CompositeCollider2D composite;
        private TilemapCollider2D tilemapCollider;

        private void Awake()
        {
            composite = GetComponent<CompositeCollider2D>();
            tilemapCollider = GetComponent<TilemapCollider2D>();
        }

        private System.Collections.IEnumerator Start()
        {
            for (int i = 0; i < Mathf.Max(1, delayFrames); i++)
                yield return null;

            Rebuild();
        }

        /// <summary>立即重新烘焙碰撞体形状。</summary>
        public void Rebuild()
        {
            if (tilemapCollider != null)
            {
                tilemapCollider.enabled = false;
                tilemapCollider.enabled = true;
            }

            if (composite != null)
            {
                composite.enabled = false;
                composite.enabled = true;
                composite.GenerateGeometry();
            }
        }
    }
}
