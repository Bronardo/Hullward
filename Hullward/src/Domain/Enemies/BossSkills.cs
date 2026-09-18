namespace Hullward.Domain.Enemies;

/// <summary>Boss 阶段（LD Sprint 3 §4.3：100%–60% / 60%–30% / 30%–0%）。</summary>
public enum BossPhase
{
    Phase1,
    Phase2,
    Phase3
}

/// <summary>Boss 技能种类（域层只负责"何时用什么技能"，表现层执行实体/视觉）。</summary>
public enum BossSkillKind
{
    None,
    PhaseCharge,          // 相位冲锋（直线冲撞，路径持续伤害）
    SummonScouts,         // 召唤 2 艘侦察机（P3 狂暴改召唤突击舰）
    PointFire,            // 点射炮（远程 3 连弹幕）
    AnnihilationPulse     // 湮灭脉冲（范围 AOE，预兆后闪避）
}

/// <summary>
/// Boss 技能意图：域层技能调度器的输出。
/// Payload 语义：SummonScouts = 召唤数量；PointFire = 连发数（3）；AnnihilationPulse = 双发数（P3=2）。
/// </summary>
public readonly record struct BossIntent(BossSkillKind Kind, int Payload)
{
    public static BossIntent None => new(BossSkillKind.None, 0);
}
