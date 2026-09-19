using Hullward.Domain.Ships;

namespace Hullward.Domain.Modules;

/// <summary>module类型（fit slot位类型）。</summary>
public enum ModuleType
{
    Weapon,
    Armor,
    Power,
    Special
}

/// <summary>
/// shipmoduleinterface（ULO2：interfacepolymorphism锚点）。
/// implement类：WeaponModule / ArmorModule / PowerModule / SpecialModule（iteration 4 fitsystem完整接入）。
/// </summary>
public interface IShipModule
{
    ModuleType Type { get; }
    string Name { get; }
    /// <summary>fit生效时对hullattribute施加bonus。</summary>
    void ApplyEffect(ShipBase ship);
}

/// <summary>weaponmodule：firepower/attack speed/crit（affix"reinforcement炮击/急速供弹/fatal一击/crit增幅"并入bonus）。</summary>
public sealed class WeaponModule : IShipModule
{
    public ModuleType Type => ModuleType.Weapon;
    public string Name { get; }
    public float FirepowerBonus { get; }
    public float FireRateBonus { get; }
    public float CritChanceBonus { get; }
    public float CritDamageBonus { get; }

    public WeaponModule(string name, float firepowerBonus, float fireRateBonus = 0f, float critChanceBonus = 0f, float critDamageBonus = 0f)
    {
        Name = name;
        FirepowerBonus = firepowerBonus;
        FireRateBonus = fireRateBonus;
        CritChanceBonus = critChanceBonus;
        CritDamageBonus = critDamageBonus;
    }

    public void ApplyEffect(ShipBase ship)
    {
        ship.AddFirepower(FirepowerBonus);
        ship.AddFireRate(FireRateBonus);
        if (CritChanceBonus > 0f)
        {
            ship.AddCritChance(CritChanceBonus);
        }
        if (CritDamageBonus > 0f)
        {
            ship.AddCritDamage(CritDamageBonus);
        }
    }
}

/// <summary>armormodule：shield/hull/抗性/damage reduction/thorns（affix"shieldscale out/hull加固/全向抗性/受击damage reduction/thorns镀层"并入bonus）。</summary>
public sealed class ArmorModule : IShipModule
{
    public ModuleType Type => ModuleType.Armor;
    public string Name { get; }
    public int ShieldBonus { get; }
    public int HullBonus { get; }
    public int ArmorBonus { get; }
    public float DamageReductionBonus { get; }
    public float ThornsBonus { get; }

    public ArmorModule(string name, int shieldBonus, int armorBonus, int hullBonus = 0, float damageReductionBonus = 0f, float thornsBonus = 0f)
    {
        Name = name;
        ShieldBonus = shieldBonus;
        ArmorBonus = armorBonus;
        HullBonus = hullBonus;
        DamageReductionBonus = damageReductionBonus;
        ThornsBonus = thornsBonus;
    }

    public void ApplyEffect(ShipBase ship)
    {
        ship.AddShield(ShieldBonus);
        ship.AddArmor(ArmorBonus);
        if (HullBonus > 0)
        {
            ship.AddMaxHull(HullBonus);
        }
        if (DamageReductionBonus > 0f)
        {
            ship.AddDamageReduction(DamageReductionBonus);
        }
        if (ThornsBonus > 0f)
        {
            ship.AddThorns(ThornsBonus);
        }
    }
}

/// <summary>能源module：energy条/skill系bonus（LD Sprint4 §3.2：能源scale out/快速charge/节能module/cooldown缩减；过载缓冲由skill侧消费）。</summary>
public sealed class PowerModule : IShipModule
{
    public ModuleType Type => ModuleType.Power;
    public string Name { get; }
    public float OverdriveBonus { get; }
    /// <summary>能源scale out：max energy +%（LD：8-12 / 暗金 25-30）。</summary>
    public float MaxEnergyPercent { get; }
    /// <summary>快速charge：energy regen +%（LD：8-12 / 暗金 25-30）。</summary>
    public float EnergyRegenPercent { get; }
    /// <summary>节能module：Q/E skillenergy cost -%（LD：5-8 / 暗金 15-20）。</summary>
    public float SkillCostPercent { get; }
    /// <summary>cooldown缩减：skillcooldown -%（LD：5-8 / 暗金 15-20）。</summary>
    public float CooldownPercent { get; }

    public PowerModule(string name, float overdriveBonus = 0f, float maxEnergyPercent = 0f, float energyRegenPercent = 0f, float skillCostPercent = 0f, float cooldownPercent = 0f)
    {
        Name = name;
        OverdriveBonus = overdriveBonus;
        MaxEnergyPercent = maxEnergyPercent;
        EnergyRegenPercent = energyRegenPercent;
        SkillCostPercent = skillCostPercent;
        CooldownPercent = cooldownPercent;
    }

    public void ApplyEffect(ShipBase ship)
    {
        if (MaxEnergyPercent > 0f)
        {
            ship.AddMaxEnergyPercent(MaxEnergyPercent);
        }
        if (EnergyRegenPercent > 0f)
        {
            ship.AddEnergyRegen(EnergyRegenPercent);
        }
        if (SkillCostPercent > 0f)
        {
            ship.AddSkillCostReduction(SkillCostPercent);
        }
        if (CooldownPercent > 0f)
        {
            ship.AddCooldownReduction(CooldownPercent);
        }
        // 过载缓冲：过载炮damagebonus由skill侧read取（OverdriveBonus 在 ActiveSkill 层消费）
    }
}

/// <summary>特殊module：寻宝增效等（affix"打捞增效"生效）。</summary>
public sealed class SpecialModule : IShipModule
{
    public ModuleType Type => ModuleType.Special;
    public string Name { get; }
    public int MagicFindBonus { get; }

    public SpecialModule(string name, int magicFindBonus = 0)
    {
        Name = name;
        MagicFindBonus = magicFindBonus;
    }

    public void ApplyEffect(ShipBase ship)
    {
        ship.AddMagicFind(MagicFindBonus);
    }
}
