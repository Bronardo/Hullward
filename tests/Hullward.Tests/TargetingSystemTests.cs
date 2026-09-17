using Hullward.Domain.Combat;
using Xunit;

namespace Hullward.Tests.Domain;

public class TargetingSystemTests
{
    private sealed class Target : ITargetable
    {
        public float X { get; }
        public float Y { get; }
        public int Hull { get; set; }

        public Target(float x, float y, int hull = 100)
        {
            X = x;
            Y = y;
            Hull = hull;
        }

        public void TakeHit(int damage) => Hull = Math.Max(0, Hull - damage);
    }

    [Fact]
    public void Acquire_EmptyList_ReturnsNull()
    {
        var system = new TargetingSystem();
        Assert.Null(system.Acquire([], 0, 0));
    }

    [Fact]
    public void Acquire_NearestPriority_SelectsClosest()
    {
        var system = new TargetingSystem();
        var far = new Target(100, 0);
        var near = new Target(10, 0);

        var picked = system.Acquire([far, near], 0, 0);

        Assert.Same(near, picked);
    }

    [Fact]
    public void Acquire_SkipsDestroyedTargets()
    {
        var system = new TargetingSystem();
        var destroyed = new Target(1, 0, hull: 0);
        var alive = new Target(50, 0);

        var picked = system.Acquire([destroyed, alive], 0, 0);

        Assert.Same(alive, picked);
    }

    [Fact]
    public void Acquire_LowestHullPriority_SelectsWeakest()
    {
        var system = new TargetingSystem { Priority = TargetingSystem.TargetPriority.LowestHull };
        var weak = new Target(50, 0, hull: 10);
        var strong = new Target(10, 0, hull: 90);

        var picked = system.Acquire([strong, weak], 0, 0);

        Assert.Same(weak, picked);
    }

    [Fact]
    public void Acquire_AllDestroyed_ReturnsNull()
    {
        var system = new TargetingSystem();
        Assert.Null(system.Acquire([new Target(1, 0, hull: 0)], 0, 0));
    }
}
