using System;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Combat;

/// <summary>skill施放context（玩家currentstate与lock定target）。</summary>
public sealed class PlayerContext
{
    public required ShipBase Ship { get; init; }
    public ITargetable? LockedTarget { get; init; }
}

/// <summary>
/// 主动skillabstraction基类（拍板项：主炮auto索敌 + manual主动skill）。
/// cooldown/施放条件/效果均为纯 C# 域层，可单测。
/// </summary>
public abstract class ActiveSkill
{
    public string Name { get; }
    public float Cooldown { get; }
    /// <summary>基础energy cost（LD Sprint4 §3.2：Q 过载炮 30 / E shield boost 40；affix"节能module"reduction）。</summary>
    public float EnergyCost { get; }
    public float Remaining { get; private set; }
    public bool IsReady => Remaining <= 0f;

    protected ActiveSkill(string name, float cooldown, float energyCost)
    {
        Name = name;
        Cooldown = cooldown;
        EnergyCost = energyCost;
    }

    /// <summary>
    /// 尝试施放：cooldownready + 条件满足 + energy充足则生效并entercooldown（cooldown受"cooldown缩减"affix缩放）。
    /// energy不足reject施放（LD §3.2：skill不可用 + 不扣费不resetcooldown）。
    /// </summary>
    public bool TryUse(PlayerContext context)
    {
        if (!IsReady || !CanUse(context))
        {
            return false;
        }
        if (!context.Ship.HasEnergyFor(EnergyCost))
        {
            return false; // energy不足：skill不可用
        }
        Apply(context);
        context.Ship.SpendEnergy(context.Ship.EffectiveSkillCost(EnergyCost));
        Remaining = Cooldown * context.Ship.SkillCooldownMultiplier;
        return true;
    }

    public void Tick(float dt)
    {
        if (Remaining > 0f)
        {
            Remaining = Math.Max(0f, Remaining - dt);
        }
    }

    protected abstract bool CanUse(PlayerContext context);
    protected abstract void Apply(PlayerContext context);
}

/// <summary>Q：过载炮——对lock定target造成 3× firepower直击damage。</summary>
public sealed class OverdriveCannon : ActiveSkill
{
    public const float DamageMultiplier = 3f;

    public OverdriveCannon()
        : base("过载炮", cooldown: 6f, energyCost: 30f) // LD §3.2：Q energy cost 30
    {
    }

    protected override bool CanUse(PlayerContext context) =>
        context.LockedTarget != null && context.LockedTarget.Hull > 0;

    protected override void Apply(PlayerContext context)
    {
        int damage = Math.Max(1, (int)(context.Ship.Firepower * DamageMultiplier));
        context.LockedTarget!.TakeHit(damage);
    }
}

/// <summary>E：shield boost——instant回复 50% shieldmax。</summary>
public sealed class ShieldBurst : ActiveSkill
{
    public ShieldBurst()
        : base("护盾充能", cooldown: 10f, energyCost: 40f) // LD §3.2：E energy cost 40
    {
    }

    protected override bool CanUse(PlayerContext context) =>
        context.Ship.Shield < context.Ship.MaxShield;

    protected override void Apply(PlayerContext context)
    {
        int restore = Math.Max(1, context.Ship.MaxShield / 2);
        context.Ship.Shield = Math.Min(context.Ship.MaxShield, context.Ship.Shield + restore);
    }
}
