using System;
using System.Collections.Generic;
using Godot;

namespace Hullward.Game;

/// <summary>
/// 战斗 HUD（LD v0.6.0 §3 布局）：顶部栏 / 左下雷达+状态条+资源 / 右侧任务面板 / 底部中央 Q·E 技能槽+能量条 / 右下船体信息。
/// 表现层，不持有战斗状态；数据由 Main 每帧推入。
/// </summary>
public partial class HUD : CanvasLayer
{
    // 顶部栏
    private PanelContainer _topBar = null!;
    private Label _taskLabel = null!;
    private Label _waveLabel = null!;

    // 左下
    private RadarView _radar = null!;
    private ProgressBar _shieldBar = null!;
    private Label _shieldText = null!;
    private ProgressBar _hullBar = null!;
    private Label _hullText = null!;
    private Label _resourceLabel = null!;

    // 右侧任务面板
    private PanelContainer _missionPanel = null!;
    private Label _missionLabel = null!;

    // 底部中央技能槽
    private SkillSlot _slotQ = null!;
    private SkillSlot _slotE = null!;
    private ProgressBar _energyBar = null!;
    private Label _energyText = null!;

    // 右下船体
    private Label _shipLabel = null!;

    // 飘字（保留）
    private Label _toast = null!;
    private float _toastTimer;

    private static readonly Color PanelBg = new("151a28");
    private static readonly Color PanelBorder = new("2a3548");

    public override void _Ready()
    {
        BuildTopBar();
        BuildBottomLeft();
        BuildMissionPanel();
        BuildSkillBar();
        BuildShipInfo();
        BuildToast();
    }

    private static StyleBoxFlat PanelBox(float a = 0.85f) => new()
    {
        BgColor = new Color(PanelBg, a),
        BorderColor = PanelBorder,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6
    };

    private void BuildTopBar()
    {
        _topBar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _topBar.OffsetLeft = 8; _topBar.OffsetRight = -8; _topBar.OffsetTop = 8; _topBar.OffsetBottom = -8;
        _topBar.AddThemeStyleboxOverride("panel", PanelBox());

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        _taskLabel = MakeLabel(16, "#d8ecff");
        _taskLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _waveLabel = MakeLabel(13, "#8fa3c8");
        _waveLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _waveLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var pause = MakeLabel(14, "#8fa3c8");
        pause.Text = "▮▮ Esc 暂停";
        row.AddChild(_taskLabel);
        row.AddChild(_waveLabel);
        row.AddChild(pause);
        _topBar.AddChild(row);
        AddChild(_topBar);
    }

    private void BuildBottomLeft()
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        box.OffsetLeft = 12; box.OffsetTop = -240; box.OffsetRight = -8; box.OffsetBottom = -12;
        box.AddThemeConstantOverride("separation", 4);

        _radar = new RadarView { CustomMinimumSize = new Vector2(120, 120), MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddChild(_radar);

        _shieldBar = MakeBar("#4da6ff");
        box.AddChild(_shieldBar);
        _shieldText = MakeLabel(12, "#cfe4ff");
        box.AddChild(_shieldText);

        _hullBar = MakeBar("#ff4d4d");
        box.AddChild(_hullBar);
        _hullText = MakeLabel(12, "#ffd0d0");
        box.AddChild(_hullText);

        _resourceLabel = MakeLabel(12, "#ffe9a8");
        box.AddChild(_resourceLabel);
        AddChild(box);
    }

    private void BuildMissionPanel()
    {
        _missionPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _missionPanel.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        _missionPanel.OffsetLeft = -200; _missionPanel.OffsetRight = -12; _missionPanel.OffsetTop = 60; _missionPanel.OffsetBottom = -300;
        _missionPanel.AddThemeStyleboxOverride("panel", PanelBox());
        _missionLabel = MakeLabel(14, "#ffd166");
        _missionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _missionPanel.AddChild(_missionLabel);
        AddChild(_missionPanel);
    }

