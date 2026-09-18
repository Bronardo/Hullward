using System;
using System.Collections.Generic;
using Hullward.Domain.Enemies;
using Hullward.Domain.Loot;
using Xunit;

namespace Hullward.Tests.Domain;

/// <summary>
/// 迭代 8 Boss 三阶段（LD Sprint 3 §4.3）域层测试：
/// 阶段状态机切换血量条件 + 三阶段技能调度（冲锋/召唤/点射/湮灭脉冲）+ 频率修正 + Boss 必掉奖励。
/// </summary>
public class BossPhaseTests
{
    // ---------- 阶段状态机：血量阈值 ----------

    [Theory]
    [InlineData(500, BossPhase.Phase1)] // 100%
    [InlineData(400, BossPhase.Phase1)] // 80%
    [InlineData(301, BossPhase.Phase1)] // 60.2% > 60%
    [InlineData(300, BossPhase.Phase2)] // 60% → 阶段 2
    [InlineData(200, BossPhase.Phase2)] // 40%
    [InlineData(151, BossPhase.Phase2)] // 30.2% > 30%
    [InlineData(150, BossPhase.Phase3)] // 30% → 阶段 3
    [InlineData(50, BossPhase.Phase3)]  // 10%
    [InlineData(1, BossPhase.Phase3)]   // 濒死
    public void UpdatePhase_SwitchesByHullThresholds(int hull, BossPhase expected)
    {
        var boss = new GuardianBoss();
        boss.Hull = hull; // 直接控血（TakeHit 含装甲减伤，无法精确）
        Assert.Equal(expected, boss.UpdatePhase());
    }

    [Fact]
    public void UpdatePhase_RaisesPhaseChangedExactlyOncePerThreshold()
    {
        var boss = new GuardianBoss();
        var events = new List<BossPhase>();
        boss.PhaseChanged += phase => events.Add(phase);

        boss.Hull = 300; // → 60% P2
        boss.UpdatePhase();
        boss.UpdatePhase(); // 幂等：不重复触发
        Assert.Equal(new[] { BossPhase.Phase2 }, events);

        boss.Hull = 150; // → 30% P3
        boss.UpdatePhase();
        Assert.Equal(new[] { BossPhase.Phase2, BossPhase.Phase3 }, events);
    }

    // ---------- 技能调度：阶段 1（冲锋 + 召唤 + 点射） ----------

    [Fact]
    public void TickSkills_Phase1_CyclesThroughAllThreeSkills()
    {
        var boss = new GuardianBoss(); // P1
        var seen = new HashSet<BossSkillKind>();
        float t = 0f;
        // 累积 tick 18s（覆盖 3s 点射 / 5s 冲锋 / 14s 召唤冷却）
        while (t < 18f)
        {
            var intent = boss.TickSkills(0.5f, 500f, 0f);
            if (intent.Kind != BossSkillKind.None)
            {
                seen.Add(intent.Kind);
            }
            t += 0.5f;
        }
        Assert.Contains(BossSkillKind.PhaseCharge, seen);
        Assert.Contains(BossSkillKind.SummonScouts, seen);
        Assert.Contains(BossSkillKind.PointFire, seen);
    }

    // ---------- 技能调度：阶段 2（湮灭脉冲 + 频率 +20%） ----------

    [Fact]
    public void TickSkills_Phase2_AddsAnnihilationPulse()
    {
        var boss = new GuardianBoss();
        boss.Hull = 300; // → P2（60%）
        boss.UpdatePhase();
        float t = 0f;
        var seen = new HashSet<BossSkillKind>();
        while (t < 9f) // 脉冲冷却 8s / 1.2 频率 = 6.67s
        {
            var intent = boss.TickSkills(0.5f, 500f, 0f);
            if (intent.Kind != BossSkillKind.None)
            {
                seen.Add(intent.Kind);
            }
            t += 0.5f;
        }
        Assert.Contains(BossSkillKind.AnnihilationPulse, seen);
    }

    [Fact]
    public void TickSkills_Phase2_FiresPulseFasterThanPhase1Baseline()
    {
        // P1 无脉冲；P2 脉冲首见时间 = 8s / 1.2 ≈ 6.67s → 应早于 8s
        var boss = new GuardianBoss();
        boss.Hull = 300;
        boss.UpdatePhase();
        float t = 0f;
        bool saw = false;
        while (t < 8f && !saw)
        {
            if (boss.TickSkills(0.1f, 500f, 0f).Kind == BossSkillKind.AnnihilationPulse)
            {
                saw = true;
            }
            t += 0.1f;
        }
        Assert.True(saw, "P2 湮灭脉冲应早于 8s 出现（频率 +20%）");
    }

    // ---------- 技能调度：阶段 3（狂暴：脉冲双发、冲锋更快） ----------

