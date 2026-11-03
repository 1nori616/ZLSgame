using System.Collections;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 受击闪白：订阅同物体的 Health.OnDamaged，把 SpriteRenderer 短暂染成高亮色。
    /// 敌人、木桩、玩家都能挂。
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Health health;

        [Tooltip("留空会自动收集子物体上所有 SpriteRenderer")]
        [SerializeField] private SpriteRenderer[] targets;

        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.07f;

        private Color[] originalColors;
        private Coroutine routine;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (targets == null || targets.Length == 0)
                targets = GetComponentsInChildren<SpriteRenderer>();

            if (targets != null)
            {
                originalColors = new Color[targets.Length];
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] != null) originalColors[i] = targets[i].color;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamaged -= HandleDamaged;
        }

        private void HandleDamaged(DamageInfo info)
        {
            if (targets == null || targets.Length == 0) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].color = flashColor;

            yield return new WaitForSeconds(flashDuration);

            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].color = originalColors[i];

            routine = null;
        }
    }
}
