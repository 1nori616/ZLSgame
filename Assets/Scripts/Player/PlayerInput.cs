using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 只负责读输入，不移动任何东西。
    /// 用的是旧版 Input Manager 的默认轴（Horizontal / Jump），不需要额外配置。
    /// </summary>
    public struct PlayerInputData
    {
        public float MoveX;    // -1 ~ 1
        public bool JumpDown;  // 本帧刚按下
        public bool JumpHeld;  // 持续按住
        public bool DashDown;  // 本帧刚按下冲刺
    }

    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;

        public PlayerInputData Read()
        {
            return new PlayerInputData
            {
                MoveX = Input.GetAxisRaw("Horizontal"),           // A/D 或 左右方向键
                JumpDown = Input.GetButtonDown("Jump"),           // 空格
                JumpHeld = Input.GetButton("Jump"),
                DashDown = Input.GetKeyDown(dashKey),
            };
        }
    }
}
