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
        public float moveSpeed = 7f;
        public float acceleration = 70f;
        public float deceleration = 90f;

        [Header("跳跃")]
        public float jumpForce = 13f;
        public float gravityScale = 3.5f;
        [Tooltip("下落时的重力倍率，>1 会让人物落地更干脆")]
        public float fallGravityMultiplier = 1.6f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.12f;
        public float jumpCutMultiplier = 0.5f;

        [Header("冲刺")]
        public float dashSpeed = 18f;
        public float dashDuration = 0.15f;
        public float dashCooldown = 0.5f;

        [Header("重力操控")]
        [Tooltip("重力影响持续时间（秒）")]
        public float gravityDuration = 2f;

        [Tooltip("最多攒几格充能")]
        public int gravityCharges = 3;

        [Tooltip("每恢复一格充能需要几秒")]
        public float gravityRechargeTime = 1.2f;

        [Tooltip("施放时镜头聚焦的时长（秒）")]
        public float cameraFocusDuration = 0.8f;

        [Header("重力翻转（F）")]
        [Tooltip("翻转一次持续多少秒")]
        public float flipDuration = 10f;

        [Tooltip("两次翻转之间的冷却")]
        public float flipCooldown = 3f;

        [Header("对自己施放（R）")]
        public float selfAntiAcceleration = 16f;
        public float selfAntiMaxSpeed = 6f;

        [Header("施放判定")]
        [Tooltip("以玩家为中心的搜索半径（前后都能选中）")]
        public float castRadius = 3.5f;

        [Tooltip("朝向前方的目标权重更高：距离 × 这个系数")]
        [Range(0.2f, 1f)]
        public float frontPreference = 0.6f;

        [Tooltip("施放判定能命中的层")]
        public LayerMask castLayer = ~0;

        [Header("受击")]
        public float hurtInvincibleTime = 0.5f;
        public float knockbackTime = 0.18f;

        [Header("接地检测")]
        public LayerMask groundLayer = ~0;
        [Range(0.5f, 1f)] public float groundCheckWidthScale = 0.9f;
        public float groundCheckDistance = 0.06f;
    }
}
