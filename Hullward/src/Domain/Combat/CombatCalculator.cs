using System;

namespace Hullward.Domain.Combat;

/// <summary>可受击实体（舰船/可破坏物）：护盾 + 船体耐久。</summary>
public interface IShip
{
    string Name { get; }
    int Hull { get; set; }
    int Shield { get; set; }
    int Armor { get; }
}

/// <summary>
/// 战斗结算（纯 C# 域层，可单测）。
/// 公式：命中伤害 = max(1, 火力 × 技能系数 − 抗性减免)
/// 吸收顺序：护盾优先，溢出转船体耐久。
/// </summary>
public static class CombatCalculator
{
    /// <summary>计算一次命中的最终伤害（下限 1，保证任何命中都有威胁）。</summary>
    public static int CalculateDamage(int firepower, float skillMultiplier = 1f, int armorReduction = 0)
    {
        int raw = (int)Math.Round(firepower * skillMultiplier);
        return Math.Max(1, raw - armorReduction);
    }

    /// <summary>对目标应用伤害：护盾先吸收，溢出转耐久。</summary>
    public static void ApplyHit(IShip target, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        int shieldAbsorbed = Math.Min(target.Shield, damage);
        target.Shield -= shieldAbsorbed;
        int remaining = damage - shieldAbsorbed;

        if (remaining > 0)
        {
            target.Hull = Math.Max(0, target.Hull - remaining);
        }
    }
}
