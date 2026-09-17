using System;
using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>星域类型：风险递进，产出与爆船惩罚对应策划 §3.4。</summary>
public enum StarSystemType
{
    Safe,        // 安全星域：白/蓝、少量黄；拾取保留
    Contested,   // 争议星域：稀有/套装主要产出；拾取概率掉落
    DeepVoid,    // 无人深空：黄/绿高概率；拾取概率遗失
    CollapseZone // 坍缩禁区：太古唯一产出；拾取全丢
}

/// <summary>单个星域节点：类型、功能点（空间站/矿点）、跃迁门连接。</summary>
public sealed class StarSystem
{
    public int Id { get; }
    public string Name { get; }
    public StarSystemType Type { get; }
    public bool HasStation { get; set; }
    public bool HasMiningNode { get; set; }
    public List<int> GateLinks { get; } = new();

    public StarSystem(int id, string name, StarSystemType type)
    {
        Id = id;
        Name = name;
        Type = type;
    }
}

/// <summary>
/// 星域生成器（纯 C# 域层，可单测）。
/// 规则：
///  1) 数量至少 4；
///  2) 至少 1 个安全星域且必须带空间站；
///  3) 恰好 1 个坍缩禁区（置于末段）；
///  4) 其余按 安全/争议/深空 分布；
///  5) 跃迁门成环连接，保证全图连通。
/// </summary>
public sealed class StarSystemGenerator
{
    private readonly Random _random;

    public StarSystemGenerator(Random? random = null)
    {
        _random = random ?? new Random();
    }

    public List<StarSystem> Generate(int count)
    {
        if (count < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "星域数量至少为 4");
        }

        var systems = new List<StarSystem>(count);

        // 1) 安全区（首个必为安全区，作为起始站）
        systems.Add(CreateSystem(0, StarSystemType.Safe, hasStation: true));

        // 2) 坍缩禁区固定为最后一个
        int collapseIndex = count - 1;

        // 3) 中间段分布：安全 30% / 争议 45% / 深空 25%
        for (int i = 1; i < collapseIndex; i++)
        {
            double roll = _random.NextDouble();
            StarSystemType type = roll switch
            {
                < 0.30 => StarSystemType.Safe,
                < 0.75 => StarSystemType.Contested,
                _ => StarSystemType.DeepVoid
            };
            bool hasStation = type == StarSystemType.Safe && _random.NextDouble() < 0.4;
            bool hasMining = type is StarSystemType.Contested or StarSystemType.DeepVoid
                             && _random.NextDouble() < 0.5;
            systems.Add(CreateSystem(i, type, hasStation, hasMining));
        }

        // 4) 禁区：无空间站，可带高价值矿点
        systems.Add(CreateSystem(collapseIndex, StarSystemType.CollapseZone,
            hasStation: false, hasMining: _random.NextDouble() < 0.6));

        // 5) 环形连通：i → i+1，末位 → 0
        for (int i = 0; i < systems.Count; i++)
        {
            systems[i].GateLinks.Add(systems[(i + 1) % systems.Count].Id);
        }

        return systems;
    }

    private static StarSystem CreateSystem(
        int id, StarSystemType type, bool hasStation, bool hasMining = false)
    {
        var system = new StarSystem(id, $"{type}星域-{id:00}", type)
        {
            HasStation = hasStation,
            HasMiningNode = hasMining
        };
        return system;
    }
}
