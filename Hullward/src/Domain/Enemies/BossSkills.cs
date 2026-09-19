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
    PhaseCharge,          // 相位charge（直线冲撞，pathdurationdamage）
    SummonScouts,         // spawn 2 艘recon（P3 enrage改spawnassault ship）
    PointFire,            // burst炮（remote 3 连弹幕）
    AnnihilationPulse     // 湮灭pulse（range AOE，预兆后闪避）
}

/// <summary>
/// Boss skill意图：域层skillschedule器的output。
/// Payload 语义：SummonScouts = spawn数量；PointFire = 连发数（3）；AnnihilationPulse = 双发数（P3=2）。
/// </summary>
public readonly record struct BossIntent(BossSkillKind Kind, int Payload)
{
    public static BossIntent None => new(BossSkillKind.None, 0);
}
