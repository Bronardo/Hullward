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
            Label = drop.DisplayName,
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
        // Sprint 4 线 A：CC0 掉落图标——模块=蓝色菱形 powerupBlue、合金=金色螺栓 bolt_gold
        var (path, scale) = Kind == PickupKind.Module
            ? ("res://assets/effects/powerupBlue.png", 0.5f)
            : ("res://assets/effects/bolt_gold.png", 0.8f);
        var icon = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(path),
            Scale = new Vector2(scale, scale),
            SelfModulate = Tint,
            Centered = true
        };
        AddChild(icon);
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
            GD.Print($"Picked up: {Label}");
            Collected?.Invoke(this);
            QueueFree();
        }
    }
}
