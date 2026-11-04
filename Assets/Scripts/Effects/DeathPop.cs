using System.Collections;
using Game.CameraSystem;
using Game.Combat;
using Game.Gravity;
using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 击杀表现：敌人死亡时先"涨一下"再缩没，同时闪白、掉落冲击波、震屏。
    /// 挂在敌人身上（需要同物体有 Health）。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DeathPop : MonoBehaviour
    {
        [SerializeField] private Health health;

        [Tooltip("整个消失过程的时长")]
        [SerializeField] private float duration = 0.28f;

        [Tooltip("先膨胀到多少倍")]
        [SerializeField] private float bulge = 1.4f;

        [SerializeField] private Color flashColor = Color.white;

        [Header("反馈")]
        [SerializeField] private float shake = 0.55f;
        [SerializeField] private float ringRadius = 1.8f;

        private SpriteRenderer[] renderers;
        private Color[] originalColors;
        private Vector3 originalScale;
        private bool dying;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) originalColors[i] = renderers[i].color;

            originalScale = transform.localScale;

            if (health != null) health.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDied -= HandleDied;
        }

        private void HandleDied()
        {
            if (dying) return;
            dying = true;

            // 停掉它的行动，避免尸体还在追人
            Enemy.EnemyAI ai = GetComponent<Enemy.EnemyAI>();
            if (ai != null) ai.enabled = false;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = false;

            GravityAffected gravity = GetComponent<GravityAffected>();
            if (gravity != null) gravity.enabled = false;

            StartCoroutine(PopRoutine());
        }

        private IEnumerator PopRoutine()
        {
            Sfx.PlayDeath();

            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(shake);

            ShockwaveRing.Spawn(transform.position, ringRadius, 0.3f, new Color(1f, 0.9f, 0.6f, 0.9f));

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                // 0~0.3 鼓起来，0.3~1 缩到 0
                float scale = k < 0.3f
                    ? Mathf.Lerp(1f, bulge, k / 0.3f)
                    : Mathf.Lerp(bulge, 0f, (k - 0.3f) / 0.7f);

                transform.localScale = originalScale * scale;

                float alpha = 1f - Mathf.Pow(k, 3f);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = Color.Lerp(flashColor, originalColors[i], k);
                    c.a = alpha;
                    renderers[i].color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
