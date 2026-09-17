using System;
using Hullward.Domain.Ships;

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
/// 公式：命中伤害 = max(1, 火力 × 技能系数 − 抗性减免)；暴击 = 火力 × 暴伤倍率。
/// 吸收顺序：护盾优先，溢出转船体耐久。
/// 减伤：受击后 2s 窗口内按"受击减伤"词缀比例减免（LD Sprint 3 §4.1）。
/// </summary>
public static class CombatCalculator
{
    /// <summary>计算一次命中的最终伤害（下限 1，保证任何命中都有威胁）。</summary>
    public static int CalculateDamage(int firepower, float skillMultiplier = 1f, int armorReduction = 0)
    {
        int raw = (int)Math.Round(firepower * skillMultiplier);
        return Math.Max(1, raw - armorReduction);
    }

    /// <summary>
    /// 攻击者开火伤害结算：按暴击率判定，暴击则乘暴伤倍率（LD Sprint 3 词缀"致命一击/暴击增幅"）。
    /// </summary>
    public static int RollAttackDamage(ShipBase attacker, Random rng, out bool isCritical)
    {
        int dmg = Math.Max(1, (int)attacker.Firepower);
        bool crit = attacker.CritChance > 0f && rng.NextDouble() < attacker.CritChance;
        if (crit)
        {
            dmg = Math.Max(1, (int)(dmg * attacker.CritDamage));
        }
        isCritical = crit;
        return dmg;
    }

    /// <summary>对目标应用伤害：受击减伤窗口（上次受击后 2s 内减免）+ 护盾先吸收，溢出转耐久。</summary>
    public static void ApplyHit(IShip target, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        // 受击减伤（词缀"受击减伤"）：减伤窗口 = 上次受击后 2s；本次受击后刷新窗口
        if (target is ShipBase ship)
        {
            float reduction = ship.EffectiveDamageReduction;
            if (reduction > 0f)
            {
                damage = Math.Max(1, (int)(damage * (1f - reduction)));
            }
            ship.OnHit();
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
