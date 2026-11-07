using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 连击计数：靠重力造成伤害就 +1，显示在屏幕右上方。
    /// 命中时数字弹一下、变亮、带黑描边；连得越多颜色越热。
    /// </summary>
    public class ComboCounter : MonoBehaviour
    {
        public static ComboCounter Instance { get; private set; }

        [Tooltip("多久没有新命中就断连")]
        [SerializeField] private float window = 3f;

        [Tooltip("命中瞬间的放大倍数")]
        [SerializeField] private float popScale = 1.6f;

        [Tooltip("低于这个连击数不显示")]
        [SerializeField] private int minShow = 1;

        [Tooltip("基础字号")]
        [SerializeField] private int baseSize = 64;

        private float timer;
        private float pop;
        private float appear;

        public int Count { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>静态入口：场景里没有就自动创建，保证一定能显示。</summary>
        public static void Hit(int amount = 1)
        {
            if (Instance == null)
            {
                var go = new GameObject("ComboCounter");
                go.AddComponent<ComboCounter>();
            }

            if (Instance != null) Instance.AddHit(amount);
        }

        public void AddHit(int amount = 1)
        {
            Count += amount;
            timer = window;
            pop = 1f;
        }

        public void ResetCombo()
        {
            Count = 0;
            timer = 0f;
            pop = 0f;
            appear = 0f;
        }

        private void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f) Count = 0;
            }

            pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 5f);

            float target = Count >= minShow ? 1f : 0f;
            appear = Mathf.MoveTowards(appear, target, Time.unscaledDeltaTime * 8f);
        }

        private void OnGUI()
        {
            if (appear <= 0.01f) return;

            float scale = Mathf.Lerp(1f, popScale, pop) * Mathf.Lerp(0.7f, 1f, appear);
            int size = Mathf.RoundToInt(baseSize * scale);

            // 连得越多颜色越热：黄 → 橙 → 红
            Color hot = Count >= 10
                ? new Color(1f, 0.35f, 0.25f)
                : Count >= 6
                    ? new Color(1f, 0.55f, 0.2f)
                    : new Color(1f, 0.85f, 0.3f);
            hot.a = appear;

            float w = 420f;
            float x = Screen.width - w - 40f;
            float y = 46f;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = hot }
            };

            // 黑色描边，星空背景上也清楚
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, appear * 0.85f);

            string text = $"x{Count}";
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (dx != 0 || dy != 0)
                        GUI.Label(new Rect(x + dx, y + dy, w, size + 20f), text, shadow);

            GUI.Label(new Rect(x, y, w, size + 20f), text, style);

            var tip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 1f, 1f, appear * 0.75f) }
            };
            GUI.Label(new Rect(x, y + size + 12f, w, 22f), "COMBO", tip);

            // 断连倒计时条
            float barW = 120f;
            float ratio = window > 0f ? Mathf.Clamp01(timer / window) : 0f;
            Color barColor = new Color(hot.r, hot.g, hot.b, appear * 0.8f);
            DrawRect(new Rect(Screen.width - barW - 40f, y + size + 38f, barW, 4f), new Color(0f, 0f, 0f, appear * 0.5f));
            DrawRect(new Rect(Screen.width - barW - 40f, y + size + 38f, barW * ratio, 4f), barColor);
        }

        private static void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
