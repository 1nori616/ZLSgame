using Game.CameraSystem;
using Game.Effects;
using Game.Enemy;
using UnityEngine;

namespace Game.Gravity
{
    /// <summary>
    /// "站在地上被加重"的专属反馈：强震屏 + 地面冲击波 + 压扁 + 短暂僵直。
    /// 挂在敌人/箱子上（需要同物体有 GravityAffected）。
    /// </summary>
    [RequireComponent(typeof(GravityAffected))]
    public class GroundSlamEffect : MonoBehaviour
    {
        [SerializeField] private GravityAffected gravity;

        [Header("冲击")]
        [Tooltip("震屏强度（0~1）")]
        [SerializeField] private float shakeStrength = 0.75f;

        [SerializeField] private float ringRadius = 2.4f;
        [SerializeField] private float ringDuration = 0.35f;
        [SerializeField] private Color ringColor = new Color(1f, 0.95f, 0.8f, 0.85f);

        [Header("压制感")]
        [Tooltip("被压住时的僵直时间（比普通受击更长，体现被压制）")]
        [SerializeField] private float stunDuration = 0.35f;

        [Tooltip("压扁幅度：0.28 = 高度压掉 28%")]
        [Range(0f, 0.6f)]
        [SerializeField] private float squashAmount = 0.28f;

        [SerializeField] private float squashDuration = 0.12f;

        [Tooltip("留空会自动找子物体上的 SpriteRenderer")]
        [SerializeField] private Transform squashTarget;

        private Vector3 baseScale;
        private bool hasTarget;
        private Coroutine squashRoutine;

        private void Awake()
        {
            if (gravity == null) gravity = GetComponent<GravityAffected>();

            if (squashTarget == null)
            {
                SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].transform != transform)
                    {
                        squashTarget = renderers[i].transform;
                        break;
                    }
                }
            }

            // 没有子物体 Sprite 时，退化为压扁自身（Sprite 在根节点的情况）
            if (squashTarget == null) squashTarget = transform;

            baseScale = squashTarget.localScale;
            hasTarget = true;
        }

        private void OnEnable()
        {
            if (gravity != null) gravity.OnGroundSlam += HandleGroundSlam;
        }

        private void OnDisable()
        {
            if (gravity != null) gravity.OnGroundSlam -= HandleGroundSlam;
        }

        private void HandleGroundSlam(GravityAffected target)
        {
            CameraFollow cam = CameraFollow.Instance;
            if (cam != null) cam.Shake(shakeStrength);

            ShockwaveRing.Spawn(
                transform.position + Vector3.down * 0.45f,
                ringRadius,
                ringDuration,
                ringColor);

            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.ApplyStun(stunDuration);

            if (hasTarget) StartSquash();
        }

        private void StartSquash()
        {
            if (squashRoutine != null) StopCoroutine(squashRoutine);
            squashRoutine = StartCoroutine(SquashRoutine());
        }

        private System.Collections.IEnumerator SquashRoutine()
        {
            // 压扁：变矮变宽
            squashTarget.localScale = new Vector3(
                baseScale.x * (1f + squashAmount),
                baseScale.y * (1f - squashAmount),
                baseScale.z);

            float t = 0f;
            while (t < squashDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / squashDuration);
                squashTarget.localScale = Vector3.Lerp(
                    new Vector3(baseScale.x * (1f + squashAmount), baseScale.y * (1f - squashAmount), baseScale.z),
                    baseScale,
                    k);
                yield return null;
            }

            squashTarget.localScale = baseScale;
            squashRoutine = null;
        }
    }
}
