using Hullward.Domain.Ships;

namespace Hullward.Domain.Modules;

/// <summary>模块类型（装配槽位类型）。</summary>
public enum ModuleType
{
    Weapon,
    Armor,
    Power,
    Special
}

/// <summary>
/// 舰船模块接口（ULO2：接口多态锚点）。
/// 实现类：WeaponModule / ArmorModule / PowerModule / SpecialModule（迭代 4 装配系统完整接入）。
/// </summary>
public interface IShipModule
{
    ModuleType Type { get; }
    string Name { get; }
    /// <summary>装配生效时对船体属性施加加成。</summary>
    void ApplyEffect(ShipBase ship);
}

/// <summary>武器模块：火力/攻速/暴击（词缀"强化炮击/急速供弹/致命一击/暴击增幅"并入加成）。</summary>
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

/// <summary>装甲模块：护盾/耐久/抗性/减伤/反伤（词缀"护盾扩容/船体加固/全向抗性/受击减伤/反伤镀层"并入加成）。</summary>
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

/// <summary>能源模块：能源/技能系加成（词缀"过载缓冲"生效；能源池数值预留）。</summary>
public sealed class PowerModule : IShipModule
{
    public ModuleType Type => ModuleType.Power;
    public string Name { get; }
    public float OverdriveBonus { get; }

    public PowerModule(string name, float overdriveBonus = 0f)
    {
        Name = name;
        OverdriveBonus = overdriveBonus;
    }

    public void ApplyEffect(ShipBase ship)
    {
        // 过载缓冲：过载炮伤害加成由技能侧读取（OverdriveBonus 在 ActiveSkill 层消费）
    }
}

/// <summary>特殊模块：寻宝增效等（词缀"打捞增效"生效）。</summary>
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
