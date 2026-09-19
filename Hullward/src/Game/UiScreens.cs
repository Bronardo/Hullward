using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hullward.Domain.Save;
using Hullward.Domain.WorldGen;

namespace Hullward.Game;

/// <summary>
/// UI 屏幕构建器（UI 规格 v0.2）：主菜单 / 命名 / 存档列表 / 星图 / 结算。
/// 全部代码构建 Control 树（像素风基调：深色底 + 高对比文字，美术规范后替换样式）。
/// 居中策略：全屏根 + CenterContainer 真居中（随窗口 resize 自适应）；星图节点锚定窗口中心。
/// </summary>
public static class UiScreens
{
    private const string TitleColor = "7fd4ff";
    private const string SubColor = "9aa7c0";
    private const string TextColor = "e8ecf4";
    private const string PanelColor = "10131f";

    /// <summary>UI 点击音效钩子（Main 在 _Ready 注入 Sfx.PlayClick；未注入时静默）。</summary>
    public static Action? ClickSound { get; set; }

    /// <summary>全屏遮罩容器（每屏复用，调用方负责切换）。</summary>
    public static Control Fullscreen()
    {
        var bg = new ColorRect
        {
            Color = new Color(PanelColor),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return bg;
    }

    /// <summary>全屏居中容器：子内容随窗口尺寸自动居中。</summary>
    private static CenterContainer Centered()
    {
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return center;
    }

    private static VBoxContainer CenterBox(float width = 420)
    {
        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        box.AddThemeConstantOverride("separation", 14);
        box.CustomMinimumSize = new Vector2(width, 0);
        return box;
    }

    private static Label Title(string text)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 46);
        label.AddThemeColorOverride("font_color", new Color(TitleColor));
        return label;
    }

    private static Label Subtitle(string text)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color(SubColor));
        return label;
    }

    private static Label Info(string text, string color = TextColor)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }

    private static Button ActionButton(string text, Action onPressed, Color color)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(360, 52),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AddThemeColorOverride("font_color", new Color("10131f"));
        btn.AddThemeColorOverride("font_hover_color", new Color("10131f"));
        btn.AddThemeColorOverride("font_pressed_color", new Color("10131f"));
        btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = color.Lightened(0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = color.Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        btn.Pressed += onPressed;
        btn.Pressed += () => ClickSound?.Invoke();
        return btn;
    }

    /// <summary>主菜单（规格 §2）。</summary>
    public static Control Menu(Action onNew, Action onContinue, Action onQuit)
    {
        Control root = Fullscreen();
        var box = CenterBox();
        box.AddChild(Title("Hullward"));
        box.AddChild(Subtitle("Echoes of Collapsed Eons"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });
        box.AddChild(ActionButton("New Game", onNew, new Color("4da6ff")));
        box.AddChild(ActionButton("Continue", onContinue, new Color("6ee06e")));
        box.AddChild(ActionButton("Quit", onQuit, new Color("ff6b4a")));
        var center = Centered();
        center.AddChild(box);
        root.AddChild(center);
        return root;
    }

    /// <summary>命名界面（规格 §3：主角名为唯一标识，必填）。</summary>
    public static Control Naming(string error, Action<string> onConfirm, Action onBack)
    {
        Control root = Fullscreen();
        var box = CenterBox();
        box.AddChild(Title("Name Your Commander"));
        box.AddChild(Subtitle("Name is your expedition save ID (max 12 chars, no special chars)"));
        var edit = new LineEdit
        {
            PlaceholderText = "Enter commander name…",
            MaxLength = SaveNameValidator.MaxLength,
            CustomMinimumSize = new Vector2(360, 48),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        edit.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(edit);
        if (!string.IsNullOrEmpty(error))
        {
            box.AddChild(Info($"⚠ {error}", "ff6b4a"));
        }
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        box.AddChild(ActionButton("Confirm & Launch", () => onConfirm(edit.Text), new Color("4da6ff")));
        box.AddChild(ActionButton("Back", onBack, new Color("9aa7c0")));
        var center = Centered();
        center.AddChild(box);
        root.AddChild(center);
        return root;
    }

    /// <summary>存档列表（规格 §3：只显示主角名称）。</summary>
    public static Control SaveList(List<string> names, Action<string> onPick, Action onBack)
    {
        Control root = Fullscreen();
        var box = CenterBox(520);
        box.AddChild(Title("Choose Expedition Save"));
        box.AddChild(Subtitle("Each name is a whole universe"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        if (names.Count == 0)
        {
            box.AddChild(Info("No saves — go back and start a new expedition", SubColor));
        }
        else
        {
            var list = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
            list.AddThemeConstantOverride("separation", 8);
            foreach (string name in names)
            {
                var item = new Button
                {
                    Text = $"⯌  {name}",
                    CustomMinimumSize = new Vector2(420, 46),
                    MouseFilter = Control.MouseFilterEnum.Stop
                };
                item.AddThemeFontSizeOverride("font_size", 20);
                item.AddThemeColorOverride("font_color", new Color(TextColor));
                item.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("1b2233"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
                item.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("2a3550"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
                item.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
                item.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
                string picked = name;
                item.Pressed += () => onPick(picked);
                item.Pressed += () => ClickSound?.Invoke();
                list.AddChild(item);
            }
            box.AddChild(list);
        }

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        box.AddChild(ActionButton("Back", onBack, new Color("9aa7c0")));
        var center = Centered();
        center.AddChild(box);
        root.AddChild(center);
        return root;
    }

    /// <summary>星图（规格 §4）：母舰居中，任务节点散点分布；节点锚定窗口中心，随 resize 自适应。</summary>
    public static Control Starmap(StarMap map, Action<StarMapNode> onPick, Action onMothership, Action onMenu)
    {
        Control root = Fullscreen();

        // Sprint 5 P0-B：星图章节星云背景（zone1-4，冷蓝/青绿/紫红/暗红），叠加于深色底
        var nebula = new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/background/zone{Math.Clamp(map.Chapter, 1, 4)}.png"),
            StretchMode = TextureRect.StretchModeEnum.Tile,
            Modulate = new Color(1f, 1f, 1f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        nebula.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(nebula);

        // 标题区
        var header = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        header.OffsetTop = 24;
        var title = new Label
        {
            Text = $"Star Map · Sector {map.Chapter} (Starship Lv.{map.MothershipLevel})",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color(TitleColor));
        header.AddChild(title);
        var hint = new Label
        {
            Text = "Pick a mission — the map re-randomizes either way",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(SubColor));
        header.AddChild(hint);

        // 进入母舰仓库（底部按钮，锚定底部居中）
        var mothershipBtn = new Button
        {
            Text = "⚙ Enter Starship (Dock/Equip/Workshop/Repair/Shop)",
            CustomMinimumSize = new Vector2(360, 48),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        mothershipBtn.AddThemeFontSizeOverride("font_size", 18);
        mothershipBtn.AddThemeColorOverride("font_color", new Color("10131f"));
        mothershipBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("ffe08a"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        mothershipBtn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("ffe08a").Lightened(0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        mothershipBtn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("ffe08a").Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        mothershipBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        mothershipBtn.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        mothershipBtn.Position = new Vector2(-180, -24);
        mothershipBtn.Pressed += onMothership;
        mothershipBtn.Pressed += () => ClickSound?.Invoke();
        root.AddChild(mothershipBtn);

        // 迭代 22：主菜单按钮（进入母舰按钮下方）
        var menuBtn = new Button
        {
            Text = "⌂ Main Menu",
            CustomMinimumSize = new Vector2(160, 36),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        menuBtn.AddThemeFontSizeOverride("font_size", 15);
        menuBtn.AddThemeColorOverride("font_color", new Color("#d8ecff"));
        menuBtn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("#2a3550"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        menuBtn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("#3a4868"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        menuBtn.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        menuBtn.Position = new Vector2(24, -24);
        menuBtn.Pressed += onMenu;
        menuBtn.Pressed += () => ClickSound?.Invoke();
        root.AddChild(menuBtn);

        root.AddChild(header);

        // 母舰居中（锚定窗口中心）
        var mothership = new Label
        {
            Text = "◆ Starship",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        mothership.AddThemeFontSizeOverride("font_size", 18);
        mothership.AddThemeColorOverride("font_color", new Color("ffe08a"));
        mothership.SetAnchorsPreset(Control.LayoutPreset.Center);
        mothership.Position = new Vector2(-40, -18);
        root.AddChild(mothership);

        // 任务节点：普通节点绕母舰环形均匀分布，Boss 固定右侧（迭代 27.1 UI 重排）
        var normalNodes = map.Nodes.Where(n => !n.IsBoss).ToList();
        var bossNode = map.Nodes.FirstOrDefault(n => n.IsBoss);
        const float ringRx = 360f;
        const float ringRy = 220f;
        const float bossX = 480f;
        const float bossY = 0f;
        for (int i = 0; i < normalNodes.Count; i++)
        {
            var node = normalNodes[i];
            double ang = -Math.PI / 2 + (Math.PI * 2 * i / normalNodes.Count);
            float nx = (float)(Math.Cos(ang) * ringRx);
            float ny = (float)(Math.Sin(ang) * ringRy);
            var btn = new Button
            {
                Text = $"⚔ Cleansing ★{node.DangerStars}",
                CustomMinimumSize = new Vector2(96, 44),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            btn.AddThemeFontSizeOverride("font_size", 15);
            Color c = node.DangerStars >= 4 ? new Color("ff6b4a") : new Color("4da6ff");
            btn.AddThemeColorOverride("font_color", new Color("10131f"));
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = c, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = c.Lightened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = c.Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.SetAnchorsPreset(Control.LayoutPreset.Center);
            btn.Position = new Vector2(nx - 48, ny - 22);

            var info = new Label
            {
                Text = $"Power {node.Strength}",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 12);
            info.AddThemeColorOverride("font_color", new Color(SubColor));
            info.SetAnchorsPreset(Control.LayoutPreset.Center);
            info.Position = new Vector2(nx - 30, ny + 24);

            StarMapNode picked = node;
            btn.Pressed += () => onPick(picked);
            btn.Pressed += () => ClickSound?.Invoke();
            root.AddChild(info);
            root.AddChild(btn);
        }
        if (bossNode != null)
        {
            var btn = new Button
            {
                Text = bossNode.GateLabel ?? "☠ BOSS",
                CustomMinimumSize = new Vector2(140, 56),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            btn.AddThemeFontSizeOverride("font_size", 17);
            btn.AddThemeColorOverride("font_color", new Color("ffffff"));
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("ff3b6b"), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("ff3b6b").Lightened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color("ff3b6b").Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.SetAnchorsPreset(Control.LayoutPreset.Center);
            btn.Position = new Vector2(bossX - 70, bossY - 28);

            var info = new Label
            {
                Text = $"Power {bossNode.Strength}",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 12);
            info.AddThemeColorOverride("font_color", new Color(SubColor));
            info.SetAnchorsPreset(Control.LayoutPreset.Center);
            info.Position = new Vector2(bossX - 30, bossY + 34);

            StarMapNode picked = bossNode;
            btn.Pressed += () => onPick(picked);
            btn.Pressed += () => ClickSound?.Invoke();
            root.AddChild(info);
            root.AddChild(btn);
        }

        return root;
    }

    /// <summary>任务结算（规格 §4.3）：无论成败消耗一次时间，返回星图重随机。</summary>
    public static Control Settlement(bool victory, string lootSummary, string missionSummary, Action onReturn)
    {
        Control root = Fullscreen();
        var box = CenterBox();
        box.AddChild(Title(victory ? "Mission Complete" : "Mission Failed"));
        box.AddChild(Info(missionSummary, victory ? TitleColor : "ff6b4a"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        if (!string.IsNullOrEmpty(lootSummary))
        {
            box.AddChild(Info(lootSummary));
        }
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        box.AddChild(Info("The sortie consumed a turn either way — the map re-randomizes on return", SubColor));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
        box.AddChild(ActionButton("Back to Map", onReturn, victory ? new Color("6ee06e") : new Color("4da6ff")));
        var center = Centered();
        center.AddChild(box);
        root.AddChild(center);
        return root;
    }
}
