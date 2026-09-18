using System;
using System.Linq;
using Hullward.Domain.Enemies;
using Hullward.Domain.WorldGen;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// Sprint 4 线 B（迭代 11）测试：第 3 章精英（旗舰护卫）+ 虫群群体冲锋 +
/// 终章章节门 + 四章解锁链路完整性（ULO3：行为规则用单测锁定）。
/// </summary>
public class Sprint4ChapterContentTests
{
    // ---------- B1：精英（旗舰护卫，高数值 + 点射压制） ----------

    [Fact]
    public void EliteGuardShip_IsHighValueSubclassOfEnemyShip()
    {
        var elite = new EliteGuardShip();
        Assert.IsAssignableFrom<EnemyShip>(elite);
        // 高数值：血量/护盾/火力均显著高于远程炮艇（第 2 章远程主力）
        var gunboat = new GunboatShip();
        Assert.True(elite.MaxHull > gunboat.MaxHull);
        Assert.True(elite.MaxShield > gunboat.MaxShield);
        Assert.True(elite.Firepower > gunboat.Firepower);
        Assert.Equal("旗舰护卫", elite.Name);
    }

    [Fact]
    public void EliteGuardShip_FiresInsideRange_OnCooldownOnly()
    {
        var elite = new EliteGuardShip { X = 0, Y = 0 };
        // 玩家在射程内（700）且冷却为 0 → 开火
        Assert.True(elite.TryFire(0.1f, 500, 0, out _, out _));
        // 开火后冷却 1.5s：小步进不放行
        Assert.False(elite.TryFire(0.1f, 500, 0, out _, out _));
        // 冷却结束：在射程外（900>700）推进 1.5s 只减冷却不开火
        float acc = 0f;
        while (acc < 1.5f) { elite.TryFire(0.1f, 900, 0, out _, out _); acc += 0.1f; }
        Assert.True(elite.TryFire(0.1f, 500, 0, out _, out _)); // 冷却 0 且射程内 → 开火
    }

    [Fact]
    public void EliteGuardShip_DoesNotFire_BeyondRange()
    {
        var elite = new EliteGuardShip { X = 0, Y = 0 };
        Assert.False(elite.TryFire(0.1f, 900, 0, out _, out _)); // 900 > 700 射程
    }

    [Fact]
    public void EliteGuardShip_PressesIn_WhenFar_WithinAggro()
    {
        var elite = new EliteGuardShip { X = 500, Y = 0 }; // 距玩家 500（<800 aggro，>360 保持带）
        float before = MathF.Sqrt(elite.X * elite.X + elite.Y * elite.Y);
        elite.UpdateBehavior(0.2f, 0, 0);
        float after = MathF.Sqrt(elite.X * elite.X + elite.Y * elite.Y);
        Assert.True(after < before); // 远时逼近
    }

    // ---------- B1：虫群群体冲锋（个体相位错开 + 周期性直冲） ----------

    [Fact]
    public void SwarmDrone_EntersCharge_AfterIndividualCooldown()
    {
        var swarm = new SwarmDrone(new Random(7)) { X = 0, Y = 0 };
        Assert.False(swarm.IsCharging);
        // 玩家在 aggro 内（100 距离），推进直到触发冲锋（冷却 2.5-4.5s）
        float elapsed = 0f;
        while (!swarm.IsCharging && elapsed < 5f)
        {
            swarm.UpdateBehavior(0.1f, 100, 0);
            elapsed += 0.1f;
        }
        Assert.True(swarm.IsCharging);
        Assert.InRange(elapsed, 2.4f, 4.7f); // 冷却区间内触发
        Assert.True(swarm.ChargeCountdown >= 2.5f); // 触发后冷却已重置
    }

    [Fact]
    public void SwarmDrone_ChargeEnds_AfterDuration_AndRecoils()
    {
        var swarm = new SwarmDrone(new Random(7)) { X = 0, Y = 0 };
        float elapsed = 0f;
        while (!swarm.IsCharging && elapsed < 5f)
        {
            swarm.UpdateBehavior(0.1f, 100, 0);
            elapsed += 0.1f;
        }
        Assert.True(swarm.IsCharging);
        // 冲锋中直冲（速度 2.4×260=624/s）→ 0.2s 位移显著大于蛇形速度（260/s）
        float x0 = swarm.X;
        swarm.UpdateBehavior(0.2f, 100, 0);
        float chargeMove = MathF.Abs(swarm.X - x0);
        Assert.True(chargeMove > 90f); // 624*0.2 ≈ 125
        // 冲锋持续 0.7s 后退出
        float t = 0.2f;
        while (swarm.IsCharging && t < 1.5f)
        {
            swarm.UpdateBehavior(0.1f, 100, 0);
            t += 0.1f;
        }
        Assert.False(swarm.IsCharging);
        // 退出后恢复蛇形（非冲锋速度）
        float x1 = swarm.X;
        swarm.UpdateBehavior(0.2f, 100, 0);
        Assert.True(MathF.Abs(swarm.X - x1) < 90f);
    }

