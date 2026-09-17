using System;
using System.Collections.Generic;

namespace Hullward.Domain.WorldGen;

/// <summary>
/// 星图生成器（UI 规格 v0.2 §4.1）：
/// 平面视图 · 母舰居中 · 节点散点分布（非线性航线）；
/// 数量随机 [MinNodes, MaxNodes]；强度随机 [章节基准 × 0.8, × 1.4]；
/// 章节主线 Boss 节点固定生成 1 个，其余节点随机。
/// </summary>
public sealed class StarMapGenerator
{
    public const int MinNodes = 4;
    public const int MaxNodes = 8;

    /// <summary>章节强度基准（敌人总量参考，来自章节目录）。</summary>
    public static int ChapterBase(int chapter) => ChapterCatalog.Get(chapter).BaseStrength;

    /// <summary>强度 → 危险等级 ★（1-5）。</summary>
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

        // 环形布局：角度均分 + 抖动，半径带内随机；母舰 (0,0) 居中
        const float minRadius = 260f, maxRadius = 430f;
        for (int i = 0; i < count; i++)
        {
            float angle = i * (MathF.PI * 2f / count) + (rng.NextSingle() - 0.5f) * 0.6f;
            float radius = minRadius + rng.NextSingle() * (maxRadius - minRadius);
            float strength = baseStrength * (0.8f + rng.NextSingle() * 0.6f); // [0.8, 1.4]

            bool isBoss = i == 0; // 章节 Boss 节点固定 1 个（首节点）
            nodes.Add(new StarMapNode
            {
                Index = i,
                Type = MissionType.Clear,
                Strength = (int)MathF.Round(strength),
                DangerStars = DangerStars((int)MathF.Round(strength)),
                IsBoss = isBoss,
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
