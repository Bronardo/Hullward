using Hullward.Domain.Combat;
using Xunit;

namespace Hullward.Tests.Domain;

public class CombatCalculatorTests
{
    private sealed class TestShip : IShip
    {
        public string Name { get; } = "测试舰";
        public int Hull { get; set; }
        public int Shield { get; set; }
        public int Armor { get; }

        public TestShip(int hull, int shield, int armor)
        {
            Hull = hull;
            Shield = shield;
            Armor = armor;
        }
    }

    [Fact]
    public void CalculateDamage_Basic_ReturnsFirepower()
    {
        Assert.Equal(10, CombatCalculator.CalculateDamage(10));
    }

    [Fact]
    public void CalculateDamage_Minimum_IsOne()
    {
        Assert.Equal(1, CombatCalculator.CalculateDamage(1, 1f, armorReduction: 99));
    }

    [Fact]
    public void CalculateDamage_AppliesSkillMultiplier()
    {
        Assert.Equal(15, CombatCalculator.CalculateDamage(10, skillMultiplier: 1.5f));
    }

    [Fact]
    public void CalculateDamage_AppliesArmorReduction()
    {
        Assert.Equal(7, CombatCalculator.CalculateDamage(10, 1f, armorReduction: 3));
    }

    [Fact]
    public void ApplyHit_ShieldAbsorbsFirst()
    {
        var ship = new TestShip(hull: 100, shield: 30, armor: 0);
        CombatCalculator.ApplyHit(ship, 20);

        Assert.Equal(10, ship.Shield); // 30 - 20，全部由护盾吸收
        Assert.Equal(100, ship.Hull);
    }

    [Fact]
    public void ApplyHit_OverflowDamagesHull()
    {
        var ship = new TestShip(hull: 100, shield: 10, armor: 0);
        CombatCalculator.ApplyHit(ship, 30);

        Assert.Equal(0, ship.Shield);
        Assert.Equal(80, ship.Hull);
    }

    [Fact]
    public void ApplyHit_HullNeverGoesNegative()
    {
        var ship = new TestShip(hull: 5, shield: 0, armor: 0);
        CombatCalculator.ApplyHit(ship, 50);

        Assert.Equal(0, ship.Hull);
    }
}
