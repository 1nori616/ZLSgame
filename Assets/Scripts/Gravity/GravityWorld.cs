using System;
using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// 房间级重力方向：+1 = 正常（往下），-1 = 翻转（往上）。
    /// 直接改 Physics2D.gravity，所以玩家、敌人、掉落物会一起翻。
    /// 场景里放一个空物体挂上它即可。
    /// </summary>
    public class GravityWorld : MonoBehaviour
    {
        public static GravityWorld Instance { get; private set; }

        [Tooltip("正常重力大小")]
        [SerializeField] private float normalGravity = 9.81f;

        [Tooltip("翻转默认持续时间（秒）")]
        [SerializeField] private float flipDuration = 10f;

        private float timer;

        public int Sign { get; private set; } = 1;
        public bool IsFlipped => Sign < 0;
        public float RemainingTime => Mathf.Max(0f, timer);

        /// <summary>重力方向变化时触发，参数为 +1 / -1。</summary>
        public event Action<int> OnSignChanged;

        private void Awake()
        {
            Instance = this;
            Apply(1);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (timer <= 0f) return;

            timer -= Time.deltaTime;
            if (timer <= 0f) Apply(1);
        }

        /// <summary>翻转一次（正常↔翻转），duration 秒后自动恢复。</summary>
        public void Toggle(float duration = -1f)
        {
            Apply(Sign > 0 ? -1 : 1);
            timer = duration > 0f ? duration : flipDuration;
        }

        public void ForceSign(int sign, float duration = 0f)
        {
            Apply(sign >= 0 ? 1 : -1);
            timer = duration;
        }

        private void Apply(int sign)
        {
            Sign = sign;
            Physics2D.gravity = new Vector2(0f, -normalGravity * sign);
            OnSignChanged?.Invoke(sign);
        }
    }
}
