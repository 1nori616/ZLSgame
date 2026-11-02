namespace Game.Combat
{
    /// <summary>
    /// "能被伤害"的契约。木桩、敌人、玩家都实现它，
    /// 攻击方只依赖这个接口，不需要知道打的是谁。
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        bool IsInvincible { get; }
        void TakeDamage(in DamageInfo info);
    }
}
