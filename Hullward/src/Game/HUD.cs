using Godot;

namespace Hullward.Game;

/// <summary>
/// 顶部状态 HUD（表现层）：星域 / 耐久 / 护盾 / 技能冷却 / 资源 / 操作提示。
/// 拾取/事件浮动提示（LD Sprint 3 §4.6 B4：掉落反馈可感知）。
/// </summary>
public partial class HUD : CanvasLayer
{
    private Label _status = null!;
    private Label _toast = null!;
    private float _toastTimer;

    public override void _Ready()
    {
        _status = new Label
        {
            Position = new Vector2(12, 8)
        };
        _status.AddThemeFontSizeOverride("font_size", 15);
        _status.AddThemeColorOverride("font_color", new Color("d8ecff"));
        AddChild(_status);

        _toast = new Label
        {
            Position = new Vector2(0, 64),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
        _toast.AddThemeFontSizeOverride("font_size", 17);
        _toast.AddThemeColorOverride("font_color", new Color("ffd166"));
        AddChild(_toast);
    }

    public override void _Process(double delta)
    {
        if (_toastTimer > 0f)
        {
            _toastTimer -= (float)delta;
            if (_toastTimer <= 0f)
            {
                _toast.Modulate = new Color(1f, 1f, 1f, 0f);
            }
        }
    }

    public void UpdateStatus(string text) => _status.Text = text;

    /// <summary>屏幕中央浮动提示（3 秒后淡出），用于掉落反馈等。</summary>
    public void ShowToast(string text)
    {
        _toast.Text = text;
        _toast.Modulate = new Color(1f, 1f, 1f, 1f);
        _toastTimer = 3f;
        // 水平居中：按文本估算宽度（等宽近似）
        _toast.ResetSize();
        _toast.Position = new Vector2((GetViewport().GetVisibleRect().Size.X - _toast.Size.X) / 2f, 64f);
    }
}
