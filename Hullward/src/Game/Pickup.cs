using System;
using Godot;
using Hullward.Domain.Loot;

namespace Hullward.Game;

/// <summary>
/// 掉落物（表现层）：模块（品质色）/合金（金色）方块。
/// 玩家接近即拾取（通知 Main 入背包/装配；背包系统已接入）。
/// </summary>
public partial class Pickup : Node2D
{
    public enum PickupKind
    {
        Module,
        Alloy
    }

    public PickupKind Kind { get; private set; }
    public string Label { get; private set; } = "";
    public Color Tint { get; private set; }
    public ModuleDrop? ModuleData { get; private set; }
    public PlayerShip? Player { get; set; }
    public event Action<Pickup>? Collected;

    private const float PickupRadius = 22f;
    private float _bobPhase;

    public static Pickup CreateModule(ModuleDrop drop, Color color)
    {
        var pickup = new Pickup
        {
            Kind = PickupKind.Module,
            Label = drop.Name,
            Tint = color,
            ModuleData = drop
        };
        return pickup;
    }

    public static Pickup CreateAlloy(int amount)
    {
        var pickup = new Pickup
        {
            Kind = PickupKind.Alloy,
            Label = $"合金×{amount}",
            Tint = new Color("ffd166")
        };
        return pickup;
    }

    public override void _Ready()
    {
        // 品质色边 + 深色底：模块/合金方块
        var frame = new ColorRect
        {
            Size = new Vector2(16, 16),
            Color = Tint,
            Position = new Vector2(-8, -8)
        };
        AddChild(frame);

        var inner = new ColorRect
        {
            Size = new Vector2(8, 8),
            Color = new Color("1a1a2e"),
            Position = new Vector2(-4, -4)
        };
        AddChild(inner);
    }

    public override void _PhysicsProcess(double delta)
    {
        // 轻微漂浮
        _bobPhase += (float)delta * 3f;
        Position = new Vector2(Position.X, Position.Y + Mathf.Sin(_bobPhase) * 0.3f);

        if (Player == null)
        {
            return;
        }

        float dx = Player.Position.X - Position.X;
        float dy = Player.Position.Y - Position.Y;
        if (dx * dx + dy * dy < PickupRadius * PickupRadius)
        {
            GD.Print($"拾取: {Label}");
            Collected?.Invoke(this);
            QueueFree();
        }
    }
}
