using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 屏幕暗角：进入"锁定慢动作"时四周变暗，把注意力收到中间。
    /// 纯代码生成，不需要美术资源。挂在 Main Camera 上。
    /// </summary>
    public class Vignette : MonoBehaviour
    {
        public static Vignette Instance { get; private set; }

        [Tooltip("最大暗度（0~1），0.75 比较明显")]
        [Range(0f, 1f)]
        [SerializeField] private float maxAlpha = 0.72f;

        [Tooltip("渐变速度")]
        [SerializeField] private float fadeSpeed = 6f;

        [Tooltip("暗角颜色")]
        [SerializeField] private Color color = Color.black;

        [Tooltip("从画面中心多远开始变暗（0 = 中心就暗，1 = 只在四角）")]
        [Range(0f, 1f)]
        [SerializeField] private float startRatio = 0.45f;

        private Texture2D texture;
        private float alpha;
        private float targetAlpha;

        private void Awake()
        {
            Instance = this;
            texture = BuildVignetteTexture(128);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (texture != null) Destroy(texture);
        }

        /// <summary>开关暗角。</summary>
        public void SetActive(bool on)
        {
            targetAlpha = on ? maxAlpha : 0f;
        }

        public void SetActive(bool on, float customAlpha)
        {
            maxAlpha = Mathf.Clamp01(customAlpha);
            targetAlpha = on ? maxAlpha : 0f;
        }

        /// <summary>带颜色的暗角：失重时用淡蓝，瞄准锁定时用黑。</summary>
        public void SetActive(bool on, float customAlpha, Color tint)
        {
            color = tint;
            maxAlpha = Mathf.Clamp01(customAlpha);
            targetAlpha = on ? maxAlpha : 0f;
        }

        private void Update()
        {
            alpha = Mathf.MoveTowards(alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (alpha <= 0.002f || texture == null) return;

            Color prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), texture, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private Texture2D BuildVignetteTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;   // 0 中心 → 1 角落

                    float a = Mathf.InverseLerp(startRatio, 1f, d);
                    a = Mathf.SmoothStep(0f, 1f, a);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
