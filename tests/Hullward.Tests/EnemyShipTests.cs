using Hullward.Domain.Enemies;
using Xunit;

namespace Hullward.Tests.Domain;

public class EnemyShipTests
{
    private const float PlayerX = 0f;
    private const float PlayerY = 0f;

    [Fact]
    public void ReconDrone_ChasesPlayer_WhenInAggroRange()
    {
        var drone = new ReconDrone { X = 200f, Y = 0f };
        float before = drone.X;

        for (int i = 0; i < 60; i++)
        {
            drone.UpdateBehavior(1f / 60f, PlayerX, PlayerY);
        }

        Assert.True(drone.X < before, "侦察机应朝玩家逼近");
    }

    [Fact]
    public void ReconDrone_StaysIdle_WhenOutOfAggroRange()
    {
        var drone = new ReconDrone { X = 900f, Y = 900f };
        drone.UpdateBehavior(1f, PlayerX, PlayerY);

        Assert.Equal(900f, drone.X);
        Assert.Equal(900f, drone.Y);
    }

    [Fact]
    public void RaiderShip_KeepsOrbitBand_AroundPlayer()
    {
        var raider = new RaiderShip { X = 400f, Y = 0f };

        for (int i = 0; i < 600; i++)
        {
            raider.UpdateBehavior(1f / 60f, PlayerX, PlayerY);
        }

        float dist = System.MathF.Sqrt(raider.X * raider.X + raider.Y * raider.Y);
        Assert.InRange(dist, 200f, 400f);
    }

    [Fact]
    public void HeavyFortress_MovesSlowerThanReconDrone()
    {
        var fortress = new HeavyFortress { X = 300f, Y = 0f };
        var drone = new ReconDrone { X = 300f, Y = 0f };

        for (int i = 0; i < 60; i++)
        {
            fortress.UpdateBehavior(1f / 60f, PlayerX, PlayerY);
            drone.UpdateBehavior(1f / 60f, PlayerX, PlayerY);
        }

        float fortressMoved = 300f - fortress.X;
        float droneMoved = 300f - drone.X;
        Assert.True(fortressMoved < droneMoved, "堡垒应比侦察机慢");
    }

    [Fact]
    public void TakeHit_ReducesHull_AndDestroyedAtZero()
    {
        var drone = new ReconDrone { X = 0f, Y = 0f };
        Assert.Equal(30, drone.Hull);

        drone.TakeHit(30);

        Assert.Equal(0, drone.Hull);
        Assert.True(drone.IsDestroyed);
    }

    [Fact]
    public void TakeHit_ShieldAbsorbsBeforeHull()
    {
        var raider = new RaiderShip { X = 0f, Y = 0f };
        Assert.Equal(20, raider.Shield);

        raider.TakeHit(15);

        Assert.Equal(5, raider.Shield);
        Assert.Equal(70, raider.Hull);
    }

    [Fact]
    public void Position_ClampsToWorldBounds()
    {
        // 敌舰在界外、玩家在附近：追击移动后应被 Clamp 回世界边界内
        var drone = new ReconDrone { X = 5000f, Y = 0f };
        drone.UpdateBehavior(1f, 5000f, 100f);

        Assert.Equal(950f, drone.X); // Clamp 到 X 上限
        Assert.InRange(drone.Y, -530f, 530f);
    }

    [Fact]
    public void GuardianBoss_HasHighHull_AndFullMapAggro()
    {
        var boss = new GuardianBoss { X = 800f, Y = 0f };

        // 全图索敌：远处也会逼近
        boss.UpdateBehavior(1f, 0f, 0f);
        Assert.True(boss.X < 800f, "Boss 应全图索敌并逼近玩家");

        Assert.Equal(500, boss.Hull);
        Assert.True(boss.Hull > new HeavyFortress().Hull);
    }

    [Fact]
    public void ScaleForZone_HigherZone_Stronger()
    {
        var zone1 = new ReconDrone();
        var zone4 = new ReconDrone();
        zone1.ScaleForZone(1);
        zone4.ScaleForZone(4);

        Assert.Equal(zone1.Hull * 2 + zone1.Hull / 2, zone4.Hull); // 2.5×
        Assert.True(zone4.Firepower > zone1.Firepower);
    }
}
