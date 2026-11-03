using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 纯代码生成的冲击波圆环：不需要任何美术资源。
    /// ShockwaveRing.Spawn(位置, 最大半径, 时长, 颜色) 即可。
    /// </summary>
    public class ShockwaveRing : MonoBehaviour
    {
        private static Sprite cachedRing;

        private SpriteRenderer sr;
        private float elapsed;
        private float duration = 0.35f;
        private float maxRadius = 2f;
        private Color baseColor = new Color(1f, 1f, 1f, 0.8f);

        public static ShockwaveRing Spawn(Vector3 position, float radius, float lifetime, Color color)
        {
            GameObject go = new GameObject("ShockwaveRing");
            go.transform.position = position;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetRingSprite();
            renderer.color = color;
            renderer.sortingOrder = 100;

            ShockwaveRing ring = go.AddComponent<ShockwaveRing>();
            ring.sr = renderer;
            ring.duration = Mathf.Max(0.05f, lifetime);
            ring.maxRadius = Mathf.Max(0.1f, radius);
            ring.baseColor = color;

            return ring;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float radius = Mathf.Lerp(0.15f, maxRadius, t);
            transform.localScale = Vector3.one * (radius * 2f);

            Color c = baseColor;
            c.a = baseColor.a * (1f - t);
            if (sr != null) sr.color = c;

            if (t >= 1f) Destroy(gameObject);
        }

        private static Sprite GetRingSprite()
        {
            if (cachedRing != null) return cachedRing;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.18f);
                    a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            cachedRing = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return cachedRing;
        }
    }
}
