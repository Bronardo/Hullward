# Hullward《深空暗骸》

太空舰船 ARPG 刷宝游戏 —— Deakin SIT771 "Something Awesome" 7.4H 项目
技术栈：**Godot 4.7.2（.NET 版）+ C# / .NET 8**

## 玩法

- **主炮自动索敌开火**：靠近敌舰即自动锁定最近目标射击
- **手动技能**：`Q` 过载炮（3× 火力直击）/ `E` 护盾充能（回复 50% 护盾）
- **刷宝成长**：击毁敌舰掉落模块（白/蓝/黄/绿/太古 5 品质）与合金，自动装配最强装备，火力/护盾实时成长
- **4 星域推进**：安全 → 争议 → 无人深空 → 坍缩禁区（Boss），清怪自动跃迁，难度与掉落递增
- **永久存档**：`F5` 存档 / `F9` 读档（JSON，含星域/背包/合金/耐久）

## 操作

| 按键 | 功能 |
|---|---|
| WASD / 方向键 | 移动 |
| Q / E | 过载炮 / 护盾充能 |
| F5 / F9 | 存档 / 读档 |

## 运行（编辑器/开发）

```powershell
# 双击 run_game.bat（仓库根），或：
D:\pg\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path Hullward
```

## 运行（发布版）

```powershell
# release exe（含 .NET 运行时，无需安装 Godot）
Hullward\build\Hullward.exe
```

## 测试

```powershell
dotnet test Hullward\Hullward.sln    # 53 个域层测试
dotnet build Hullward\Hullward.sln   # 0 error / 0 warning
```

## 架构

```
Hullward/
├── src/
│   ├── Domain/     # 纯 C# 域层（零 Godot 依赖，xUnit 可测）
│   │   ├── Ships/      # ShipBase 抽象层次（轻巡/突击/战列/要塞）
│   │   ├── Modules/    # IShipModule 接口 + 背包 + 装配
│   │   ├── Enemies/    # EnemyShip 多态层次 + AI 行为
│   │   ├── Combat/     # 结算 / 索敌 / 主动技能
│   │   ├── Loot/       # 掉落表 / 拆解工坊
│   │   ├── WorldGen/   # 星域生成
│   │   └── Save/       # JSON 存档
│   └── Game/       # Godot 表现层（节点渲染/输入/HUD）
├── tests/           # xUnit 测试项目
├── docs/            # 设计文档 / ULO 证据文档
└── build/           # release exe（git 忽略）
```

设计依据见 `docs/设计文档.md`；过程证据见 `docs/ULO 证据文档.md` 与工程日志。

## 素材

开发期全部为程序化绘制（ColorRect 像素方块）；定稿期可替换 CC0/自绘素材（见设计文档 §4 策略）。
