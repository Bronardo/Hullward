using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>任务类型（UI 规格 v0.2 §4.2）。</summary>
public enum MissionType
{
    /// <summary>清剿：消灭全部暗骸（MVP 必做）。</summary>
    Clear
}

/// <summary>星图节点 = 一个可进入的任务关卡。</summary>
public sealed class StarMapNode
{
    public int Index { get; init; }

    public MissionType Type { get; init; }

    /// <summary>节点强度（敌人总量参考，章节基准 × 随机系数）。</summary>
    public int Strength { get; init; }

    /// <summary>危险等级 ★ 1-5。</summary>
    public int DangerStars { get; init; }

    /// <summary>章节 Boss 节点（固定生成，非随机）。</summary>
    public bool IsBoss { get; init; }

    /// <summary>章节门标签（非空 = 该节点是章节门，UI 显示专属名；Sprint 4 线 B2：终章"坍缩禁区 · 遗迹守护"）。</summary>
    public string? GateLabel { get; init; }

    /// <summary>平面坐标（母舰居中 (0,0)）。</summary>
    public float X { get; init; }

    public float Y { get; init; }
}

/// <summary>一局星图（进入即消耗一次时间，返回后整体重随机）。</summary>
public sealed class StarMap
{
    public int Chapter { get; init; }

    public int MothershipLevel { get; init; }

    public List<StarMapNode> Nodes { get; init; } = new();
}
