using Hullward.Domain.WorldGen;
using Xunit;

namespace Hullward.Tests.Domain;

public class StarSystemGeneratorTests
{
    [Fact]
    public void Generate_WithCountLessThanFour_Throws()
    {
        var gen = new StarSystemGenerator();
        Assert.Throws<ArgumentOutOfRangeException>(() => gen.Generate(3));
    }

    [Fact]
    public void Generate_ReturnsExactCount()
    {
        var gen = new StarSystemGenerator(new Random(42));
        Assert.Equal(7, gen.Generate(7).Count);
    }

    [Fact]
    public void Generate_FirstSystemIsSafeWithStation()
    {
        var gen = new StarSystemGenerator(new Random(42));
        var systems = gen.Generate(6);

        Assert.Equal(StarSystemType.Safe, systems[0].Type);
        Assert.True(systems[0].HasStation);
    }

    [Fact]
    public void Generate_ExactlyOneCollapseZoneAtEnd()
    {
        var gen = new StarSystemGenerator(new Random(7));
        var systems = gen.Generate(8);

        var zones = systems.Where(s => s.Type == StarSystemType.CollapseZone).ToList();
        Assert.Single(zones);
        Assert.Equal(systems.Count - 1, zones[0].Id);
    }

    [Fact]
    public void Generate_AllSystemsReachable_FromAnyStart()
    {
        var gen = new StarSystemGenerator(new Random(1));
        var systems = gen.Generate(9);

        for (int start = 0; start < systems.Count; start++)
        {
            Assert.True(IsFullyConnected(systems, start), $"从节点 {start} 出发应可达全部");
        }
    }

    [Fact]
    public void Generate_SameSeed_ProducesIdenticalLayout()
    {
        var a = new StarSystemGenerator(new Random(2026)).Generate(6);
        var b = new StarSystemGenerator(new Random(2026)).Generate(6);

        Assert.Equal(
            a.Select(s => (s.Type, s.HasStation, s.HasMiningNode)),
            b.Select(s => (s.Type, s.HasStation, s.HasMiningNode)));
    }

    private static bool IsFullyConnected(List<StarSystem> systems, int start)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(systems[start].Id);
        visited.Add(systems[start].Id);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            var node = systems.First(s => s.Id == current);
            foreach (int next in node.GateLinks)
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return visited.Count == systems.Count;
    }
}
