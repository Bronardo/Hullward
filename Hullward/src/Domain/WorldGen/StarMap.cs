using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>mission类型（UI spec v0.2 §4.2）。</summary>
public enum MissionType
{
    /// <summary>清剿：clear全部暗骸（MVP 必做）。</summary>
    Clear
}

/// <summary>starmapnode = 一个可enter的mission关卡。</summary>
public sealed class StarMapNode
{
    public int Index { get; init; }

    public MissionType Type { get; init; }

    /// <summary>node强度（enemy总量参考，sectorbenchmark × random系数）。</summary>
    public int Strength { get; init; }

    /// <summary>危险level ★ 1-5。</summary>
    public int DangerStars { get; init; }

    /// <summary>sector Boss node（fixed生成，非random）。</summary>
    public bool IsBoss { get; init; }

    /// <summary>sector门tab（非空 = 该node是sector门，UI show专属名；Sprint 4 线 B2：终章"collapse禁区 · 遗迹守护"）。</summary>
    public string? GateLabel { get; init; }

    /// <summary>平面position（mothership居medium (0,0)）。</summary>
    public float X { get; init; }

    public float Y { get; init; }
}

/// <summary>一局starmap（enter即消耗一次time，back后整体重random）。</summary>
public sealed class StarMap
{
    public int Chapter { get; init; }

    public int MothershipLevel { get; init; }

    public List<StarMapNode> Nodes { get; init; } = new();
}
