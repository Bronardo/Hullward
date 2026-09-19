using System;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Combat;

/// <summary>可受击entity（ship/可破坏物）：shield + hull。</summary>
public interface IShip
{
    string Name { get; }
    int Hull { get; set; }
    int Shield { get; set; }
    int Armor { get; }
}

/// <summary>
/// combat结算（纯 C# 域层，可单测）。
/// 公式：hitdamage = max(1, firepower × skill系数 − 抗性reduction)；crit = firepower × 暴伤倍率。
/// 吸收ordering：shield优先，溢出转hull。
/// damage reduction：受击后 2s window内按"受击damage reduction"affix比例reduction（LD Sprint 3 §4.1）。
/// </summary>
public static class CombatCalculator
{
    /// <summary>计算一次hit的finaldamage（min 1，保证任何hit都有威胁）。</summary>
    public static int CalculateDamage(int firepower, float skillMultiplier = 1f, int armorReduction = 0)
    {
        int raw = (int)Math.Round(firepower * skillMultiplier);
        return Math.Max(1, raw - armorReduction);
    }

    /// <summary>
    /// 攻击者开火damage结算：按crit chance判定，crit则乘暴伤倍率（LD Sprint 3 affix"fatal一击/crit增幅"）。
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

    /// <summary>对targetapplydamage：受击damage reductionwindow（上次受击后 2s 内reduction）+ shield先吸收，溢出转hull。</summary>
    public static void ApplyHit(IShip target, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        // 受击damage reduction（affix"受击damage reduction"）：damage reductionwindow = 上次受击后 2s；本次受击后refreshwindow
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
