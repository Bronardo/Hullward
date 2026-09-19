using System;
using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>
/// starmapgenerator（UI spec v0.2 §4.1）：
/// 平面view · mothership居medium · node散点distribution（非linearizable航线）；
/// 数量random [MinNodes, MaxNodes]；强度random [sectorbenchmark × 0.8, × 1.4]；
/// sector主线 Boss nodefixed生成 1 个，其余noderandom。
/// </summary>
public sealed class StarMapGenerator
{
    public const int MinNodes = 4;
    public const int MaxNodes = 8;

    /// <summary>sector强度benchmark（enemy总量参考，来自sector目录）。</summary>
    public static int ChapterBase(int chapter) => ChapterCatalog.Get(chapter).BaseStrength;

    /// <summary>强度 → 危险level ★（1-5）。</summary>
    public static int DangerStars(int strength) => strength switch
    {
        <= 5 => 1,
        <= 8 => 2,
        <= 12 => 3,
        <= 16 => 4,
        _ => 5
    };

    public StarMap Generate(int chapter, int mothershipLevel, Random rng)
    {
        int count = rng.Next(MinNodes, MaxNodes + 1);
        float baseStrength = ChapterBase(chapter);
        var nodes = new List<StarMapNode>(count);

        // 环形布局：angle均分 + jitter，半径带内random；mothership (0,0) 居medium
        const float minRadius = 260f, maxRadius = 430f;
        for (int i = 0; i < count; i++)
        {
            float angle = i * (MathF.PI * 2f / count) + (rng.NextSingle() - 0.5f) * 0.6f;
            float radius = minRadius + rng.NextSingle() * (maxRadius - minRadius);
            float strength = baseStrength * (0.8f + rng.NextSingle() * 0.6f); // [0.8, 1.4]

            bool isBoss = i == 0; // sector Boss nodefixed 1 个（首node）
            nodes.Add(new StarMapNode
            {
                Index = i,
                Type = MissionType.Clear,
                Strength = (int)MathF.Round(strength),
                DangerStars = DangerStars((int)MathF.Round(strength)),
                IsBoss = isBoss,
                GateLabel = isBoss && chapter == ChapterCatalog.MaxChapter ? "Collapse Zone: Relic Guardian" : null, // 终章sector门（线 B2）
                X = MathF.Cos(angle) * radius,
                Y = MathF.Sin(angle) * radius
            });
        }

        return new StarMap
        {
            Chapter = chapter,
            MothershipLevel = mothershipLevel,
            Nodes = nodes
        };
    }
}
