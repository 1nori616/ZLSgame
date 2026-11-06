using System.Collections;
using Game.CameraSystem;
using Game.Combat;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 玩家的受击逻辑：无敌帧（冲刺 / 受击后）、击退、闪烁、震屏、死亡。
    /// 血量数字本身存在 Health 组件里。
    /// </summary>
    [RequireComponent(typeof(PlayerMotor2D))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerConfig config;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool showDebugHud = true;

        [Tooltip("受击时镜头震动的基础强度")]
        [SerializeField] private float hitShake = 0.5f;

        private float invincibleTimer;
        private Coroutine flashRoutine;
        private Color baseColor = Color.white;

        public bool IsDead => health == null || health.IsDead;
        public bool IsInvincible => invincibleTimer > 0f || (motor != null && motor.IsDashing);
        public Health Health => health;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (health == null) health = GetComponent<Health>();
            if (config == null && motor != null) config = motor.Config;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
        }

        private void Update()
        {
            if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead || IsInvincible) return;

            if (health != null) health.TakeDamage(info);

            ShakeCamera(info.Amount);
            Game.Audio.SoundPlayer.Play("hit", 0.55f);

            // 击退方向：背离伤害源
            float dir = -1f;
            if (info.Source != null)
                dir = Mathf.Sign(transform.position.x - info.Source.transform.position.x);
            if (Mathf.Approximately(dir, 0f)) dir = -motor.Facing;

            if (motor != null)
            {
                float duration = config != null ? config.knockbackTime : 0.18f;
                motor.ApplyKnockback(new Vector2(dir * Mathf.Abs(info.Knockback.x), info.Knockback.y), duration);
            }

            invincibleTimer = config != null ? config.hurtInvincibleTime : 0.8f;
            StartFlash();
        }

        private void ShakeCamera(int damage)
        {
            CameraFollow cam = CameraFollow.Instance;
            if (cam == null) return;
            cam.Shake(Mathf.Clamp01(hitShake + 0.08f * Mathf.Abs(damage)));
        }

        private void StartFlash()
        {
            if (spriteRenderer == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float elapsed = 0f;
            while (elapsed < invincibleTimer)
            {
                spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.25f);
                yield return new WaitForSeconds(0.08f);
                spriteRenderer.color = baseColor;
                yield return new WaitForSeconds(0.08f);
                elapsed += 0.16f;
            }

            spriteRenderer.color = baseColor;
            flashRoutine = null;
        }

        private void OnGUI()
        {
            if (!showDebugHud || health == null) return;

            GUILayout.BeginArea(new Rect(12f, 12f, 240f, 50f));
            GUILayout.Label($"HP: {health.Current} / {health.Max}" + (IsInvincible ? "  [无敌]" : ""));
            GUILayout.EndArea();
        }
    }
}
