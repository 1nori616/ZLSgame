using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 一次伤害的完整描述：谁打的、打多少、往哪飞。
    /// 只读数据，不包含任何逻辑。
    /// </summary>
    public struct DamageInfo
    {
        public int Amount;          // 扣几格血
        public Vector2 Knockback;   // 击退方向 * 力度（x 会被按受击方向校正）
        public GameObject Source;   // 造成伤害的对象

        public DamageInfo(int amount, Vector2 knockback, GameObject source)
        {
            Amount = amount;
            Knockback = knockback;
            Source = source;
        }
    }
}