    [Fact]
    public void TickSkills_Phase3_PulseFiresDoubleBurst()
    {
        var boss = new GuardianBoss();
        boss.Hull = 100; // → 20% P3
        boss.UpdatePhase();
        float t = 0f;
        BossIntent pulse = default;
        bool found = false;
        while (t < 8f && !found)
        {
            var intent = boss.TickSkills(0.1f, 500f, 0f);
            if (intent.Kind == BossSkillKind.AnnihilationPulse)
            {
                pulse = intent;
                found = true;
            }
            t += 0.1f;
        }
        Assert.True(found, "P3 应出现湮灭脉冲");
        Assert.Equal(2, pulse.Payload); // 双发
    }

    [Fact]
    public void TickSkills_Phase3_ChargesMoreFrequentlyThanPhase1()
    {
        // P1 冲锋冷却 5s，P3 冷却 3s/1.6 频率 = 1.875s
        var bossP1 = new GuardianBoss();
        int p1Charges = CountCharges(bossP1, 500f, 0f, 10f);
        var bossP3 = new GuardianBoss();
        bossP3.Hull = 100; // → P3
        bossP3.UpdatePhase();
        int p3Charges = CountCharges(bossP3, 500f, 0f, 10f);
        Assert.True(p3Charges > p1Charges, $"P3 冲锋应更频繁（P1={p1Charges} P3={p3Charges}）");
    }

    private static int CountCharges(GuardianBoss boss, float px, float py, float total)
    {
        int count = 0;
        float t = 0f;
        while (t < total)
        {
            if (boss.TickSkills(0.1f, px, py).Kind == BossSkillKind.PhaseCharge)
            {
                count++;
            }
            t += 0.1f;
        }
        return count;
    }

    // ---------- 相位冲锋：直线冲撞 ----------

    [Fact]
    public void Charge_MovesBossAlongDirectionTowardPlayer()
    {
        var boss = new GuardianBoss { X = 0f, Y = 0f };
        // tick 出冲锋（初始冷却 5s）
        float t = 0f;
        while (t < 6f)
        {
            boss.TickSkills(0.5f, 500f, 0f);
            t += 0.5f;
        }
        Assert.True(boss.IsCharging, "冲锋意图后应进入冲锋状态");

        float startX = boss.X;
        boss.UpdateBehavior(0.5f, 500f, 0f); // 玩家在正右
        Assert.True(boss.X > startX, "冲锋应沿玩家方向直线推进（X 增大）");
        Assert.True(boss.IsCharging, "冲锋持续 1.1s，0.5s 后仍在冲锋");
    }

    [Fact]
    public void Charge_EndsAfterDuration()
    {
        var boss = new GuardianBoss { X = 0f, Y = 0f };
        float t = 0f;
        while (t < 6f)
        {
            boss.TickSkills(0.5f, 500f, 0f);
            t += 0.5f;
        }
        Assert.True(boss.IsCharging);
        // 冲锋持续 1.1s：推进 1.2s 后结束
        float u = 0f;
        while (u < 1.2f)
        {
            boss.UpdateBehavior(0.1f, 500f, 0f);
            u += 0.1f;
        }
        Assert.False(boss.IsCharging);
    }

    // ---------- Boss 必掉奖励（LD §4.3：黄+、暗金 1-3% 受 MF 加成） ----------

    [Fact]
    public void RollBossModule_AlwaysRareOrBetter()
    {
        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            ModuleDrop drop = new LootTable().RollBossModule(4, rng);
            Assert.True(
                drop.Rarity == ItemRarity.Rare || drop.Rarity == ItemRarity.Set || drop.Rarity == ItemRarity.Ancient,
                $"Boss 必掉黄+，实际 {drop.Rarity}");
        }
    }

    [Fact]
    public void RollBossModule_AncientChanceRisesWithMagicFind()
    {
        const int N = 20000;
        var rng0 = new Random(42);
        var rng50 = new Random(43);
        var rng100 = new Random(44);

        int CountAncient(Func<ModuleDrop> roll)
        {
            int c = 0;
            for (int i = 0; i < N; i++)
            {
                if (roll().Rarity == ItemRarity.Ancient)
                {
                    c++;
                }
            }
            return c;
        }

        var table = new LootTable();
        int mf0 = CountAncient(() => table.RollBossModule(4, rng0, 0));
        int mf50 = CountAncient(() => table.RollBossModule(4, rng50, 50));
        int mf100 = CountAncient(() => table.RollBossModule(4, rng100, 100));

        float r0 = mf0 / (float)N, r50 = mf50 / (float)N, r100 = mf100 / (float)N;
        // MF0 ≈ 1%，MF50 ≈ 2%，MF100 封顶 ≈ 3%（容差 ±1.5%）
        Assert.InRange(r0, 0.0f, 0.025f);
        Assert.True(r50 > r0, $"MF50 暗金率应高于 MF0（{r50:P2} vs {r0:P2}）");
        Assert.InRange(r100, 0.02f, 0.045f);
    }
}