    [Fact]
    public void SwarmDrone_IndividualPhases_AreStaggered()
    {
        var a = new SwarmDrone(new Random(1)) { X = 0, Y = 0 };
        var b = new SwarmDrone(new Random(2)) { X = 0, Y = 0 };
        Assert.NotEqual(a.ChargeCountdown, b.ChargeCountdown); // 不同 seed → 相位错开
    }

    // ---------- B1/B3：四章敌人差异化完整表 ----------

    [Theory]
    [InlineData(1, false, false, false, false)] // 第 1 章：无 Heavy/Gunboat/Swarm/Elite
    [InlineData(2, true, true, false, false)]  // 第 2 章：+Heavy/Gunboat
    [InlineData(3, true, true, true, true)]    // 第 3 章：+Swarm/Elite
    [InlineData(4, true, true, true, true)]    // 第 4 章：全量
    public void WaveComposer_ChapterDifferentiation(int zone, bool heavy, bool gunboat, bool swarm, bool elite)
    {
        var wave = WaveComposer.Compose(zone, 9, isBoss: false);
        Assert.Equal(heavy, wave.Any(e => e.Kind == EnemyKind.Heavy));
        Assert.Equal(gunboat, wave.Any(e => e.Kind == EnemyKind.Gunboat));
        Assert.Equal(swarm, wave.Any(e => e.Kind == EnemyKind.Swarm));
        Assert.Equal(elite, wave.Any(e => e.Kind == EnemyKind.Elite));
        Assert.Contains(wave, e => e.Kind == EnemyKind.Recon);
        Assert.Contains(wave, e => e.Kind == EnemyKind.Raider);
    }

    [Fact]
    public void WaveComposer_EliteCount_ScalesWithStrength()
    {
        var low = WaveComposer.Compose(3, 6, false);
        var high = WaveComposer.Compose(3, 24, false);
        int lowElite = low.First(e => e.Kind == EnemyKind.Elite).Count;
        int highElite = high.First(e => e.Kind == EnemyKind.Elite).Count;
        Assert.True(highElite >= lowElite);
        Assert.True(lowElite >= 1 && highElite >= 1);
    }

    [Fact]
    public void WaveComposer_BossWave_Chapter3Plus_HasEliteGuard()
    {
        var ch2 = WaveComposer.Compose(2, 9, isBoss: true);
        Assert.Contains(ch2, e => e.Kind == EnemyKind.Boss && e.Count == 1);
        Assert.DoesNotContain(ch2, e => e.Kind == EnemyKind.Elite); // 第 2 章 Boss 无护卫

        var ch3 = WaveComposer.Compose(3, 9, isBoss: true);
        Assert.Contains(ch3, e => e.Kind == EnemyKind.Boss && e.Count == 1);
        Assert.Contains(ch3, e => e.Kind == EnemyKind.Elite && e.Count == 1); // 第 3 章+ Boss 带 1 精英
    }

    // ---------- B2：终章章节门（坍缩禁区 · 遗迹守护） ----------

    [Fact]
    public void StarMap_Chapter4_BossNode_IsCollapseGate()
    {
        var map = new StarMapGenerator().Generate(4, 4, new Random(42));
        var boss = map.Nodes.First(n => n.IsBoss);
        Assert.Equal("坍缩禁区 · 遗迹守护", boss.GateLabel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StarMap_EarlierChapters_BossNode_HasNoGateLabel(int chapter)
    {
        var map = new StarMapGenerator().Generate(chapter, chapter, new Random(42));
        var boss = map.Nodes.First(n => n.IsBoss);
        Assert.Null(boss.GateLabel); // 章节门专属名仅终章
    }

    // ---------- B3：四章解锁链路完整性 ----------

    [Fact]
    public void ChapterUnlock_Chain_AllFour_Reachable()
    {
        // 章节随母舰等级解锁且强度单调递进（无死路：每章打完 Boss 得 2 经验 → 升级进下一章）
        for (int chapter = 1; chapter <= ChapterCatalog.MaxChapter; chapter++)
        {
            Assert.True(ChapterCatalog.IsUnlocked(chapter, chapter), $"第 {chapter} 章应随 Lv{chapter} 解锁");
            Assert.True(ChapterCatalog.Get(chapter).HasBoss);
        }
        // 强度单调（B3 基准强度校验：章节递进 = 敌人总量递进）
        for (int i = 1; i < ChapterCatalog.MaxChapter; i++)
        {
            Assert.True(ChapterCatalog.Get(i + 1).BaseStrength > ChapterCatalog.Get(i).BaseStrength);
        }
    }

    [Fact]
    public void ChapterUnlock_NotReachable_BeforeLevel()
    {
        Assert.False(ChapterCatalog.IsUnlocked(2, 3)); // Lv2 不可进第 3 章
        Assert.False(ChapterCatalog.IsUnlocked(3, 4)); // Lv3 不可进终章
    }
}
