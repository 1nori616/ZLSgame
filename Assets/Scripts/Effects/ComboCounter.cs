using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 连击计数：每次靠重力造成伤害 +1，超过 window 秒没有新命中就归零。
    /// 屏幕右侧显示大数字，命中时有一个"弹一下"的动画。
    /// </summary>
    public class ComboCounter : MonoBehaviour
    {
        public static ComboCounter Instance { get; private set; }

        [Tooltip("多久没有新命中就断连")]
        [SerializeField] private float window = 2.5f;

        [Tooltip("命中瞬间的放大倍数")]
        [SerializeField] private float popScale = 1.45f;

        [Tooltip("低于这个连击数不显示")]
        [SerializeField] private int minShow = 2;

        private float timer;
        private float pop;

        public int Count { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
        }

        private void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f) Count = 0;
            }

            pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 6f);
        }

        private void OnGUI()
        {
            if (Count < minShow) return;

            float scale = Mathf.Lerp(1f, popScale, pop);
            int size = Mathf.RoundToInt(46f * scale);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 0.82f, 0.25f) }
            };

            GUI.Label(new Rect(Screen.width - 320f, 60f, 280f, 90f), $"x{Count}", style);

            var tip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
            };
            GUI.Label(new Rect(Screen.width - 320f, 130f, 280f, 24f), "COMBO", tip);
        }
    }
}
