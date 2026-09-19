namespace Hullward.Domain.Enemies;

/// <summary>Boss phase（LD Sprint 3 §4.3：100%–60% / 60%–30% / 30%–0%）。</summary>
public enum BossPhase
{
    Phase1,
    Phase2,
    Phase3
}

/// <summary>Boss skill种类（域层只负责"何时用什么skill"，presentation执rowentity/视觉）。</summary>
public enum BossSkillKind
{
    None,
    PhaseCharge,          // 相位冲锋（直线冲撞，路径持续伤害）
    SummonScouts,         // 召唤 2 艘侦察机（P3 狂暴改召唤突击舰）
    PointFire,            // 点射炮（远程 3 连弹幕）
    AnnihilationPulse     // 湮灭脉冲（范围 AOE，预兆后闪避）
}

/// <summary>
/// Boss skill意图：域层skillschedule器的output。
/// Payload 语义：SummonScouts = spawn数量；PointFire = 连发数（3）；AnnihilationPulse = 双发数（P3=2）。
/// </summary>
public readonly record struct BossIntent(BossSkillKind Kind, int Payload)
{
    public static BossIntent None => new(BossSkillKind.None, 0);
}
