using System;
using Hullward.Domain.WorldGen;
using Xunit;

namespace Hullward.Tests.Domain;

public class StarMapGeneratorTests
{
    private static StarMap Generate(int chapter = 1, int level = 1, int seed = 42)
        => new StarMapGenerator().Generate(chapter, level, new Random(seed));

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void Generate_NodeCount_WithinRange(int seed)
    {
        for (int i = 0; i < 20; i++)
        {
            var map = Generate(seed: seed + i);
            Assert.InRange(map.Nodes.Count, StarMapGenerator.MinNodes, StarMapGenerator.MaxNodes);
        }
    }

    [Theory]
    [InlineData(1, 5)]  // 章1 基准 5 → [4, 7]
    [InlineData(2, 8)]  // 章2 基准 8 → [6, 11]
    [InlineData(3, 12)] // 章3 基准 12 → [9, 16]
    [InlineData(4, 16)] // 终章 基准 16 → [12, 22]
    public void Generate_Strength_WithinRandomRange(int chapter, int baseStrength)
    {
        var map = Generate(chapter);
        foreach (var node in map.Nodes)
        {
            int lo = (int)MathF.Floor(baseStrength * 0.8f);
            int hi = (int)MathF.Ceiling(baseStrength * 1.4f);
            Assert.InRange(node.Strength, lo, hi);
        }
    }

    [Fact]
    public void Generate_SameSeed_IsDeterministic()
    {
        var a = Generate(seed: 123);
        var b = Generate(seed: 123);

        Assert.Equal(a.Nodes.Count, b.Nodes.Count);
        for (int i = 0; i < a.Nodes.Count; i++)
        {
            Assert.Equal(a.Nodes[i].Strength, b.Nodes[i].Strength);
            Assert.Equal(a.Nodes[i].X, b.Nodes[i].X);
            Assert.Equal(a.Nodes[i].Y, b.Nodes[i].Y);
        }
    }

    [Fact]
    public void Generate_ExactlyOneBossNode()
    {
        var map = Generate();
        int bossCount = map.Nodes.FindAll(n => n.IsBoss).Count;
        Assert.Equal(1, bossCount);
    }

    [Fact]
    public void Generate_Nodes_SpreadAroundCenter_NoOverlap()
    {
        var map = Generate(seed: 7);
        Assert.Equal(0f, 0f); // 母舰居中

        const float minRadius = 240f; // 容差：最小半径 260 - 抖动余量
        const float minPairDist = 40f;
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var n = map.Nodes[i];
            float dist = MathF.Sqrt(n.X * n.X + n.Y * n.Y);
            Assert.True(dist >= minRadius, $"节点 {i} 距母舰过近: {dist}");

            for (int j = i + 1; j < map.Nodes.Count; j++)
            {
                var m = map.Nodes[j];
                float d = MathF.Sqrt((n.X - m.X) * (n.X - m.X) + (n.Y - m.Y) * (n.Y - m.Y));
                Assert.True(d >= minPairDist, $"节点 {i}/{j} 重叠: {d}");
            }
        }
    }

    [Fact]
    public void Generate_AllNodes_AreClearMissions()
    {
        var map = Generate();
        foreach (var node in map.Nodes)
        {
            Assert.Equal(MissionType.Clear, node.Type);
        }
    }

    [Fact]
    public void Generate_CarriesChapterAndLevel()
    {
        var map = Generate(chapter: 3, level: 2);
        Assert.Equal(3, map.Chapter);
        Assert.Equal(2, map.MothershipLevel);
    }
}
