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

    /// <summary>船体耐久上限（词缀"船体加固"扩容）。</summary>
    public int MaxHull { get; private set; }

    /// <summary>攻速倍率（词缀"急速供弹"：1.0 = 无加成，表现层攻击间隔按此缩放）。</summary>
    public float FireRateMultiplier { get; private set; } = 1f;

    /// <summary>寻宝值（词缀"打捞增效"，合金掉落加成）。</summary>
    public int MagicFind { get; private set; }

    /// <summary>暴击概率 0-1（词缀"致命一击"）。</summary>
    public float CritChance { get; private set; }

    /// <summary>暴击伤害倍率（词缀"暴击增幅"，默认 2.0）。</summary>
    public float CritDamage { get; private set; } = 2f;

    /// <summary>受击减伤 %（词缀"受击减伤"：受击后 2s 窗口内生效）。</summary>
    public float DamageReductionPct { get; private set; }

    /// <summary>反弹近身伤害 %（词缀"反伤镀层"）。</summary>
    public float ThornsPct { get; private set; }

    // ---------- 能量系统（LD Sprint4 §3.2 拍板：轻量能量条） ----------

    /// <summary>能量基础上限（LD：100，初始满）。</summary>
    public const float EnergyCapacityBase = 100f;

    /// <summary>战斗中自然回复（LD：8/s）。</summary>
    public const float EnergyRegenCombat = 8f;

    /// <summary>脱战自然回复（LD：12/s）。</summary>
    public const float EnergyRegenOutOfCombat = 12f;

    /// <summary>能量上限（词缀"能源扩容"扩容）。</summary>
    public float MaxEnergy { get; private set; }

    /// <summary>当前能量（0..MaxEnergy，技能消耗；战斗/脱战回复）。</summary>
    public float Energy { get; private set; }

    /// <summary>能量回复加成 %（词缀"快速充能"，战斗/脱战两速率同乘）。</summary>
    public float EnergyRegenBonus { get; private set; }

    /// <summary>技能能耗倍率（词缀"节能模块"：1.0 = 无减免，乘法复合，下限 0.2）。</summary>
    public float SkillCostMultiplier { get; private set; } = 1f;

    /// <summary>技能冷却倍率（词缀"冷却缩减"：1.0 = 无缩减，乘法复合，下限 0.2）。</summary>
    public float SkillCooldownMultiplier { get; private set; } = 1f;

    /// <summary>技能实际能耗（能耗 × 节能倍率）。</summary>
    public float EffectiveSkillCost(float baseCost) => baseCost * SkillCostMultiplier;

    /// <summary>技能是否能量充足（冷却就绪判定由 ActiveSkill 层负责）。</summary>
    public bool HasEnergyFor(float baseCost) => Energy >= EffectiveSkillCost(baseCost) - 0.001f;

    /// <summary>消耗能量（不降为负）。</summary>
    public void SpendEnergy(float cost) => Energy = Math.Max(0f, Energy - cost);

    /// <summary>自然回复（战斗中 8/s、脱战 12/s，均乘快速充能加成；不超上限）。</summary>
    public void RegenEnergy(float dt, bool inCombat)
    {
        float rate = (inCombat ? EnergyRegenCombat : EnergyRegenOutOfCombat) * (1f + EnergyRegenBonus / 100f);
        Energy = Math.Min(MaxEnergy, Energy + rate * dt);
    }

    private float _mitigationTimer;

    /// <summary>减伤窗口是否生效（受击后 2s）。</summary>
    public bool IsMitigating => _mitigationTimer > 0f;

    /// <summary>有效减伤率：窗口内取词缀值，否则 0。</summary>
    public float EffectiveDamageReduction => IsMitigating ? DamageReductionPct : 0f;

    /// <summary>受击时启动 2s 减伤窗口。</summary>
    public void OnHit() => _mitigationTimer = 2f;

    /// <summary>递减减伤窗口（表现层每帧调用）。</summary>
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

    // 模块加成入口（internal：同一程序集的模块实现可调用）
    internal void AddFirepower(float bonus) => Firepower += bonus;
    internal void AddShield(int bonus)
    {
        Shield += bonus;
        MaxShield += bonus;
    }

    internal void AddMaxHull(int bonus)
    {
        MaxHull += bonus;
        Hull += bonus; // 扩容同时抬升当前耐久（出战即满）
    }

    internal void AddFireRate(float multiplierBonus) => FireRateMultiplier += multiplierBonus;

    internal void AddMagicFind(int bonus) => MagicFind += bonus;

    internal void AddCritChance(float bonus) => CritChance = Math.Clamp(CritChance + bonus, 0f, 1f);

    internal void AddCritDamage(float bonus) => CritDamage += bonus;

    internal void AddDamageReduction(float bonus) => DamageReductionPct = Math.Clamp(DamageReductionPct + bonus, 0f, 0.8f);

    internal void AddThorns(float bonus) => ThornsPct += bonus;

    internal void AddArmor(int bonus) => Armor += bonus;

    /// <summary>能源扩容：上限按当前值复合扩容，当前能量同步抬升（出战即满）。</summary>
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
