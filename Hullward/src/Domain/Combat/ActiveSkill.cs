using System;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Combat;

/// <summary>技能施放上下文（玩家当前状态与锁定目标）。</summary>
public sealed class PlayerContext
{
    public required ShipBase Ship { get; init; }
    public ITargetable? LockedTarget { get; init; }
}

/// <summary>
/// 主动技能抽象基类（拍板项：主炮自动索敌 + 手动主动技能）。
/// 冷却/施放条件/效果均为纯 C# 域层，可单测。
/// </summary>
public abstract class ActiveSkill
{
    public string Name { get; }
    public float Cooldown { get; }
    public float Remaining { get; private set; }
    public bool IsReady => Remaining <= 0f;

    protected ActiveSkill(string name, float cooldown)
    {
        Name = name;
        Cooldown = cooldown;
    }

    /// <summary>尝试施放：就绪 + 条件满足则生效并进入冷却。</summary>
    public bool TryUse(PlayerContext context)
    {
        if (!IsReady || !CanUse(context))
        {
            return false;
        }
        Apply(context);
        Remaining = Cooldown;
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

/// <summary>Q：过载炮——对锁定目标造成 3× 火力直击伤害。</summary>
public sealed class OverdriveCannon : ActiveSkill
{
    public const float DamageMultiplier = 3f;

    public OverdriveCannon()
        : base("过载炮", cooldown: 6f)
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

/// <summary>E：护盾充能——立即回复 50% 护盾上限。</summary>
public sealed class ShieldBurst : ActiveSkill
{
    public ShieldBurst()
        : base("护盾充能", cooldown: 10f)
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
