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
/// 实现类：WeaponModule / ArmorModule / PowerModule / SpecialModule（Day 4 装配系统完整接入）。
/// </summary>
public interface IShipModule
{
    ModuleType Type { get; }
    string Name { get; }
    /// <summary>装配生效时对船体属性施加加成。</summary>
    void ApplyEffect(ShipBase ship);
}

/// <summary>武器模块：提升火力与射速。</summary>
public sealed class WeaponModule : IShipModule
{
    public ModuleType Type => ModuleType.Weapon;
    public string Name { get; }
    public float FirepowerBonus { get; }
    public float FireRateBonus { get; }

    public WeaponModule(string name, float firepowerBonus, float fireRateBonus = 0f)
    {
        Name = name;
        FirepowerBonus = firepowerBonus;
        FireRateBonus = fireRateBonus;
    }

    public void ApplyEffect(ShipBase ship)
    {
        ship.AddFirepower(FirepowerBonus);
    }
}

/// <summary>装甲模块：提升护盾与抗性。</summary>
public sealed class ArmorModule : IShipModule
{
    public ModuleType Type => ModuleType.Armor;
    public string Name { get; }
    public int ShieldBonus { get; }
    public int ArmorBonus { get; }

    public ArmorModule(string name, int shieldBonus, int armorBonus)
    {
        Name = name;
        ShieldBonus = shieldBonus;
        ArmorBonus = armorBonus;
    }

    public void ApplyEffect(ShipBase ship)
    {
        ship.AddShield(ShieldBonus);
        ship.AddArmor(ArmorBonus);
    }
}
