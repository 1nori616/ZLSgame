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

        private float coyoteTimer;
        private float bufferTimer;
        private bool jumpHeldLastFrame;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int DashHash = Animator.StringToHash("Dash");

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (config == null && motor != null) config = motor.Config;
            if (animator == null) animator = GetComponentInChildren<Animator>();
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
            if (animator == null) return;

            animator.SetFloat(SpeedHash, motor.HorizontalSpeed);
            animator.SetBool(GroundedHash, motor.IsGrounded);
            animator.SetFloat(VerticalSpeedHash, motor.VerticalVelocity);
            animator.SetBool(DashHash, motor.IsDashing);
        }
    }
}
