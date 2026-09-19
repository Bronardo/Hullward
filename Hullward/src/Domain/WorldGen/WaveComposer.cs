using System;
using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>enemy ship种类（wave构成枚举，presentationmap到具体 EnemyShip 子类与配色）。</summary>
public enum EnemyKind
{
    Recon,
    Raider,
    Heavy,
    Gunboat,
    Swarm,
    Elite,
    Boss
}

/// <summary>waveentry：一种enemy ship + 数量。</summary>
public sealed record WaveEntry(EnemyKind Kind, int Count);

/// <summary>
/// wave构成规则（LD Sprint 3 §4.2：第 2 章起堡垒舰 + gunboat；第 3 章加swarm）。
/// 纯 C# 域层，规则可单测（sector差异化 = polymorphism + config证据）。
/// </summary>
public static class WaveComposer
{
    /// <summary>按sector/强度/是否 Boss 波生成wave构成。</summary>
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
            var bossWave = new List<WaveEntry>
            {
                new(EnemyKind.Boss, 1),
                new(EnemyKind.Recon, 2),
                new(EnemyKind.Raider, 2)
            };
            // Sprint 4 线 B1：第 3 章起 Boss spawns 1 名elite guard（elite）——"遗迹守护"escort感
            if (zoneLevel >= 3)
            {
                bossWave.Insert(1, new WaveEntry(EnemyKind.Elite, 1));
            }
            return bossWave;
        }

        var entries = new List<WaveEntry>
        {
            new(EnemyKind.Recon, Math.Max(1, strength * 4 / 9)),
            new(EnemyKind.Raider, Math.Max(1, strength * 3 / 9))
        };

        // 堡垒舰/gunboat：第 2 章起登场（sector差异化acceptance点）
        if (zoneLevel >= 2)
        {
            int heavy = Math.Max(0, strength * 2 / 9);
            if (heavy > 0)
            {
                entries.Add(new WaveEntry(EnemyKind.Heavy, heavy));
            }
            entries.Add(new WaveEntry(EnemyKind.Gunboat, Math.Max(1, strength / 4)));
        }

        // swarm：第 3 章起登场
        if (zoneLevel >= 3)
        {
            entries.Add(new WaveEntry(EnemyKind.Swarm, Math.Max(2, strength / 3)));
        }

        // elite（elite guard）：第 3 章起登场（highstat压制，数量少而精）
        if (zoneLevel >= 3)
        {
            entries.Add(new WaveEntry(EnemyKind.Elite, Math.Max(1, strength / 6)));
        }

        return entries;
    }
}
