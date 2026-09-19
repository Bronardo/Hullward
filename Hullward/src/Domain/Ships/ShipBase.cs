using System;
using System.Collections.Generic;
using Hullward.Domain.Combat;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Ships;

/// <summary>
/// shipabstraction基类（ULO2：abstraction + inheritance + polymorphism锚点）。
/// 四型hull派生：ScoutShip / AssaultShip / Battleship / FortressShip。
/// </summary>
public abstract class ShipBase : IShip
{
    public string Name { get; protected set; }
    public int Hull { get; set; }
    public int Shield { get; set; }

    /// <summary>shieldmax（fit/reset时sync），skill回盾与 UI using。</summary>
    public int MaxShield { get; private set; }

    /// <summary>hullmax（affix"hull加固"scale out）。</summary>
    public int MaxHull { get; private set; }

    /// <summary>attack speed倍率（affix"急速供弹"：1.0 = 无bonus，presentation攻击间隔按此缩放）。</summary>
    public float FireRateMultiplier { get; private set; } = 1f;

    /// <summary>寻宝值（affix"打捞增效"，alloydropbonus）。</summary>
    public int MagicFind { get; private set; }

    /// <summary>critchance 0-1（affix"fatal一击"）。</summary>
    public float CritChance { get; private set; }

    /// <summary>crit damage倍率（affix"crit增幅"，default 2.0）。</summary>
    public float CritDamage { get; private set; } = 2f;

    /// <summary>受击damage reduction %（affix"受击damage reduction"：受击后 2s window内生效）。</summary>
    public float DamageReductionPct { get; private set; }

    /// <summary>反弹近身damage %（affix"thorns镀层"）。</summary>
    public float ThornsPct { get; private set; }

    // ---------- energysystem（LD Sprint4 §3.2 拍板：轻量energy条） ----------

    /// <summary>energy基础max（LD：100，initial满）。</summary>
    public const float EnergyCapacityBase = 100f;

    /// <summary>combatmedium自然回复（LD：8/s）。</summary>
    public const float EnergyRegenCombat = 8f;

    /// <summary>脱战自然回复（LD：12/s）。</summary>
    public const float EnergyRegenOutOfCombat = 12f;

    /// <summary>max energy（affix"能源scale out"scale out）。</summary>
    public float MaxEnergy { get; private set; }

    /// <summary>currentenergy（0..MaxEnergy，skill消耗；combat/脱战回复）。</summary>
    public float Energy { get; private set; }

    /// <summary>energy regenbonus %（affix"快速charge"，combat/脱战两velocity同乘）。</summary>
    public float EnergyRegenBonus { get; private set; }

    /// <summary>skillenergy cost倍率（affix"节能module"：1.0 = 无reduction，乘法复合，min 0.2）。</summary>
    public float SkillCostMultiplier { get; private set; } = 1f;

    /// <summary>skillcooldown倍率（affix"cooldown缩减"：1.0 = 无缩减，乘法复合，min 0.2）。</summary>
    public float SkillCooldownMultiplier { get; private set; } = 1f;

    /// <summary>skillactualenergy cost（energy cost × 节能倍率）。</summary>
    public float EffectiveSkillCost(float baseCost) => baseCost * SkillCostMultiplier;

    /// <summary>skill是否energy充足（cooldownready判定由 ActiveSkill 层负责）。</summary>
    public bool HasEnergyFor(float baseCost) => Energy >= EffectiveSkillCost(baseCost) - 0.001f;

    /// <summary>消耗energy（不降为负）。</summary>
    public void SpendEnergy(float cost) => Energy = Math.Max(0f, Energy - cost);

    /// <summary>自然回复（combatmedium 8/s、脱战 12/s，均乘快速chargebonus；不超max）。</summary>
    public void RegenEnergy(float dt, bool inCombat)
    {
        float rate = (inCombat ? EnergyRegenCombat : EnergyRegenOutOfCombat) * (1f + EnergyRegenBonus / 100f);
        Energy = Math.Min(MaxEnergy, Energy + rate * dt);
    }

    private float _mitigationTimer;

    /// <summary>damage reductionwindow是否生效（受击后 2s）。</summary>
    public bool IsMitigating => _mitigationTimer > 0f;

    /// <summary>有效damage reduction率：window内取affix值，else 0。</summary>
    public float EffectiveDamageReduction => IsMitigating ? DamageReductionPct : 0f;

    /// <summary>受击时startup 2s damage reductionwindow。</summary>
    public void OnHit() => _mitigationTimer = 2f;

    /// <summary>递减damage reductionwindow（presentation每帧call）。</summary>
    public void TickTimers(float dt)
    {
        if (_mitigationTimer > 0f)
        {
            _mitigationTimer -= dt;
        }
    }

