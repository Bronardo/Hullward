using System;
using Hullward.Domain.Combat;
using Hullward.Domain.Ships;

namespace Hullward.Domain.Enemies;

/// <summary>
/// 暗骸敌舰抽象基类（ULO2 多态核心：行为差异由子类 UpdateBehavior 提供，杜绝 if-else 分支）。
/// 位置与耐久均为纯 C# 域数据，表现层节点仅做渲染桥接。
/// </summary>
public abstract class EnemyShip : ShipBase, ITargetable
{
    /// <summary>域层位置（世界坐标，表现层每帧同步到节点）。</summary>
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>行为速度上限（子类定）。</summary>
    protected abstract float BehaviorSpeed { get; }

    /// <summary>索敌触发距离（玩家进入后开始追击/攻击）。</summary>
    protected abstract float AggroRange { get; }

    public const float WorldHalfWidth = 950f;
    public const float WorldHalfHeight = 530f;

    protected EnemyShip(string name, int hull, int shield, int armor, float firepower)
        : base(name, hull, shield, armor, firepower, speed: 0f, moduleSlots: 0)
    {
    }

    /// <summary>多态行为入口：子类实现各自战术。</summary>
    public abstract void UpdateBehavior(float dt, float playerX, float playerY);

    /// <summary>按星域等级缩放强度（Zone4 = 2.5× 耐久 / 1.75× 火力）。</summary>
    public void ScaleForZone(int zoneLevel)
    {
        if (zoneLevel < 1)
        {
            zoneLevel = 1;
        }
        float hullMult = 1f + 0.5f * (zoneLevel - 1);
        float dmgMult = 1f + 0.25f * (zoneLevel - 1);
        Hull = (int)(Hull * hullMult);
        Shield = (int)(Shield * hullMult);
        Firepower *= dmgMult;
    }

    /// <summary>向目标方向移动（子类调用）。</summary>
    protected void MoveToward(float tx, float ty, float speed, float dt)
    {
        float dx = tx - X;
        float dy = ty - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.001f)
        {
            return;
        }
        X += dx / dist * speed * dt;
        Y += dy / dist * speed * dt;
        ClampToWorld();
    }

    protected void ClampToWorld()
    {
        X = Math.Clamp(X, -WorldHalfWidth, WorldHalfWidth);
        Y = Math.Clamp(Y, -WorldHalfHeight, WorldHalfHeight);
    }

    protected static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>侦察机：高速直线追击，轻甲轻火。</summary>
public sealed class ReconDrone : EnemyShip
{
    protected override float BehaviorSpeed => 190f;
    protected override float AggroRange => 600f;

    public ReconDrone()
        : base("侦察机", hull: 30, shield: 0, armor: 0, firepower: 4f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return; // 未进入索敌范围：待机
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}

/// <summary>劫掠舰：保持中距环形游走，中等火力。</summary>
public sealed class RaiderShip : EnemyShip
{
    protected override float BehaviorSpeed => 130f;
    protected override float AggroRange => 700f;

    private const float DesiredDistance = 300f;
    private const float Deadband = 40f;
    private int _orbitDir = 1; // 环形方向

    public RaiderShip()
        : base("劫掠舰", hull: 70, shield: 20, armor: 5, firepower: 8f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        float dist = Distance(X, Y, playerX, playerY);
        if (dist > AggroRange)
        {
            return;
        }

        if (dist > DesiredDistance + Deadband)
        {
            // 太远：逼近
            MoveToward(playerX, playerY, BehaviorSpeed, dt);
        }
        else if (dist < DesiredDistance - Deadband)
        {
            // 太近：后退
            MoveToward(X - (playerX - X), Y - (playerY - Y), BehaviorSpeed, dt);
        }
        else
        {
            // 距离合适：切向环绕
            float tangentX = -(playerY - Y) * _orbitDir;
            float tangentY = (playerX - X) * _orbitDir;
            X += tangentX / dist * BehaviorSpeed * dt;
            Y += tangentY / dist * BehaviorSpeed * dt;
            ClampToWorld();
        }
    }
}

/// <summary>重装堡垒：慢速逼近，重甲重火，正面威胁核心。</summary>
public sealed class HeavyFortress : EnemyShip
{
    protected override float BehaviorSpeed => 55f;
    protected override float AggroRange => 500f;

    public HeavyFortress()
        : base("重装堡垒", hull: 220, shield: 80, armor: 18, firepower: 18f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return;
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}

/// <summary>坍缩禁区守卫（Boss）：全图索敌，重甲重火，慢速碾压。</summary>
public sealed class GuardianBoss : EnemyShip
{
    protected override float BehaviorSpeed => 42f;
    protected override float AggroRange => 950f;

    public GuardianBoss()
        : base("禁区守卫", hull: 500, shield: 180, armor: 25, firepower: 30f)
    {
    }

    public override void UpdateBehavior(float dt, float playerX, float playerY)
    {
        if (Distance(X, Y, playerX, playerY) > AggroRange)
        {
            return;
        }
        MoveToward(playerX, playerY, BehaviorSpeed, dt);
    }
}
