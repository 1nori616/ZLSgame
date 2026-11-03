using UnityEngine;

namespace Game.Player
{
    /// <summary>只负责读输入，不移动任何东西。</summary>
    public struct PlayerInputData
    {
        public float MoveX;      // -1 ~ 1
        public bool JumpDown;
        public bool JumpHeld;
        public bool DashDown;
        public bool AntiDown;    // Q 反重力
        public bool HeavyDown;   // E 加重
        public bool SelfDown;    // R 对自己
        public bool FlipDown;    // F 翻转世界
        public bool LockDown;    // Tab 切换锁定（备用）
        public bool ClickDown;   // 鼠标左键：锁定/取消锁定
        public Vector2 Pointer;  // 鼠标屏幕坐标
    }

    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode antiGravityKey = KeyCode.Q;
        [SerializeField] private KeyCode heavyGravityKey = KeyCode.E;
        [SerializeField] private KeyCode selfGravityKey = KeyCode.R;
        [SerializeField] private KeyCode flipGravityKey = KeyCode.F;
        [SerializeField] private KeyCode lockKey = KeyCode.Tab;

        public PlayerInputData Read()
        {
            return new PlayerInputData
            {
                MoveX = Input.GetAxisRaw("Horizontal"),
                JumpDown = Input.GetButtonDown("Jump"),
                JumpHeld = Input.GetButton("Jump"),
                DashDown = Input.GetKeyDown(dashKey),
                AntiDown = Input.GetKeyDown(antiGravityKey),
                HeavyDown = Input.GetKeyDown(heavyGravityKey),
                SelfDown = Input.GetKeyDown(selfGravityKey),
                FlipDown = Input.GetKeyDown(flipGravityKey),
                LockDown = Input.GetKeyDown(lockKey),
                ClickDown = Input.GetMouseButtonDown(0),
                Pointer = Input.mousePosition,
            };
        }
    }
}
