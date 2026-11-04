using UnityEngine;

namespace Game.Player
{
    /// <summary>只负责读输入，不移动任何东西。</summary>
    public struct PlayerInputData
    {
        public float MoveX;      // -1 ~ 1
        public float MoveY;      // 失重时的上下推进（-1 下 / +1 上）
        public bool JumpDown;
        public bool JumpHeld;
        public bool DashDown;
        public bool AntiDown;    // Q 反重力
        public bool HeavyDown;   // E 加重
        public bool ZeroGHeld;   // 按住 F 蓄力失重
        public bool ClickDown;   // 鼠标左键：锁定/取消锁定
        public Vector2 Pointer;  // 鼠标屏幕坐标
    }

    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode antiGravityKey = KeyCode.Q;
        [SerializeField] private KeyCode heavyGravityKey = KeyCode.E;
        [SerializeField] private KeyCode zeroGravityKey = KeyCode.F;

        public PlayerInputData Read()
        {
            return new PlayerInputData
            {
                MoveX = Input.GetAxisRaw("Horizontal"),
                MoveY = Input.GetAxisRaw("Vertical"),
                JumpDown = Input.GetButtonDown("Jump"),
                JumpHeld = Input.GetButton("Jump"),
                DashDown = Input.GetKeyDown(dashKey),
                AntiDown = Input.GetKeyDown(antiGravityKey),
                HeavyDown = Input.GetKeyDown(heavyGravityKey),
                ZeroGHeld = Input.GetKey(zeroGravityKey),
                ClickDown = Input.GetMouseButtonDown(0),
                Pointer = Input.mousePosition,
            };
        }
    }
}
