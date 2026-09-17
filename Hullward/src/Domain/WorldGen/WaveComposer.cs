using System;
using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>敌舰种类（波次构成枚举，表现层映射到具体 EnemyShip 子类与配色）。</summary>
public enum EnemyKind
{
    Recon,
    Raider,
    Heavy,
    Gunboat,
    Swarm,
    Boss
}

/// <summary>波次条目：一种敌舰 + 数量。</summary>
public sealed record WaveEntry(EnemyKind Kind, int Count);

/// <summary>
/// 波次构成规则（LD Sprint 3 §4.2：第 2 章起堡垒舰 + 远程炮艇；第 3 章加虫群）。
/// 纯 C# 域层，规则可单测（章节差异化 = 多态 + 配置证据）。
/// </summary>
public static class WaveComposer
{
    /// <summary>按章节/强度/是否 Boss 波生成波次构成。</summary>
    public static IReadOnlyList<WaveEntry> Compose(int zoneLevel, int strength, bool isBoss)
    {
        if (zoneLevel < 1)
        {
            zoneLevel = 1;
        }
        if (strength < 1)
        {
            strength = 1;
        }

        if (isBoss)
        {
            return new[]
            {
                new WaveEntry(EnemyKind.Boss, 1),
                new WaveEntry(EnemyKind.Recon, 2),
                new WaveEntry(EnemyKind.Raider, 2)
            };
        }

        var entries = new List<WaveEntry>
        {
            new(EnemyKind.Recon, Math.Max(1, strength * 4 / 9)),
            new(EnemyKind.Raider, Math.Max(1, strength * 3 / 9))
        };

        // 堡垒舰/炮艇：第 2 章起登场（章节差异化验收点）
        if (zoneLevel >= 2)
        {
            int heavy = Math.Max(0, strength * 2 / 9);
            if (heavy > 0)
            {
                entries.Add(new WaveEntry(EnemyKind.Heavy, heavy));
            }
            entries.Add(new WaveEntry(EnemyKind.Gunboat, Math.Max(1, strength / 4)));
        }

        // 虫群：第 3 章起登场
        if (zoneLevel >= 3)
        {
            entries.Add(new WaveEntry(EnemyKind.Swarm, Math.Max(2, strength / 3)));
        }

        return entries;
    }
}
