using Godot;

namespace Hullward;

/// <summary>
/// Hullward entry node. Placeholder bootstrap for Day 1 scaffold.
/// Domain layer (combat, loot, economy, save) will live in pure C# classes,
/// Godot nodes only bridge rendering and input.
/// </summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        GD.Print("Hullward bootstrap OK - Godot C# pipeline ready");
    }
}
