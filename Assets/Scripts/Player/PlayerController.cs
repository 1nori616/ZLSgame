using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 把 Input 和 Motor 串起来：土狼时间、跳跃缓冲、跳跃截断、冲刺、动画参数。
    /// 所有"什么时候该跳"的决策在这里，改手感优先改这里。
    /// </summary>
    [RequireComponent(typeof(PlayerInput), typeof(PlayerMotor2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerConfig config;
        [SerializeField] private Animator animator;

        [Tooltip("留空会自动找子物体上的 SpriteRenderer")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("素材朝向和代码相反时勾上")]
        [SerializeField] private bool invertFacing;

        private float coyoteTimer;
        private float bufferTimer;
        private bool jumpHeldLastFrame;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int DashHash = Animator.StringToHash("Dash");

        /// <summary>当前播放的动画状态名，避免每帧重复 Play 导致动画卡在第一帧。</summary>
        private string currentState;
        private GravityShift gravityShift;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (config == null && motor != null) config = motor.Config;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            gravityShift = GetComponent<GravityShift>();
        }

        private void Update()
        {
            if (config == null || input == null || motor == null) return;

            PlayerInputData data = input.Read();

            // 跳跃缓冲：落地前按下也算数
            if (data.JumpDown) bufferTimer = config.jumpBufferTime;
            else bufferTimer -= Time.deltaTime;

            // 土狼时间：刚离开地面的一小段时间内仍可起跳
            if (motor.IsGrounded) coyoteTimer = config.coyoteTime;
            else coyoteTimer -= Time.deltaTime;

            motor.SetHorizontal(data.MoveX);
            motor.SetVertical(data.MoveY);

            if (bufferTimer > 0f && coyoteTimer > 0f)
            {
                motor.Jump(config.jumpForce);
                bufferTimer = 0f;
                coyoteTimer = 0f;
            }

            // 松手截断
            if (jumpHeldLastFrame && !data.JumpHeld) motor.CutJump();
            jumpHeldLastFrame = data.JumpHeld;

            if (data.DashDown) motor.Dash(data.MoveX);

            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            // 按朝向翻转贴图（invertFacing 用于素材方向相反的情况）
            if (spriteRenderer != null)
                spriteRenderer.flipX = (motor.Facing < 0f) ^ invertFacing;

            if (animator == null) return;

            // 锁定目标（准备施放）时切到"抬手"姿势
            if (gravityShift != null && gravityShift.HasManualLock)
            {
                PlayState("Player_Cast");
                return;
            }

            bool moving = Mathf.Abs(motor.HorizontalSpeed) > 0.2f;
            bool grounded = motor.IsGrounded;

            if (!grounded)
                PlayState(motor.VerticalVelocity > 0f ? "Player_Jump" : "Player_Fall");
            else
                PlayState(moving ? "Player_Run" : "Player_Idle");
        }

        /// <summary>只在状态变化时调用 Play，否则每帧调用会把动画重置到第 0 帧。</summary>
        private void PlayState(string stateName)
        {
            if (currentState == stateName) return;

            // 没有对应状态就退回待机，避免 Animation.Play 报错
            if (!animator.HasState(0, Animator.StringToHash(stateName)))
                stateName = "Player_Idle";

            if (currentState == stateName) return;

            currentState = stateName;
            animator.Play(stateName, 0, 0f);
        }
    }
}
