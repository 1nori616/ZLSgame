using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 敌人头顶的小血条：只在被打过之后短暂显示，避免画面太乱。
    /// 挂敌人身上（同物体有 Health）。
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("尺寸（屏幕像素）")]
        [SerializeField] private float width = 30f;
        [SerializeField] private float height = 4f;

        [Header("位置")]
        [Tooltip("在敌人头顶多高（世界单位，会乘上物体缩放）")]
        [SerializeField] private float heightOffset = 1.05f;

        [Header("显示")]
        [SerializeField] private bool alwaysShow;
        [Tooltip("受伤后显示多久")]
        [SerializeField] private float showDuration = 2.5f;

        [Header("配色")]
        [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Color fillColor = new Color(0.9f, 0.25f, 0.25f);

        private Combat.Health health;
        private float timer;

        private void Awake()
        {
            health = GetComponent<Combat.Health>();
            if (health != null) health.OnDamaged += HandleDamaged;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDamaged -= HandleDamaged;
        }

        private void HandleDamaged(Combat.DamageInfo info)
        {
            timer = showDuration;
        }

        private void Update()
        {
            if (timer > 0f) timer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (health == null) return;
            if (health.IsDead) return;
            if (health.Current >= health.Max) return;
            if (!alwaysShow && timer <= 0f) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 world = transform.position + Vector3.up * (heightOffset * transform.localScale.y);
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return;

            float x = screen.x - width * 0.5f;
            float y = Screen.height - screen.y;

            float ratio = Mathf.Clamp01(health.Current / (float)Mathf.Max(1, health.Max));

            DrawRect(new Rect(x, y, width, height), backColor);
            DrawRect(new Rect(x, y, width * ratio, height), fillColor);
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
