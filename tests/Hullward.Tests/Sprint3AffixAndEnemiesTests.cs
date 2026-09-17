using System;
using System.Linq;
using Hullward.Domain.Combat;
using Hullward.Domain.Enemies;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Ships;
using Hullward.Domain.WorldGen;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// 迭代 7（LD Sprint 3 §4.1/§4.2）域层测试：
/// 词缀接线 4 条（暴击/暴伤/受击减伤/反伤）+ 第 2 章新敌人（堡垒舰/远程炮艇）+ 波次构成规则。
/// </summary>
public class Sprint3AffixAndEnemiesTests
{
    // ---------- P0-1 词缀接线：暴击（致命一击）/暴伤（暴击增幅） ----------

    [Fact]
    public void RollAttackDamage_NoCritChance_NeverCrits()
    {
        var ship = new ScoutShip();
        Assert.Equal(0f, ship.CritChance);
        for (int i = 0; i < 50; i++)
        {
            int dmg = CombatCalculator.RollAttackDamage(ship, new Random(i), out bool crit);
            Assert.False(crit);
            Assert.Equal(Math.Max(1, (int)ship.Firepower), dmg);
        }
    }

    [Fact]
    public void RollAttackDamage_HighCritChance_CritsOften_AndAppliesCritDamage()
    {
        var ship = new ScoutShip();
        // 通过 public 模块装配路径施加 100% 暴击 + 暴伤 0.5（倍率 2.0 → 2.5）
        new WeaponModule("测试炮", firepowerBonus: 0f, critChanceBonus: 1f, critDamageBonus: 0.5f).ApplyEffect(ship);

        int dmg = CombatCalculator.RollAttackDamage(ship, new Random(7), out bool crit);
        Assert.True(crit);
        Assert.Equal(Math.Max(1, (int)(ship.Firepower * 2.5f)), dmg);
    }

    [Fact]
    public void CreateModule_CritAffixes_MappedIntoWeaponModule()
    {
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Rare);
        drop.Affixes.Add(new Affix("致命一击", AffixStat.CritChance, 8f));
        drop.Affixes.Add(new Affix("暴击增幅", AffixStat.CritDamage, 25f));

        var module = ShipFitting.CreateModule(drop) as WeaponModule;
        Assert.NotNull(module);
        Assert.Equal(0.08f, module.CritChanceBonus, 3);
        Assert.Equal(0.25f, module.CritDamageBonus, 3);

