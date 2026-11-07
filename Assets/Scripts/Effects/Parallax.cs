using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 视差滚动背景：跟随摄像机移动，但速度更慢，产生纵深。
    /// 由关卡生成器自动创建，不需要手动摆。
    /// </summary>
    public class Parallax : MonoBehaviour
    {
        [Tooltip("横向跟随比例：0 = 完全不动，1 = 和摄像机一样快")]
        [SerializeField] private float factorX = 0.35f;

        [Tooltip("纵向跟随比例")]
        [SerializeField] private float factorY = 0.2f;

        private Transform cam;
        private Vector3 startPos;
        private Vector3 camStart;

        private void Start()
        {
            Camera main = Camera.main;
            if (main == null) return;

            cam = main.transform;
            startPos = transform.position;
            camStart = cam.position;
        }

        private void LateUpdate()
        {
            if (cam == null) return;

            Vector3 delta = cam.position - camStart;
            transform.position = startPos + new Vector3(delta.x * factorX, delta.y * factorY, 0f);
        }

        /// <summary>给生成器用：设置跟随强度。</summary>
        public void Setup(float fx, float fy)
        {
            factorX = fx;
            factorY = fy;
        }
    }
}
