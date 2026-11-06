using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 关卡里的浮动提示文字：玩家走近才显示，走远淡出。
    /// 由关卡生成器根据 *.hints.txt 自动创建，不需要手动摆放。
    /// </summary>
    public class LevelHint : MonoBehaviour
    {
        [TextArea(1, 3)]
        [SerializeField] private string text = "提示";

        [Tooltip("玩家进入这个距离内才显示（世界单位），0 = 一直显示")]
        [SerializeField] private float showRadius = 9f;

        [Tooltip("再往外多远完全淡出")]
        [SerializeField] private float fadeRadius = 4f;

        [SerializeField] private int fontSize = 15;

        [Tooltip("整体往上偏移，避免压住角色")]
        [SerializeField] private float yOffset = 0.6f;

        private Transform player;
        private float alpha;
        private GUIStyle style;

        private void Update()
        {
            if (player == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }

            float target = 0f;

            if (player != null)
            {
                float dist = Vector2.Distance(player.position, transform.position);
                if (showRadius <= 0f || dist <= showRadius)
                {
                    float t = fadeRadius <= 0f ? 1f : Mathf.Clamp01((showRadius - dist) / fadeRadius);
                    target = Mathf.Clamp01(t);
                }
            }
            else
            {
                target = 1f;
            }

            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime * 4f);
        }

        private void OnGUI()
        {
            if (alpha <= 0.01f || string.IsNullOrEmpty(text)) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * yOffset);
            if (screen.z < 0f) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    richText = false
                };
            }

            style.normal.textColor = new Color(1f, 0.95f, 0.75f, alpha);

            float w = 760f;
            int lineCount = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lineCount++;
            float h = fontSize * 1.5f * lineCount;

            Rect r = new Rect(screen.x - w * 0.5f, Screen.height - screen.y - h, w, h);

            // 阴影，保证在亮背景上也看得清
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.75f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, shadow);

            GUI.Label(r, text, style);
        }

        public void Setup(string t, float radius)
        {
            text = t;
            showRadius = radius;
        }
    }
}
