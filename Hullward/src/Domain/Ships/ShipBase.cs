using System;
using System.Collections.Generic;
using Hullward.Domain.Combat;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Ships;

/// <summary>
/// 舰船抽象基类（ULO2：抽象 + 继承 + 多态锚点）。
/// 四型船体派生：ScoutShip / AssaultShip / Battleship / FortressShip。
/// </summary>
public abstract class ShipBase : IShip
{
    public string Name { get; protected set; }
    public int Hull { get; set; }
    public int Shield { get; set; }

    /// <summary>护盾上限（装配/重置时同步），技能回盾与 UI 使用。</summary>
    public int MaxShield { get; private set; }

    public int Armor { get; protected set; }
    public float Firepower { get; protected set; }
    public float Speed { get; protected set; }
    public int ModuleSlots { get; protected set; }

    /// <summary>已装配模块（接口多态：Weapon/Armor/Power/Special）。</summary>
    public List<IShipModule> Modules { get; } = new();

    private readonly int _baseHull;
    private readonly int _baseShield;
    private readonly float _baseFirepower;

    protected ShipBase(string name, int hull, int shield, int armor, float firepower, float speed, int moduleSlots)
    {
        Name = name;
        Hull = hull;
        Shield = shield;
        MaxShield = shield;
        Armor = armor;
        Firepower = firepower;
        Speed = speed;
        ModuleSlots = moduleSlots;
        _baseHull = hull;
        _baseShield = shield;
        _baseFirepower = firepower;
    }

    public bool IsDestroyed => Hull <= 0;

    /// <summary>装配模块并即时生效（由派生类实现属性加成）。</summary>
    public void EquipModule(IShipModule module)
    {
        if (Modules.Count >= ModuleSlots)
        {
            throw new InvalidOperationException($"{Name} 槽位已满（{ModuleSlots}）");
        }
        Modules.Add(module);
        module.ApplyEffect(this);
    }

    public virtual void TakeHit(int damage) => CombatCalculator.ApplyHit(this, damage);

    /// <summary>重生/读档：重置战斗状态并重新应用模块加成。</summary>
    public void ResetCombatState()
    {
        Hull = _baseHull;
        Shield = _baseShield;
        MaxShield = _baseShield;
        Firepower = _baseFirepower;
        foreach (var module in Modules)
        {
            module.ApplyEffect(this);
        }
    }

    // 模块加成入口（internal：同一程序集的模块实现可调用）
    internal void AddFirepower(float bonus) => Firepower += bonus;
    internal void AddShield(int bonus)
    {
        Shield += bonus;
        MaxShield += bonus;
    }

    internal void AddArmor(int bonus) => Armor += bonus;
}

/// <summary>轻巡：均衡机动，槽位最少。</summary>
public sealed class ScoutShip : ShipBase
{
    public ScoutShip()
        : base("轻巡", hull: 120, shield: 40, armor: 5, firepower: 10f, speed: 320f, moduleSlots: 2)
    {
    }
}

/// <summary>突击舰：火力优先。</summary>
public sealed class AssaultShip : ShipBase
{
    public AssaultShip()
        : base("突击舰", hull: 160, shield: 60, armor: 10, firepower: 16f, speed: 280f, moduleSlots: 3)
    {
    }
}

/// <summary>战列舰：重装重火力。</summary>
public sealed class Battleship : ShipBase
{
    public Battleship()
        : base("战列舰", hull: 280, shield: 120, armor: 25, firepower: 24f, speed: 200f, moduleSlots: 4)
    {
    }
}

/// <summary>要塞舰：极致生存。</summary>
public sealed class FortressShip : ShipBase
{
    public FortressShip()
        : base("要塞舰", hull: 420, shield: 200, armor: 40, firepower: 18f, speed: 150f, moduleSlots: 5)
    {
    }
}
