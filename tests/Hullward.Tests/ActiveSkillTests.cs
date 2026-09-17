using Hullward.Domain.Combat;
using Hullward.Domain.Ships;
using Xunit;

namespace Hullward.Tests.Domain;

public class ActiveSkillTests
{
    private sealed class Target : ITargetable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int Hull { get; set; }
        public void TakeHit(int damage) => Hull = System.Math.Max(0, Hull - damage);
    }

    [Fact]
    public void OverdriveCannon_DealsTripleFirepower_ToLockedTarget()
    {
        var ship = new ScoutShip(); // 火力 10
        var target = new Target { X = 100, Y = 0, Hull = 100 };
        var skill = new OverdriveCannon();

        bool used = skill.TryUse(new PlayerContext { Ship = ship, LockedTarget = target });

        Assert.True(used);
        Assert.Equal(70, target.Hull); // 100 - 30
        Assert.False(skill.IsReady); // 进入冷却
    }

    [Fact]
    public void OverdriveCannon_WithoutTarget_CannotUse()
    {
        var ship = new ScoutShip();
        var skill = new OverdriveCannon();

        Assert.False(skill.TryUse(new PlayerContext { Ship = ship, LockedTarget = null }));
        Assert.True(skill.IsReady);
    }

    [Fact]
    public void Skill_Cooldown_TicksDownAndRecovers()
    {
        var ship = new ScoutShip();
        var target = new Target { X = 10, Y = 0, Hull = 100 };
        var skill = new OverdriveCannon();
        skill.TryUse(new PlayerContext { Ship = ship, LockedTarget = target });

        skill.Tick(3f);
        Assert.False(skill.IsReady);

        skill.Tick(3f);
        Assert.True(skill.IsReady);
    }

    [Fact]
    public void ShieldBurst_RestoresHalfMaxShield()
    {
        var ship = new ScoutShip(); // 盾 40 / 上限 40
        ship.Shield = 0;
        var skill = new ShieldBurst();

        bool used = skill.TryUse(new PlayerContext { Ship = ship });

        Assert.True(used);
        Assert.Equal(20, ship.Shield); // 40 / 2
        Assert.False(skill.IsReady);
    }

    [Fact]
    public void ShieldBurst_AtFullShield_CannotUse()
    {
        var ship = new ScoutShip();
        var skill = new ShieldBurst();

        Assert.False(skill.TryUse(new PlayerContext { Ship = ship }));
        Assert.True(skill.IsReady);
    }

    [Fact]
    public void ShieldBurst_NeverExceedsMaxShield()
    {
        var ship = new ScoutShip();
        ship.Shield = 30;
        var skill = new ShieldBurst();

        skill.TryUse(new PlayerContext { Ship = ship });

        Assert.True(ship.Shield <= ship.MaxShield);
        Assert.Equal(40, ship.Shield); // 30 + 20 封顶 40
    }
}
