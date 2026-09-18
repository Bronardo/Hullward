using System;
using Hullward.Domain.Combat;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// Sprint 4 线 C1（迭代 14）测试：能量条系统（LD Sprint4 §3.2 拍板）——
/// 上限 100 初始满、战斗中 8/s / 脱战 12/s 回复、Q 耗 30 / E 耗 40、
/// 能源扩容/快速充能/节能模块/冷却缩减四条词缀接线、能量不足拒绝施放。
/// </summary>
public class Sprint4EnergySystemTests
{
    // ---------- 基础：上限 / 初始满 / 消耗 ----------

    [Fact]
    public void Energy_StartsWithFullCapacity()
    {
        var ship = new ScoutShip();
        Assert.Equal(100f, ship.MaxEnergy);
        Assert.Equal(100f, ship.Energy);
    }

    [Fact]
    public void QConsumes_30Energy_WhenCast()
    {
        var ship = new ScoutShip();
        var enemy = new TestTarget();
        var q = new OverdriveCannon();
        Assert.True(q.TryUse(new PlayerContext { Ship = ship, LockedTarget = enemy }));
        Assert.Equal(70f, ship.Energy);
    }

    [Fact]
    public void EConsumes_40Energy_WhenCast()
    {
        var ship = new ScoutShip { Shield = 0 };
        var e = new ShieldBurst();
        Assert.True(e.TryUse(new PlayerContext { Ship = ship }));
        Assert.Equal(60f, ship.Energy);
    }

    [Fact]
    public void SpendEnergy_NeverGoesBelowZero()
    {
        var ship = new ScoutShip();
        ship.SpendEnergy(500f);
        Assert.Equal(0f, ship.Energy);
    }

    // ---------- 能量不足拒绝施放 ----------

    [Fact]
    public void TryUse_RejectsWhenEnergyInsufficient_NoCostNoCooldown()
    {
        var ship = new ScoutShip();
        ship.SpendEnergy(100f); // 空能
        var enemy = new TestTarget();
        var q = new OverdriveCannon();
        Assert.False(q.TryUse(new PlayerContext { Ship = ship, LockedTarget = enemy }));
        Assert.Equal(0f, ship.Energy);          // 未扣费
        Assert.True(q.IsReady);                  // 未进冷却（Remaining 仍 0）
        Assert.Equal(50, enemy.Hull);            // 未生效（满血不变）
    }

    [Fact]
    public void TryUse_RejectsWhenAlmostEmpty_ButAllowsWhenJustEnough()
    {
        var ship = new ScoutShip();
        ship.SpendEnergy(70f); // 剩 30
        var enemy = new TestTarget();
        var q = new OverdriveCannon();
        Assert.True(q.TryUse(new PlayerContext { Ship = ship, LockedTarget = enemy })); // 刚好 30
        Assert.Equal(0f, ship.Energy);
        Assert.False(q.TryUse(new PlayerContext { Ship = ship, LockedTarget = enemy })); // 空能 + 冷却中
    }

    // ---------- 回复：战斗 8/s、脱战 12/s ----------

    [Fact]
    public void Regen_InCombat_8PerSecond()
    {
        var ship = new ScoutShip();
        ship.SpendEnergy(30f);
        ship.RegenEnergy(1f, inCombat: true);
        Assert.Equal(78f, ship.Energy, 3);
    }

    [Fact]
    public void Regen_OutOfCombat_12PerSecond()
    {
        var ship = new ScoutShip();
        ship.SpendEnergy(30f);
        ship.RegenEnergy(1f, inCombat: false);
        Assert.Equal(82f, ship.Energy, 3);
    }

    [Fact]
    public void Regen_DoesNotExceedCapacity()
    {
        var ship = new ScoutShip();
        ship.RegenEnergy(10f, inCombat: false);
        Assert.Equal(100f, ship.Energy);
    }

    // ---------- 词缀接线（LD §3.2 映射） ----------

    [Fact]
    public void MaxEnergyAffix_ExpandsCapacityAndCurrent()
    {
        var ship = new ScoutShip();
        ship.ResetCombatState();
        ship.EquipModule(new PowerModule("测试", maxEnergyPercent: 10f));
        Assert.Equal(110f, ship.MaxEnergy, 3);
        Assert.Equal(110f, ship.Energy, 3); // 扩容同步抬升
    }

    [Fact]
    public void EnergyRegenAffix_SpeedsUpBothRates()
    {
        var ship = new ScoutShip();
        ship.ResetCombatState();
        ship.EquipModule(new PowerModule("测试", energyRegenPercent: 25f));
        ship.SpendEnergy(30f);
        ship.RegenEnergy(1f, inCombat: true);   // 8 × 1.25 = 10
        Assert.Equal(80f, ship.Energy, 3);
        ship.SpendEnergy(50f);
        ship.RegenEnergy(1f, inCombat: false);  // 12 × 1.25 = 15
        Assert.Equal(45f, ship.Energy, 3);
    }

    [Fact]
    public void SkillCostAffix_ReducesQCost()
    {
        var ship = new ScoutShip();
        ship.ResetCombatState();
        ship.EquipModule(new PowerModule("测试", skillCostPercent: 10f)); // 节能 10% → 0.9×
        var enemy = new TestTarget();
        var q = new OverdriveCannon();
        Assert.True(q.TryUse(new PlayerContext { Ship = ship, LockedTarget = enemy }));
        Assert.Equal(100f - 30f * 0.9f, ship.Energy, 3); // 耗 27
    }

    [Fact]
    public void CooldownAffix_ReducesSkillCooldown()
    {
        var ship = new ScoutShip();
        ship.ResetCombatState();
        ship.EquipModule(new PowerModule("测试", cooldownPercent: 10f)); // 冷却缩减 10% → 0.9×
        var enemy = new TestTarget();
        var q = new OverdriveCannon();
        Assert.True(q.TryUse(new PlayerContext { Ship = ship, LockedTarget = enemy }));
        Assert.Equal(6f * 0.9f, q.Remaining, 3); // 6 → 5.4
    }

    [Fact]
    public void CreateModule_PowerDrop_CarriesEnergyAffixes()
    {
        var drop = new ModuleDrop(ModuleType.Power, ItemRarity.Rare);
        drop.Affixes.Add(new Affix("能源扩容", AffixStat.MaxEnergyPercent, 10f));
        drop.Affixes.Add(new Affix("节能模块", AffixStat.SkillCostPercent, 5f));
        var module = ShipFitting.CreateModule(drop);
        var power = Assert.IsType<PowerModule>(module);
        Assert.Equal(10f, power.MaxEnergyPercent);
        Assert.Equal(5f, power.SkillCostPercent);
    }

    /// <summary>可索敌目标桩（域层接口实现，供技能锁定目标）。</summary>
    private sealed class TestTarget : ITargetable
    {
        public int Hull { get; set; } = 50;
        public float X { get; set; }
        public float Y { get; set; }
        public void TakeHit(int damage) => Hull = Math.Max(0, Hull - damage);
    }
}