    public int Armor { get; protected set; }
    public float Firepower { get; protected set; }
    public float Speed { get; protected set; }
    public int ModuleSlots { get; protected set; }

    /// <summary>已fitmodule（interfacepolymorphism：Weapon/Armor/Power/Special）。</summary>
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
        MaxHull = hull;
        Armor = armor;
        Firepower = firepower;
        Speed = speed;
        ModuleSlots = moduleSlots;
        MaxEnergy = EnergyCapacityBase;
        Energy = EnergyCapacityBase;
        _baseHull = hull;
        _baseShield = shield;
        _baseFirepower = firepower;
    }

    public bool IsDestroyed => Hull <= 0;

    /// <summary>fitmodule并即时生效（由派生类implementattributebonus）。</summary>
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

    /// <summary>重生/load：resetcombatstate并重新applymodulebonus。</summary>
    public void ResetCombatState()
    {
        Hull = _baseHull;
        Shield = _baseShield;
        MaxShield = _baseShield;
        MaxHull = _baseHull;
        FireRateMultiplier = 1f;
        MagicFind = 0;
        CritChance = 0f;
        CritDamage = 2f;
        DamageReductionPct = 0f;
        ThornsPct = 0f;
        MaxEnergy = EnergyCapacityBase;
        Energy = EnergyCapacityBase;
        EnergyRegenBonus = 0f;
        SkillCostMultiplier = 1f;
        SkillCooldownMultiplier = 1f;
        _mitigationTimer = 0f;
        Firepower = _baseFirepower;
        foreach (var module in Modules)
        {
            module.ApplyEffect(this);
        }
    }

    // modulebonus入口（internal：同一program集的moduleimplement可call）
    internal void AddFirepower(float bonus) => Firepower += bonus;
    internal void AddShield(int bonus)
    {
        Shield += bonus;
        MaxShield += bonus;
    }

    internal void AddMaxHull(int bonus)
    {
        MaxHull += bonus;
        Hull += bonus; // scale outalso抬升currenthull（出战即满）
    }

    internal void AddFireRate(float multiplierBonus) => FireRateMultiplier += multiplierBonus;

    internal void AddMagicFind(int bonus) => MagicFind += bonus;

    internal void AddCritChance(float bonus) => CritChance = Math.Clamp(CritChance + bonus, 0f, 1f);

    internal void AddCritDamage(float bonus) => CritDamage += bonus;

    internal void AddDamageReduction(float bonus) => DamageReductionPct = Math.Clamp(DamageReductionPct + bonus, 0f, 0.8f);

    internal void AddThorns(float bonus) => ThornsPct += bonus;

    internal void AddArmor(int bonus) => Armor += bonus;

    /// <summary>能源scale out：max按current值复合scale out，currentenergysync抬升（出战即满）。</summary>
    internal void AddMaxEnergyPercent(float bonus)
    {
        float extra = MaxEnergy * bonus / 100f;
        MaxEnergy += extra;
        Energy += extra;
    }

    internal void AddEnergyRegen(float bonusPercent) => EnergyRegenBonus += bonusPercent;

    internal void AddSkillCostReduction(float bonusPercent)
        => SkillCostMultiplier = Math.Clamp(SkillCostMultiplier * (1f - bonusPercent / 100f), 0.2f, 1f);

    internal void AddCooldownReduction(float bonusPercent)
        => SkillCooldownMultiplier = Math.Clamp(SkillCooldownMultiplier * (1f - bonusPercent / 100f), 0.2f, 1f);
}

/// <summary>轻巡：balance机动，slot最少。</summary>
public sealed class ScoutShip : ShipBase
{
    public ScoutShip()
        : base("Scout", hull: 120, shield: 40, armor: 5, firepower: 10f, speed: 320f, moduleSlots: 2)
    {
    }
}

/// <summary>assault ship：firepower优先。</summary>
public sealed class AssaultShip : ShipBase
{
    public AssaultShip()
        : base("Assault", hull: 160, shield: 60, armor: 10, firepower: 16f, speed: 280f, moduleSlots: 3)
    {
    }
}

/// <summary>battleship：重装重firepower。</summary>
public sealed class Battleship : ShipBase
{
    public Battleship()
        : base("Battleship", hull: 280, shield: 120, armor: 25, firepower: 24f, speed: 200f, moduleSlots: 4)
    {
    }
}

/// <summary>fortress：极致生存。</summary>
public sealed class FortressShip : ShipBase
{
    public FortressShip()
        : base("Fortress", hull: 420, shield: 200, armor: 40, firepower: 18f, speed: 150f, moduleSlots: 5)
    {
    }
}
