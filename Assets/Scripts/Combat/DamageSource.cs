using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 挂在"会造成伤害的东西"上：尖刺、敌人的攻击判定、飞行道具。
    /// 需要目标自己来检测（玩家用 PlayerHurtbox 检测）。
    /// </summary>
    public class DamageSource : MonoBehaviour
    {
        [SerializeField] private int amount = 1;
        [SerializeField] private Vector2 knockback = new Vector2(7f, 6f);
        [Tooltip("命中一次后销毁自己（比如一次性弹幕）")]
        [SerializeField] private bool destroyAfterHit;

        public int Amount => amount;
        public Vector2 Knockback => knockback;
        public bool DestroyAfterHit => destroyAfterHit;

        public void Consume()
        {
            if (destroyAfterHit) Destroy(gameObject);
        }
    }
}
