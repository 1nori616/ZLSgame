using System.Collections;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// 开场序列：镜头固定，主角自动从起点舱走出来，
    /// 走到画面中央后才交还控制权、镜头开始跟随。
    /// 由关卡生成器自动创建。
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [Tooltip("主角自动行走速度")]
        [SerializeField] private float walkSpeed = 2f;

        [Tooltip("镜头固定位置（相对出生点）")]
        [SerializeField] private Vector2 cameraOffset = new Vector2(7f, 2.4f);

        [Tooltip("走到目标位置后停留多久再交还控制")]
        [SerializeField] private float holdTime = 0.35f;

        private Transform player;
        private Player.PlayerController controller;
        private Player.PlayerMotor2D motor;
        private Rigidbody2D rb;
        private Animator animator;
        private CameraSystem.CameraFollow follow;
        private Camera cam;

        [SerializeField] private float targetX = -1f;
        private bool finished;

        public void Setup(float startX, float walkSpeedOverride = 0f)
        {
            if (walkSpeedOverride > 0f) walkSpeed = walkSpeedOverride;
            targetX = startX;
        }

        private System.Collections.IEnumerator Start()
        {
            // 封面还开着就先等着，别在背景里把开场播完
            while (Game.Level.TitleScreen.IsShowing)
                yield return null;

            // 保险：上一局可能留下 timeScale=0（死亡/顿帧），这里强制恢复正常
            if (Time.timeScale < 0.99f)
            {
                Debug.Log($"[Intro] 检测到 timeScale={Time.timeScale}，重置为 1");
                Time.timeScale = 1f;
            }

            // Tag 可能挂在子物体上（hurtbox），必须沿父级找到真正的玩家根
            Player.PlayerController found = null;

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) found = tagged.GetComponentInParent<Player.PlayerController>();
            if (found == null) found = Object.FindObjectOfType<Player.PlayerController>();

            if (found == null)
            {
                Debug.LogWarning("[IntroSequence] 找不到玩家，跳过开场");
                yield break;
            }

            GameObject go = found.gameObject;

            player = go.transform;
            controller = go.GetComponent<Player.PlayerController>();
            motor = go.GetComponent<Player.PlayerMotor2D>();
            rb = go.GetComponent<Rigidbody2D>();
            animator = go.GetComponentInChildren<Animator>();

            cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Intro] 没有 MainCamera，跳过");
                yield break;
            }

            // 兜底：如果没配置目标，就走到相机中心右边一点
            if (targetX <= 0f) targetX = player.position.x + 6f;

            Debug.Log($"[Intro] 开始：玩家x={player.position.x:0.00} 目标x={targetX:0.00} 速度={walkSpeed}");

            follow = cam.GetComponent<CameraSystem.CameraFollow>();

            // 镜头先固定住
            if (follow != null) follow.enabled = false;
            cam.transform.position = new Vector3(player.position.x + cameraOffset.x,
                                                 player.position.y + cameraOffset.y,
                                                 -10f);

            // 主角暂时不能操作：控制器和电机都关掉
            if (controller != null) controller.enabled = false;
            if (motor != null) motor.enabled = false;

            // 开场期间关掉重力影响，保证匀速走出来
            if (rb != null) rb.gravityScale = 1f;

            StartCoroutine(WalkOut());
        }

        private IEnumerator WalkOut()
        {
            if (animator != null) animator.Play("Player_Run", 0, 0f);

            float elapsed = 0f;
            const float minDuration = 1.2f;
            float logTimer = 0f;

            while (player != null && (player.position.x < targetX || elapsed < minDuration))
            {
                if (rb != null) rb.velocity = new Vector2(walkSpeed, rb.velocity.y);
                elapsed += Time.deltaTime;

                logTimer += Time.unscaledDeltaTime;
                if (logTimer > 0.3f)
                {
                    logTimer = 0f;
                    Debug.Log($"[Intro] t={elapsed:0.00} x={player.position.x:0.00} vx={rb.velocity.x:0.00} vy={rb.velocity.y:0.00} ts={Time.timeScale} bodyType={rb.bodyType} motor={(motor != null && motor.enabled)} ctrl={(controller != null && controller.enabled)}");
                }

                yield return null;
            }

            if (rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
            if (animator != null) animator.Play("Player_Idle", 0, 0f);

            if (holdTime > 0f) yield return new WaitForSeconds(holdTime);

            Finish();
        }

        private System.Collections.IEnumerator RestoreSmoothTime()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            if (follow != null) follow.SetSmoothTime(0.12f);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;

            Debug.Log($"[Intro] 结束：x={player.position.x:0.00}");
            if (motor != null) motor.enabled = true;
            if (controller != null) controller.enabled = true;

            if (follow != null && cam != null)
            {
                // 从当前镜头位置平滑过渡到跟随状态，避免瞬间跳切
                follow.WarpTo(cam.transform.position);
                follow.SetSmoothTime(0.7f);
                follow.enabled = true;
                StartCoroutine(RestoreSmoothTime());
            }
        }
    }
}
