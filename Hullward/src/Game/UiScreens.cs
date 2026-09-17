using System;
using System.Collections.Generic;
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
        return btn;
    }

    /// <summary>主菜单（规格 §2）。</summary>
    public static Control Menu(Action onNew, Action onContinue, Action onQuit)
    {
        Control root = Fullscreen();
        var box = CenterBox();
        box.AddChild(Title("Hullward《深空暗骸》"));
        box.AddChild(Subtitle("Echoes of Collapsed Eons"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });
        box.AddChild(ActionButton("开始游戏（新建远征）", onNew, new Color("4da6ff")));
        box.AddChild(ActionButton("继续游戏", onContinue, new Color("6ee06e")));
        box.AddChild(ActionButton("退出", onQuit, new Color("ff6b4a")));
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
        box.AddChild(Title("命名舰长"));
        box.AddChild(Subtitle("名字将作为远征档案的唯一标识（≤12 字，禁特殊字符）"));
        var edit = new LineEdit
        {
            PlaceholderText = "输入舰长名…",
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
        box.AddChild(ActionButton("确认命名，出发", () => onConfirm(edit.Text), new Color("4da6ff")));
        box.AddChild(ActionButton("返回", onBack, new Color("9aa7c0")));
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
        box.AddChild(Title("选择远征档案"));
        box.AddChild(Subtitle("每个名字都是一位舰长的宇宙"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        if (names.Count == 0)
        {
            box.AddChild(Info("暂无存档 —— 返回并开始新的远征", SubColor));
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
                list.AddChild(item);
            }
            box.AddChild(list);
        }

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        box.AddChild(ActionButton("返回", onBack, new Color("9aa7c0")));
        var center = Centered();
        center.AddChild(box);
        root.AddChild(center);
        return root;
    }

    /// <summary>星图（规格 §4）：母舰居中，任务节点散点分布；节点锚定窗口中心，随 resize 自适应。</summary>
    public static Control Starmap(StarMap map, Action<StarMapNode> onPick, Action onMothership)
    {
        Control root = Fullscreen();

        // 标题区
        var header = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        header.OffsetTop = 24;
        var title = new Label
        {
            Text = $"星图 · 第 {map.Chapter} 章（母舰 Lv.{map.MothershipLevel}）",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color(TitleColor));
        header.AddChild(title);
        var hint = new Label
        {
            Text = "选择任务出击 · 无论成败，返回后星图将全部重随机",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(SubColor));
        header.AddChild(hint);

        // 进入母舰仓库（底部按钮，锚定底部居中）
        var mothershipBtn = new Button
        {
            Text = "⚙ 进入母舰（仓库/装配/工坊/维修/商店）",
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
        mothershipBtn.Position = new Vector2(-180, -64);
        mothershipBtn.Pressed += onMothership;
        root.AddChild(mothershipBtn);

        root.AddChild(header);

        // 节点半径带映射缩放（相对窗口中心）
        const float scale = 0.55f;

        // 母舰居中（锚定窗口中心）
        var mothership = new Label
        {
            Text = "◆ 母舰",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        mothership.AddThemeFontSizeOverride("font_size", 18);
        mothership.AddThemeColorOverride("font_color", new Color("ffe08a"));
        mothership.SetAnchorsPreset(Control.LayoutPreset.Center);
        mothership.Position = new Vector2(-30, -18);
        root.AddChild(mothership);

        // 任务节点（相对窗口中心偏移，resize 自动跟随）
        foreach (StarMapNode node in map.Nodes)
        {
            var btn = new Button
            {
                Text = node.IsBoss ? "☠ BOSS" : $"清剿 ★{node.DangerStars}",
                CustomMinimumSize = new Vector2(node.IsBoss ? 120 : 96, node.IsBoss ? 56 : 44),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            btn.AddThemeFontSizeOverride("font_size", node.IsBoss ? 18 : 15);
            Color c = node.IsBoss ? new Color("ff3b6b") : new Color(node.DangerStars >= 4 ? "ff6b4a" : "4da6ff");
            btn.AddThemeColorOverride("font_color", node.IsBoss ? new Color("ffffff") : new Color("10131f"));
            btn.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = c, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = c.Lightened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = c.Darkened(0.2f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.SetAnchorsPreset(Control.LayoutPreset.Center);
            btn.Position = new Vector2(node.X * scale - 48, node.Y * scale - 22);

            var info = new Label
            {
                Text = $"强度 {node.Strength}",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            info.AddThemeFontSizeOverride("font_size", 12);
            info.AddThemeColorOverride("font_color", new Color(SubColor));
            info.SetAnchorsPreset(Control.LayoutPreset.Center);
            info.Position = new Vector2(node.X * scale - 30, node.Y * scale + (node.IsBoss ? 34 : 24));

            StarMapNode picked = node;
            btn.Pressed += () => onPick(picked);
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
        box.AddChild(Title(victory ? "任务完成" : "远征失败"));
        box.AddChild(Info(missionSummary, victory ? TitleColor : "ff6b4a"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        if (!string.IsNullOrEmpty(lootSummary))
        {
            box.AddChild(Info(lootSummary));
        }
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        box.AddChild(Info("无论成败，此次出击已消耗一次时间 —— 返回后星图将全部重随机", SubColor));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
        box.AddChild(ActionButton("返回星图", onReturn, victory ? new Color("6ee06e") : new Color("4da6ff")));
        var center = Centered();
        center.AddChild(box);
        root.AddChild(center);
        return root;
    }
}
