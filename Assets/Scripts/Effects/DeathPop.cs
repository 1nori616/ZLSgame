using System.Collections;
using Game.CameraSystem;
using Game.Combat;
using Game.Gravity;
using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 击杀表现：
    ///   ① 瞬间进入慢动作（世界几乎停住）
    ///   ② 短暂完全定格
    ///   ③ 敌人膨胀 → 炸开（冲击波 + 震屏）
    /// 挂在敌人身上（需要同物体有 Health）。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DeathPop : MonoBehaviour
    {
        [SerializeField] private Health health;

        [Header("慢动作")]
        [Tooltip("击杀瞬间把时间放慢到多少倍")]
        [SerializeField] private float killSlowScale = 0.12f;

        [Tooltip("慢动作持续多久（真实时间）")]
        [SerializeField] private float killSlowDuration = 0.16f;

        [Tooltip("紧接着的完全定格时长（真实时间）")]
        [SerializeField] private float killFreezeDuration = 0.06f;

        [Header("炸开")]
        [Tooltip("膨胀到多少倍后炸开")]
        [SerializeField] private float bulge = 1.5f;

        [Tooltip("从膨胀到消失的时长（真实时间）")]
        [SerializeField] private float duration = 0.3f;

        [SerializeField] private Color flashColor = Color.white;

        [Header("反馈")]
        [SerializeField] private float shake = 0.7f;
        [SerializeField] private float ringRadius = 2.2f;

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
            // 记住原来的时间流速（可能正处于瞄准慢动作里）
            float restoreScale = Time.timeScale > 0.05f ? Time.timeScale : 1f;

            // ① 慢动作
            Time.timeScale = killSlowScale;
            float t = 0f;
            while (t < killSlowDuration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // ② 完全定格
            Time.timeScale = 0f;
            t = 0f;
            while (t < killFreezeDuration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // ③ 炸开：时间稍微恢复，让冲击波看得见
            Time.timeScale = Mathf.Min(restoreScale, 0.6f);

            Game.Audio.SoundPlayer.Play("death", 1f);

            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(shake);

            ShockwaveRing.Spawn(transform.position, ringRadius, 0.28f, new Color(1f, 0.95f, 0.7f, 1f));
            ShockwaveRing.Spawn(transform.position, ringRadius * 0.55f, 0.18f, new Color(1f, 0.6f, 0.4f, 0.9f));

            t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                // 0~0.25 迅速鼓起来，之后炸开缩到 0
                float scale = k < 0.25f
                    ? Mathf.Lerp(1f, bulge, k / 0.25f)
                    : Mathf.Lerp(bulge, 0f, (k - 0.25f) / 0.75f);

                transform.localScale = originalScale * scale;

                float alpha = 1f - Mathf.Pow(k, 2.2f);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = Color.Lerp(flashColor, originalColors[i], k);
                    c.a = alpha;
                    renderers[i].color = c;
                }

                yield return null;
            }

            Time.timeScale = restoreScale;
            Destroy(gameObject);
        }
    }
}
