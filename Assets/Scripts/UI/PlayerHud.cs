using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 玩家 HUD（极简）：血条 + 重力充能方块。
    /// 全代码绘制，不需要美术资源。
    /// 挂在 player 上（同物体有 PlayerHealth / GravityShift）。
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        [Header("血条")]
        [SerializeField] private Vector2 origin = new Vector2(16f, 16f);
        [SerializeField] private float healthWidth = 180f;
        [SerializeField] private float healthHeight = 14f;
        [SerializeField] private bool showHealthNumber;

        [Header("充能方块")]
        [SerializeField] private float pipSize = 10f;
        [SerializeField] private float pipGap = 5f;
        [SerializeField] private float pipTopGap = 7f;

        [Header("配色")]
        [SerializeField] private Color barBack = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color barFill = new Color(0.82f, 0.24f, 0.24f);
        [SerializeField] private Color barLow = new Color(0.98f, 0.52f, 0.12f);
        [SerializeField] private Color pipReady = new Color(0.95f, 0.85f, 0.35f);
        [SerializeField] private Color pipEmpty = new Color(1f, 1f, 1f, 0.18f);

        private Player.PlayerHealth playerHealth;
        private Player.GravityShift gravityShift;

        private void Awake()
        {
            playerHealth = GetComponent<Player.PlayerHealth>();
            gravityShift = GetComponent<Player.GravityShift>();
        }

        private void OnGUI()
        {
            DrawHealth();
            DrawPips();
        }

        private void DrawHealth()
        {
            if (playerHealth == null || playerHealth.Health == null) return;

            int cur = playerHealth.Health.Current;
            int max = Mathf.Max(1, playerHealth.Health.Max);
            float ratio = Mathf.Clamp01(cur / (float)max);

            Rect back = new Rect(origin.x, origin.y, healthWidth, healthHeight);
            Rect fill = new Rect(origin.x, origin.y, healthWidth * ratio, healthHeight);

            DrawRect(back, barBack);

            // 血少时偏橙
            Color c = ratio > 0.3f ? barFill : Color.Lerp(barLow, barFill, ratio / 0.3f);
            DrawRect(fill, c);

            if (showHealthNumber)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
                };
                GUI.Label(new Rect(origin.x, origin.y - 1f, healthWidth, healthHeight), $"{cur}", style);
            }
        }

        private void DrawPips()
        {
            if (gravityShift == null) return;

            int max = Mathf.Max(1, gravityShift.MaxCharges);
            int have = gravityShift.Charges;

            float y = origin.y + healthHeight + pipTopGap;

            for (int i = 0; i < max; i++)
            {
                var r = new Rect(origin.x + i * (pipSize + pipGap), y, pipSize, pipSize);
                DrawRect(r, i < have ? pipReady : pipEmpty);
            }
        }

        private static void DrawRect(Rect r, Color c)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
