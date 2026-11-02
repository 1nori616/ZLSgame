using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 玩家所有可调数值。放在 ScriptableObject 里，策划改数值不用改代码。
    /// 右键 Create > Game > Player Config 生成资产。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Game/Player Config")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("水平移动")]
        [Tooltip("最大水平速度")]
        public float moveSpeed = 7f;

        [Tooltip("加速度：越大越跟手")]
        public float acceleration = 70f;

        [Tooltip("松开方向键后的减速度")]
        public float deceleration = 90f;

        [Header("跳跃")]
        public float jumpForce = 13f;

        [Tooltip("基础重力倍率")]
        public float gravityScale = 3.5f;

        [Tooltip("下落时的重力倍率，>1 会让人物落地更干脆")]
        public float fallGravityMultiplier = 1.6f;

        [Tooltip("土狼时间：离开平台后仍可起跳的宽限")]
        public float coyoteTime = 0.1f;

        [Tooltip("跳跃缓冲：落地前按跳也算数")]
        public float jumpBufferTime = 0.12f;

        [Tooltip("上升中松开跳跃键时，纵向速度保留的比例")]
        public float jumpCutMultiplier = 0.5f;

        [Header("冲刺")]
        public float dashSpeed = 18f;
        public float dashDuration = 0.15f;
        public float dashCooldown = 0.5f;

        [Header("接地检测")]
        [Tooltip("哪些层算地面")]
        public LayerMask groundLayer = ~0;

        [Tooltip("检测盒宽度相对于碰撞体宽度的比例")]
        [Range(0.5f, 1f)]
        public float groundCheckWidthScale = 0.9f;

        [Tooltip("检测盒相对脚底向下的距离")]
        public float groundCheckDistance = 0.06f;
    }
}
