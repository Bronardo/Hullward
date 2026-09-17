using Godot;

namespace Hullward.Game;

/// <summary>
/// 顶部状态 HUD（表现层）：星域 / 耐久 / 护盾 / 技能冷却 / 资源 / 操作提示。
/// </summary>
public partial class HUD : CanvasLayer
{
    private Label _status = null!;

    public override void _Ready()
    {
        _status = new Label
        {
            Position = new Vector2(12, 8)
        };
        _status.AddThemeFontSizeOverride("font_size", 15);
        _status.AddThemeColorOverride("font_color", new Color("d8ecff"));
        AddChild(_status);
    }

    public void UpdateStatus(string text) => _status.Text = text;
}
