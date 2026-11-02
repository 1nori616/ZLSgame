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
        public bool AntiDown;  // 本帧刚按下：反重力
        public bool HeavyDown; // 本帧刚按下：加重
        public bool SelfDown;  // 本帧刚按下：对自己施放
    }

    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode antiGravityKey = KeyCode.Q;
        [SerializeField] private KeyCode heavyGravityKey = KeyCode.E;
        [SerializeField] private KeyCode selfGravityKey = KeyCode.R;

        public PlayerInputData Read()
        {
            return new PlayerInputData
            {
                MoveX = Input.GetAxisRaw("Horizontal"),           // A/D 或 左右方向键
                JumpDown = Input.GetButtonDown("Jump"),           // 空格
                JumpHeld = Input.GetButton("Jump"),
                DashDown = Input.GetKeyDown(dashKey),
                AntiDown = Input.GetKeyDown(antiGravityKey),
                HeavyDown = Input.GetKeyDown(heavyGravityKey),
                SelfDown = Input.GetKeyDown(selfGravityKey),
            };
        }
    }
}
