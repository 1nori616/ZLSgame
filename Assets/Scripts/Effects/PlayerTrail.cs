using System.Collections.Generic;
using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 冲刺/高速移动时在身后留下白色颗粒尾迹。
    /// 自动挂在玩家身上，不需要手动配置；用对象池，不产生 GC。
    /// </summary>
    public class PlayerTrail : MonoBehaviour
    {
        [Tooltip("冲刺时每秒喷多少颗粒")]
        [SerializeField] private float dashEmitRate = 60f;

        [Tooltip("加速瞬间每秒喷多少颗粒")]
        [SerializeField] private float runEmitRate = 18f;

        [Tooltip("横向加速度超过多少算\"加速\"")]
        [SerializeField] private float runSpeedThreshold = 30f;

        [Tooltip("颗粒存活时间")]
        [SerializeField] private float particleLife = 0.35f;

        [Tooltip("颗粒初始大小")]
        [SerializeField] private float particleSize = 0.22f;

        private Player.PlayerMotor2D motor;
        private Transform container;
        private readonly List<Particle> particles = new List<Particle>();
        private float emitTimer;
        private float lastVx;
        private Rigidbody2D rb;

        private static Sprite whiteSprite;

        private class Particle
        {
            public SpriteRenderer renderer;
            public float life;
            public float maxLife;
            public Vector2 velocity;
            public Vector3 baseScale;
            public bool active;
        }

        private void Awake()
        {
            motor = GetComponent<Player.PlayerMotor2D>();
            rb = GetComponent<Rigidbody2D>();

            var root = new GameObject("PlayerTrail");
            container = root.transform;

            for (int i = 0; i < 32; i++)
            {
                var go = new GameObject($"trail_{i:00}");
                go.transform.SetParent(container, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetWhiteSprite();
                sr.sortingOrder = -1;                 // 画在玩家后面
                sr.color = Color.clear;

                particles.Add(new Particle { renderer = sr, active = false });
            }
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;

            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();

            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return whiteSprite;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            Emit(dt);
            UpdateParticles(dt);
        }

        private void Emit(float dt)
        {
            if (motor == null || container == null) return;

            bool dashing = motor.IsDashing;

            float vy = rb != null ? rb.velocity.y : 0f;
            bool vertical = Mathf.Abs(vy) > 1.2f;          // 上升/下降

            float ax = 0f;
            if (rb != null && dt > 0.0001f)
                ax = Mathf.Abs(rb.velocity.x - lastVx) / dt;   // 横向加速度
            lastVx = rb != null ? rb.velocity.x : 0f;

            bool accelerating = ax >= runSpeedThreshold;

            float rate = 0f;
            if (dashing) rate = dashEmitRate;
            else if (vertical) rate = dashEmitRate * 0.55f;    // 飞起来也拉一条尾迹
            else if (accelerating) rate = runEmitRate;

            if (rate <= 0f)
            {
                emitTimer = 0f;
                return;
            }

            emitTimer += dt * rate;
            while (emitTimer >= 1f)
            {
                emitTimer -= 1f;
                SpawnParticle(dashing);
            }
        }

        private void SpawnParticle(bool dashing)
        {
            Particle p = null;
            for (int i = 0; i < particles.Count; i++)
            {
                if (!particles[i].active) { p = particles[i]; break; }
            }

            if (p == null) return;

            float facing = motor != null ? motor.Facing : 1f;

            p.active = true;
            p.life = particleLife * (dashing ? 1f : 0.7f);
            p.maxLife = p.life;

            Vector3 pos = transform.position + new Vector3(
                -facing * Random.Range(0.15f, 0.35f),
                Random.Range(-0.25f, 0.25f),
                0f);

            p.renderer.transform.position = pos;
            p.renderer.color = new Color(1f, 1f, 1f, dashing ? 0.85f : 0.5f);

            float s = particleSize * Random.Range(0.6f, 1.2f);
            p.baseScale = new Vector3(s, s, 1f);
            p.renderer.transform.localScale = p.baseScale;

            p.velocity = new Vector2(-facing * Random.Range(1.5f, 3.5f), Random.Range(-0.3f, 1.2f));
        }

        private void UpdateParticles(float dt)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                if (!p.active) continue;

                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.active = false;
                    p.renderer.color = Color.clear;
                    continue;
                }

                float k = p.life / p.maxLife;

                p.renderer.transform.position += (Vector3)(p.velocity * dt);
                p.velocity *= 0.92f;

                Color c = p.renderer.color;
                c.a = Mathf.Lerp(0f, 1f, k) * 0.8f;
                p.renderer.color = c;

                p.renderer.transform.localScale = p.baseScale * Mathf.Lerp(0.4f, 1f, k);
            }
        }
    }
}
