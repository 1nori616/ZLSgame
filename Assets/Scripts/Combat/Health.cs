using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 通用血量组件：只管数字和事件，不管击退、动画、无敌。
    /// 玩家、敌人、木桩都能挂。无敌/受击表现由各自的脚本负责。
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 3;
        [Tooltip("死亡时自动销毁物体（木桩、敌人勾上；玩家不要勾）")]
        [SerializeField] private bool destroyOnDeath;

        [Header("调试")]
        [Tooltip("在物体头顶显示血量文字，测试用，正式版关掉")]
        [SerializeField] private bool showDebugHud;

        private Camera cam;

        public int Max => maxHealth;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool IsInvincible => false;

        /// <summary>(当前血量, 最大血量)</summary>
        public event Action<int, int> OnChanged;
        public event Action<DamageInfo> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            Current = maxHealth;
            if (showDebugHud) cam = Camera.main;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead) return;

            int amount = Mathf.Max(0, info.Amount);
            if (amount == 0) return;

            Current = Mathf.Max(0, Current - amount);
            OnChanged?.Invoke(Current, maxHealth);
            OnDamaged?.Invoke(info);

            if (Current <= 0)
            {
                OnDied?.Invoke();
                if (destroyOnDeath) Destroy(gameObject);
            }
        }

        public void ResetHealth()
        {
            Current = maxHealth;
            OnChanged?.Invoke(Current, maxHealth);
        }

        private void OnGUI()
        {
            if (!showDebugHud || cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.8f);
            if (screen.z < 0f) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(screen.x - 60f, Screen.height - screen.y - 10f, 120f, 22f),
                $"{name} {Current}/{Max}", style);
        }
    }
}
