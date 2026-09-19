using System;
using System.Collections.Generic;
using Hullward.Domain.Combat;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Ships;

/// <summary>
/// Ship abstraction base class (ULO2: abstraction + inheritance + polymorphism anchor)。
/// Four hull subclasses: ScoutShip / AssaultShip / Battleship / FortressShip。
/// </summary>
public abstract class ShipBase : IShip
{
    public string Name { get; protected set; }
    public int Hull { get; set; }
    public int Shield { get; set; }

    /// <summary>Max shield (synced on fit/reset); used by shield-boost skill and UI。</summary>
    public int MaxShield { get; private set; }

    /// <summary>Max hull (scaled up by "Hull Reinforcement" affix)。</summary>
    public int MaxHull { get; private set; }

    /// <summary>Attack speed multiplier ("Rapid Loading" affix; 1.0 = no bonus; presentation scales fire interval by this)。</summary>
    public float FireRateMultiplier { get; private set; } = 1f;

    /// <summary>Magic Find ("Magic Find" affix; alloy drop bonus)。</summary>
    public int MagicFind { get; private set; }

    /// <summary>Crit chance 0-1 ("Critical Hit" affix)。</summary>
    public float CritChance { get; private set; }

    /// <summary>Crit damage multiplier ("Critical Amp" affix; default 2.0)。</summary>
    public float CritDamage { get; private set; } = 2f;

    /// <summary>On-hit damage reduction % ("Damage Reduction" affix; active 2s window after being hit)。</summary>
    public float DamageReductionPct { get; private set; }

    /// <summary>Reflect melee damage % ("Thorns Plating" affix)。</summary>
    public float ThornsPct { get; private set; }

    // ---------- energy system (LD Sprint 4 §3.2: lightweight energy bar) ----------

    /// <summary>Base max energy (LD: 100, starts full)。</summary>
    public const float EnergyCapacityBase = 100f;

    /// <summary>Passive regen in combat (LD: 8/s)。</summary>
    public const float EnergyRegenCombat = 8f;

    /// <summary>Passive regen out of combat (LD: 12/s)。</summary>
    public const float EnergyRegenOutOfCombat = 12f;

    /// <summary>Max energy (scaled up by "Energy Capacitor" affix)。</summary>
    public float MaxEnergy { get; private set; }

    /// <summary>Current energy (0..MaxEnergy; consumed by skills; regen in/out of combat)。</summary>
    public float Energy { get; private set; }

    /// <summary>Energy regen bonus % ("Fast Recharge" affix; applies to both combat and out-of-combat rates)。</summary>
    public float EnergyRegenBonus { get; private set; }

    /// <summary>Skill energy cost multiplier ("Skill Cost Down" affix; 1.0 = no reduction; multiplicative, min 0.2)。</summary>
    public float SkillCostMultiplier { get; private set; } = 1f;

    /// <summary>Skill cooldown multiplier ("Cooldown Reduction" affix; 1.0 = no reduction; multiplicative, min 0.2)。</summary>
    public float SkillCooldownMultiplier { get; private set; } = 1f;

    /// <summary>Actual skill energy cost (energy cost x cost multiplier)。</summary>
    public float EffectiveSkillCost(float baseCost) => baseCost * SkillCostMultiplier;

    /// <summary>Has enough energy for skill (cooldown readiness is handled by ActiveSkill)。</summary>
    public bool HasEnergyFor(float baseCost) => Energy >= EffectiveSkillCost(baseCost) - 0.001f;

    /// <summary>Consume energy (never goes negative)。</summary>
    public void SpendEnergy(float cost) => Energy = Math.Max(0f, Energy - cost);

    /// <summary>Passive regen (combat 8/s, out of combat 12/s, both multiplied by Fast Recharge bonus; capped at max)。</summary>
    public void RegenEnergy(float dt, bool inCombat)
    {
        float rate = (inCombat ? EnergyRegenCombat : EnergyRegenOutOfCombat) * (1f + EnergyRegenBonus / 100f);
        Energy = Math.Min(MaxEnergy, Energy + rate * dt);
    }

    private float _mitigationTimer;

    /// <summary>Is damage reduction window active (2s after being hit)。</summary>
    public bool IsMitigating => _mitigationTimer > 0f;

    /// <summary>Effective damage reduction: affix value if in window, else 0。</summary>
    public float EffectiveDamageReduction => IsMitigating ? DamageReductionPct : 0f;

    /// <summary>Start 2s damage reduction window when hit。</summary>
    public void OnHit() => _mitigationTimer = 2f;

    /// <summary>Tick down damage reduction window (called every frame by presentation)。</summary>
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

    /// <summary>Fitted modules (interface polymorphism: Weapon/Armor/Power/Special)。</summary>
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

    /// <summary>Fit module and apply immediately (derived class implements attribute bonus)。</summary>
    public void EquipModule(IShipModule module)
    {
        if (Modules.Count >= ModuleSlots)
        {
            throw new InvalidOperationException($"{Name} slots full ({ModuleSlots})");
        }
        Modules.Add(module);
        module.ApplyEffect(this);
    }

    public virtual void TakeHit(int damage) => CombatCalculator.ApplyHit(this, damage);

    /// <summary>On respawn/load: reset combat state and re-apply module bonuses。</summary>
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

    // Module bonus entry (internal: module impls in same assembly can call)
    internal void AddFirepower(float bonus) => Firepower += bonus;
    internal void AddShield(int bonus)
    {
        Shield += bonus;
        MaxShield += bonus;
    }

    internal void AddMaxHull(int bonus)
    {
        MaxHull += bonus;
        Hull += bonus; // Scale up also raises current hull (starts full on sortie)
    }

    internal void AddFireRate(float multiplierBonus) => FireRateMultiplier += multiplierBonus;

    internal void AddMagicFind(int bonus) => MagicFind += bonus;

    internal void AddCritChance(float bonus) => CritChance = Math.Clamp(CritChance + bonus, 0f, 1f);

    internal void AddCritDamage(float bonus) => CritDamage += bonus;

    internal void AddDamageReduction(float bonus) => DamageReductionPct = Math.Clamp(DamageReductionPct + bonus, 0f, 0.8f);

    internal void AddThorns(float bonus) => ThornsPct += bonus;

    internal void AddArmor(int bonus) => Armor += bonus;

    /// <summary>Energy scale-up: max scales multiplicatively, current energy syncs up (starts full on sortie)。</summary>
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

/// <summary>Scout: balanced mobility, fewest slots。</summary>
public sealed class ScoutShip : ShipBase
{
    public ScoutShip()
        : base("Scout", hull: 120, shield: 40, armor: 5, firepower: 10f, speed: 320f, moduleSlots: 2)
    {
    }
}

/// <summary>Assault ship: firepower-focused。</summary>
public sealed class AssaultShip : ShipBase
{
    public AssaultShip()
        : base("Assault", hull: 160, shield: 60, armor: 10, firepower: 16f, speed: 280f, moduleSlots: 3)
    {
    }
}

/// <summary>Battleship: heavy armor and firepower。</summary>
public sealed class Battleship : ShipBase
{
    public Battleship()
        : base("Battleship", hull: 280, shield: 120, armor: 25, firepower: 24f, speed: 200f, moduleSlots: 4)
    {
    }
}

/// <summary>Fortress: extreme survivability。</summary>
public sealed class FortressShip : ShipBase
{
    public FortressShip()
        : base("Fortress", hull: 420, shield: 200, armor: 40, firepower: 18f, speed: 150f, moduleSlots: 5)
    {
    }
}