    private void BuildSkillBar()
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        row.OffsetLeft = 0; row.OffsetRight = 0; row.OffsetTop = -110; row.OffsetBottom = -12;
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 10);

        _slotQ = new SkillSlot("⚡", "Q", "过载炮") { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(_slotQ);

        var energyCol = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        energyCol.AddThemeConstantOverride("separation", 2);
        _energyBar = MakeBar("#ffd24d", 300, 16);
        energyCol.AddChild(_energyBar);
        _energyText = MakeLabel(12, "#fff0c0");
        _energyText.HorizontalAlignment = HorizontalAlignment.Center;
        energyCol.AddChild(_energyText);
        row.AddChild(energyCol);

        _slotE = new SkillSlot("🛡", "E", "护盾充能") { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(_slotE);
        AddChild(row);
    }

    private void BuildShipInfo()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        panel.OffsetLeft = -240; panel.OffsetRight = -12; panel.OffsetTop = -90; panel.OffsetBottom = -12;
        panel.AddThemeStyleboxOverride("panel", PanelBox());
        _shipLabel = MakeLabel(14, "#d8ecff");
        panel.AddChild(_shipLabel);
        AddChild(panel);
    }

    private void BuildToast()
    {
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

    private static Label MakeLabel(int size, string color)
    {
        var l = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", new Color(color));
        return l;
    }

    private static ProgressBar MakeBar(string color, int w = 200, int h = 12)
    {
        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = 1,
            CustomMinimumSize = new Vector2(w, h),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ShowPercentage = false
        };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("#0d1119") });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(color) });
        return bar;
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

    /// <summary>战斗帧数据推送（迭代 19 替代旧 UpdateStatus 长串）。</summary>
    public void UpdateBattle(BattleHudData d)
    {
        _taskLabel.Text = d.TaskTitle;
        _waveLabel.Text = d.WaveTitle;

        _shieldBar.MaxValue = d.MaxShield;
        _shieldBar.Value = d.Shield;
        _shieldText.Text = $"护盾 {d.Shield}/{d.MaxShield}";

        _hullBar.MaxValue = d.MaxHull;
        _hullBar.Value = d.Hull;
        _hullText.Text = $"耐久 {d.Hull}/{d.MaxHull}";

        _resourceLabel.Text = $"合金 {d.Alloy}　模块 {d.ModulesPicked}";

        _missionLabel.Text = $"剩余敌舰 {d.EnemiesLeft}\n{d.MissionHint}";

        _energyBar.MaxValue = d.MaxEnergy;
        _energyBar.Value = d.Energy;
        _energyText.Text = $"能量 {d.Energy:0}/{d.MaxEnergy:0}";

        _slotQ.SetState(d.SkillQReady, d.SkillQEnough, d.SkillQRemain, d.SkillQCooldown);
        _slotE.SetState(d.SkillEReady, d.SkillEEnough, d.SkillERemain, d.SkillECooldown);

        _shipLabel.Text = $"{d.ShipName}　Lv.{d.ShipLevel}\n火力 {d.Firepower:0} · 攻速 ×{d.FireRate:0.00}";

        _radar.UpdateRadar(d.PlayerPos, d.Hostiles, d.RadarRange);
    }

    /// <summary>旧接口保留兼容（不再使用，顶部长串由 UpdateBattle 取代）。</summary>
    public void UpdateStatus(string text) { }

    /// <summary>屏幕中央浮动提示（3 秒后淡出）。</summary>
    public void ShowToast(string text)
    {
        _toast.Text = text;
        _toast.Modulate = new Color(1f, 1f, 1f, 1f);
        _toastTimer = 3f;
        _toast.ResetSize();
        _toast.Position = new Vector2((GetViewport().GetVisibleRect().Size.X - _toast.Size.X) / 2f, 64f);
    }

    /// <summary>雷达上的敌舰点。</summary>
    public readonly record struct RadarBlip(Vector2 Pos, bool Elite, bool Boss);

    /// <summary>技能槽（64×64，冷却灰化+剩余秒）。</summary>
    private sealed partial class SkillSlot : PanelContainer
    {
        private readonly Label _icon;
        private readonly Label _key;
        private readonly Label _cd;
        private bool _readyPulse;
        private double _t;

        public SkillSlot(string icon, string key, string name)
        {
            CustomMinimumSize = new Vector2(64, 64);
            MouseFilter = MouseFilterEnum.Ignore;
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            box.AddThemeConstantOverride("separation", 0);
            _icon = new Label { Text = icon, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            _icon.AddThemeFontSizeOverride("font_size", 22);
            _key = new Label { Text = key, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            _key.AddThemeFontSizeOverride("font_size", 11);
            _key.AddThemeColorOverride("font_color", new Color("#8fa3c8"));
            _cd = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            _cd.AddThemeFontSizeOverride("font_size", 12);
            box.AddChild(_icon);
            box.AddChild(_key);
            box.AddChild(_cd);
            AddChild(box);
            SetBox(new Color("#2a3550"));
            TooltipText = name;
        }

        private void SetBox(Color border)
        {
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color("#151a28"),
                BorderColor = border,
                BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
                ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 2, ContentMarginBottom = 2
            });
        }

        public override void _Process(double delta)
        {
            if (!_readyPulse) return;
            _t += delta;
            float a = 0.55f + 0.45f * (float)(0.5 + 0.5 * Math.Sin(_t * 4.0));
            var sb = (StyleBoxFlat)GetThemeStylebox("panel");
            sb.BorderColor = new Color(new Color("#ffd24d"), a);
        }

        public void SetState(bool ready, bool enoughEnergy, float remain, float cooldown)
        {
            if (!ready)
            {
                Modulate = new Color(0.5f, 0.5f, 0.55f, 1f);
                _cd.Text = $"{remain:0.0}s";
                _readyPulse = false;
                SetBox(new Color("#555a68"));
            }
            else if (!enoughEnergy)
            {
                Modulate = new Color(0.6f, 0.4f, 0.4f, 1f);
                _cd.Text = "能量";
                _readyPulse = false;
                SetBox(new Color("#ff5555"));
            }
            else
            {
                Modulate = Colors.White;
                _cd.Text = "";
                _readyPulse = true; // 就绪：金边呼吸
            }
        }
    }

    /// <summary>雷达小地图（自绘：中心玩家 + 敌舰点）。</summary>
    private sealed partial class RadarView : Control
    {
        private Vector2 _player;
        private readonly List<RadarBlip> _hostiles = new();
        private float _range = 350f;

        public void UpdateRadar(Vector2 player, List<RadarBlip> hostiles, float range)
        {
            _player = player;
            _hostiles.Clear();
            _hostiles.AddRange(hostiles);
            _range = range;
            QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 c = Size / 2f;
            float r = Mathf.Min(Size.X, Size.Y) / 2f - 4f;
            DrawCircle(c, r, new Color(1f, 1f, 1f, 0.05f));
            DrawArc(c, r, 0, Mathf.Tau, 64, new Color("3a4868"), 1.5f);
            DrawLine(c - new Vector2(r, 0), c + new Vector2(r, 0), new Color(1f, 1f, 1f, 0.08f), 1f);
            DrawLine(c - new Vector2(0, r), c + new Vector2(0, r), new Color(1f, 1f, 1f, 0.08f), 1f);

            // 玩家中心（白色三角/点）
            DrawCircle(c, 3.5f, Colors.White);

            foreach (var h in _hostiles)
            {
                Vector2 rel = (h.Pos - _player) / _range * (r - 8f);
                rel = new Vector2(Mathf.Clamp(rel.X, -r + 8f, r - 8f), Mathf.Clamp(rel.Y, -r + 8f, r - 8f));
                Vector2 p = c + rel;
                Color col = h.Boss ? Colors.Red : (h.Elite ? new Color("ff9a9a") : new Color("ff5555"));
                float pr = h.Boss ? 4.5f : (h.Elite ? 3f : 2f);
                DrawCircle(p, pr, col);
            }
        }
    }
}

/// <summary>战斗 HUD 帧快照（Main → HUD）。</summary>
public sealed class BattleHudData
{
    public string TaskTitle = "";
    public string WaveTitle = "";
    public int Shield, MaxShield, Hull, MaxHull;
    public float Energy, MaxEnergy;
    public int Alloy, ModulesPicked;
    public int EnemiesLeft;
    public string MissionHint = "";
    public bool SkillQReady, SkillQEnough; public float SkillQRemain, SkillQCooldown;
    public bool SkillEReady, SkillEEnough; public float SkillERemain, SkillECooldown;
    public string ShipName = ""; public int ShipLevel;
    public float Firepower, FireRate;
    public Vector2 PlayerPos;
    public List<HUD.RadarBlip> Hostiles = new();
    public float RadarRange = 350f;
}