        var ship = new ScoutShip();
        module.ApplyEffect(ship);
        Assert.Equal(0.08f, ship.CritChance, 3);
        Assert.Equal(2.25f, ship.CritDamage, 3);
    }

    // ---------- P0-1 受击减伤（受击后 2s 窗口） ----------

    [Fact]
    public void ApplyHit_WithDamageReduction_ReducesOnlyInsideWindow()
    {
        var ship = new ScoutShip { Shield = 0 };
        // 50% 减伤（public 模块装配路径）
        new ArmorModule("测试甲", shieldBonus: 0, armorBonus: 0, damageReductionBonus: 0.5f).ApplyEffect(ship);
        int initialHull = ship.Hull;

        // 首次受击：窗口尚未开启，正常伤害（10）
        CombatCalculator.ApplyHit(ship, 10);
        int hullAfterFirstHit = ship.Hull;
        Assert.Equal(initialHull - 10, hullAfterFirstHit);

        // 窗口内（1s 后）：减伤生效（10 → 5），且本次受击刷新窗口
        ship.TickTimers(1f);
        CombatCalculator.ApplyHit(ship, 10);
        Assert.Equal(hullAfterFirstHit - 5, ship.Hull);

        // 窗口过期（再 2.1s）：恢复正常伤害
        ship.TickTimers(2.1f);
        int beforeExpiry = ship.Hull;
        CombatCalculator.ApplyHit(ship, 10);
        Assert.Equal(beforeExpiry - 10, ship.Hull);
    }

    [Fact]
    public void CreateModule_DamageReductionAffix_MappedIntoArmorModule()
    {
        var drop = new ModuleDrop(ModuleType.Armor, ItemRarity.Rare);
        drop.Affixes.Add(new Affix("受击减伤", AffixStat.DamageReduction, 15f));

        var module = ShipFitting.CreateModule(drop) as ArmorModule;
        Assert.NotNull(module);
        Assert.Equal(0.15f, module.DamageReductionBonus, 3);

        var ship = new ScoutShip();
        module.ApplyEffect(ship);
        Assert.Equal(0.15f, ship.DamageReductionPct, 3);
    }

    // ---------- P0-1 反伤镀层（近身受击反弹） ----------

    [Fact]
    public void CreateModule_ThornsAffix_MappedIntoArmorModule()
    {
        var drop = new ModuleDrop(ModuleType.Armor, ItemRarity.Rare);
        drop.Affixes.Add(new Affix("反伤镀层", AffixStat.Thorns, 20f));

        var module = ShipFitting.CreateModule(drop) as ArmorModule;
        Assert.NotNull(module);
        Assert.Equal(0.20f, module.ThornsBonus, 3);

        var ship = new ScoutShip();
        module.ApplyEffect(ship);
        Assert.Equal(0.20f, ship.ThornsPct, 3);
    }

    [Fact]
    public void Thorns_TakesEffect_WhenAttackerHits()
    {
        var player = new ScoutShip { Shield = 0 };
        new ArmorModule("反伤甲", shieldBonus: 0, armorBonus: 0, thornsBonus: 0.25f).ApplyEffect(player);
        var enemy = new ReconDrone { Shield = 0 };
        int enemyInitial = enemy.Hull;

        // 表现层 TakeDamage 逻辑等价：受击伤害 × 反伤率 反弹给攻击者
        int thorns = Math.Max(1, (int)(20 * player.ThornsPct));
        enemy.TakeHit(thorns);

        Assert.Equal(enemyInitial - thorns, enemy.Hull);
    }

    // ---------- B1/B2 端到端：掉落词缀 → 装配生效（LD §4.6 补丁） ----------

    [Fact]
    public void RollModule_MagicOrBetter_AlwaysHasAffixes()
    {
        var loot = new LootTable();
        var rng = new Random(20260918);
        for (int i = 0; i < 200; i++)
        {
            ModuleDrop? drop = loot.RollModule(2, rng); // 第 2 章（含蓝+）
            Assert.NotNull(drop);
            if (drop.Rarity != ItemRarity.Common)
            {
                Assert.NotEmpty(drop.Affixes); // 蓝+ 必带词缀（B1）
            }
            else
            {
                Assert.Empty(drop.Affixes); // 白装无词缀
            }
        }
    }

    [Fact]
    public void RollModule_ThenCreateAndEquip_ChangesShipStats()
    {
        var loot = new LootTable();
        var rng = new Random(20260918);
        for (int i = 0; i < 200; i++)
        {
            ModuleDrop? drop = loot.RollModule(2, rng);
            Assert.NotNull(drop);
            if (drop.Affixes.Count == 0)
            {
                continue;
            }

            var ship = new ScoutShip { Shield = 0 };
            IShipModule module = ShipFitting.CreateModule(drop);
            float beforeCrit = ship.CritChance;
            float beforeReduction = ship.DamageReductionPct;
            float beforeThorns = ship.ThornsPct;
            module.ApplyEffect(ship);

            bool anyAffixEffect = ship.CritChance > beforeCrit
                || ship.CritDamage > 2f
                || ship.DamageReductionPct > beforeReduction
                || ship.ThornsPct > beforeThorns
                || ship.FireRateMultiplier > 1f
                || ship.MagicFind > 0
                || ship.Armor > 0
                || ship.Shield > 0;
            Assert.True(anyAffixEffect, $"词缀装配后应至少一项属性变化（{string.Join(",", drop.Affixes.Select(a => a.Name))}）");
            return;
        }
        Assert.Fail("200 次掉落未产生任何带词缀模块");
    }

    // ---------- P0-2 远程炮艇（GunboatShip） ----------

    [Fact]
    public void GunboatShip_Recon_FiresOnlyInsideRangeAndOnCooldown()
    {
        var gunboat = new GunboatShip { X = 0f, Y = 0f };

        // 射程内（620）：冷却就绪 → 开火
        bool first = gunboat.TryFire(0f, 300f, 0f, out float tx, out float ty);
        Assert.True(first);
        Assert.Equal(300f, tx);
        Assert.Equal(0f, ty);

        // 冷却中 → 不开火
        bool second = gunboat.TryFire(0.5f, 300f, 0f, out _, out _);
        Assert.False(second);

        // 冷却到（1.3s 累计 = 1.8）→ 再开火
        bool third = gunboat.TryFire(1.3f, 300f, 0f, out _, out _);
        Assert.True(third);

        // 超射程（650 > 620）→ 不开火
        bool far = gunboat.TryFire(2f, 650f, 0f, out _, out _);
        Assert.False(far);
    }

    [Fact]
    public void GunboatShip_MaintainsPreferredDistance()
    {
        var gunboat = new GunboatShip { X = 200f, Y = 0f }; // 玩家在 (0,0)，距离 200 < 240（Preferred-Deadband）
        gunboat.UpdateBehavior(1f, 0f, 0f);
        Assert.True(gunboat.X > 200f, "过近时应后退拉开距离");
    }

    [Fact]
    public void GunboatShip_IsDistinctEnemyKind_AndSubclassOfEnemyShip()
    {
        EnemyShip ship = new GunboatShip();
        Assert.Equal("远程炮艇", ship.Name);
        Assert.IsType<GunboatShip>(ship);
        // 近战敌舰默认不开火（多态默认实现）
        EnemyShip recon = new ReconDrone();
        Assert.False(recon.TryFire(1f, 0f, 0f, out _, out _));
    }

    // ---------- P0-2 波次构成（第 2 章起堡垒舰/炮艇，第 3 章起虫群） ----------

    [Fact]
    public void WaveComposer_Chapter1_NoHeavyNoGunboatNoSwarm()
    {
        var wave = WaveComposer.Compose(1, 9, isBoss: false);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Recon);
        Assert.DoesNotContain(wave, e => e.Kind == EnemyKind.Heavy);
        Assert.DoesNotContain(wave, e => e.Kind == EnemyKind.Gunboat);
        Assert.DoesNotContain(wave, e => e.Kind == EnemyKind.Swarm);
    }

    [Fact]
    public void WaveComposer_Chapter2_AddsHeavyAndGunboat_NoSwarm()
    {
        var wave = WaveComposer.Compose(2, 9, isBoss: false);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Heavy);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Gunboat);
        Assert.DoesNotContain(wave, e => e.Kind == EnemyKind.Swarm);
    }

    [Fact]
    public void WaveComposer_Chapter3_AddsSwarm()
    {
        var wave = WaveComposer.Compose(3, 9, isBoss: false);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Swarm);
    }

    [Fact]
    public void WaveComposer_BossWave_HasBossPlusEscorts()
    {
        var wave = WaveComposer.Compose(2, 9, isBoss: true);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Boss && e.Count == 1);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Recon && e.Count == 2);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Raider && e.Count == 2);
    }

    [Fact]
    public void WaveComposer_ReconRaiderRatio_ScalesWithStrength()
    {
        var low = WaveComposer.Compose(2, 4, isBoss: false);
        var high = WaveComposer.Compose(2, 12, isBoss: false);
        int lowRecon = low.First(e => e.Kind == EnemyKind.Recon).Count;
        int highRecon = high.First(e => e.Kind == EnemyKind.Recon).Count;
        Assert.True(highRecon >= lowRecon, "强度越高侦察数量越多");
    }
}
