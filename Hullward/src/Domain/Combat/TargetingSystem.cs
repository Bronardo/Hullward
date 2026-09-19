using System;
using System.Collections.Generic;

namespace Hullward.Domain.Combat;

/// <summary>
/// 主炮auto索敌（拍板项：auto索敌 + manualskill）。
/// targetpriority：可config（default最近优先）。
/// 纯 C# 域层，可单测。
/// </summary>
public sealed class TargetingSystem
{
    public enum TargetPriority
    {
        Nearest,   // 最近优先（default）
        LowestHull // 最lowhull优先
    }

    public TargetPriority Priority { get; set; } = TargetPriority.Nearest;

    /// <summary>从候选targetmedium选出currentlock定target；无候选back null。</summary>
    public ITargetable? Acquire(IReadOnlyList<ITargetable> candidates, float sourceX, float sourceY)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        ITargetable? best = null;
        float bestScore = float.MaxValue;

        foreach (var candidate in candidates)
        {
            if (candidate.Hull <= 0)
            {
                continue; // 已击毁target不索敌
            }

            float score = Priority switch
            {
                TargetPriority.Nearest => DistanceSquared(sourceX, sourceY, candidate),
                TargetPriority.LowestHull => candidate.Hull,
                _ => DistanceSquared(sourceX, sourceY, candidate)
            };

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static float DistanceSquared(float x, float y, ITargetable target)
    {
        float dx = target.X - x;
        float dy = target.Y - y;
        return dx * dx + dy * dy;
    }
}

/// <summary>可被索敌/受击的entity（enemy、可破坏物）。</summary>
public interface ITargetable
{
    float X { get; }
    float Y { get; }
    int Hull { get; }
    void TakeHit(int damage);
}
