# Hullward《深空暗骸》

太空舰船 ARPG 刷宝游戏 —— Deakin SIT771 "Something Awesome" 7.4H 项目
技术栈：**Godot 4.7.2（.NET 版）+ C# / .NET 8**

## 玩法（任务制循环 · UI 规格 v0.2）

1. **主菜单**：开始新的远征（命名舰长） / 继续游戏（多档存档，只显示舰长名）
2. **星图**：母舰居中，4-8 个随机任务散点（清剿 / BOSS），标注危险等级 ★ 与强度
3. **任务战斗**：主炮自动索敌开火 + `Q` 过载炮 / `E` 护盾充能；清剿全部暗骸即胜
4. **结算**：无论成败消耗一次"时间"，返回后星图全部重随机；远征记录自动保存
5. **成长**：任务获得母舰经验 → 母舰升级 → 仓库扩容 + 章节解锁（4 章）
6. **刷宝**：击毁敌舰掉落模块（白/蓝/黄/绿/太古 5 品质）与合金，自动装配最强装备

## 操作

| 按键 | 功能 |
|---|---|
| 鼠标 | 菜单 / 星图选任务 |
| WASD / 方向键 | 移动 |
| Q / E | 过载炮 / 护盾充能 |
| F5 / F9 | 快存 / 读档（结算自动保存）|

## 章节（随母舰等级解锁）

| 章节 | 解锁 | 敌人 |
|---|---|---|
| 第1章 航标 | 初始 | 侦察机、突击舰 |
| 第2章 星港 | 母舰 Lv2 | + 堡垒舰 |
| 第3章 深空 | 母舰 Lv3 | + 虫群 |
| 终章 坍缩 | 母舰 Lv4 | 禁区守卫（Boss）|

## 运行（编辑器/开发）

```powershell
# 双击 run_game.bat（仓库根），或：
D:\pg\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path Hullward
```

## 运行（发布版）

```powershell
Hullward\build\Hullward.exe   # release exe（含 .NET 运行时，无需安装 Godot）
```

## 测试

```powershell
dotnet test Hullward\Hullward.sln    # 81 个域层测试
dotnet build Hullward\Hullward.sln   # 0 error / 0 warning
```

## 存档位置

```
%APPDATA%\Godot\app_userdata\Hullward\saves\<舰长名>.json
```

## 架构

```
Hullward/
├── src/
│   ├── Domain/     # 纯 C# 域层（零 Godot 依赖，xUnit 可测）
│   │   ├── Ships/      # ShipBase 抽象层次（轻巡/突击/战列/要塞）
│   │   ├── Modules/    # IShipModule 接口 + 背包 + 装配
│   │   ├── Enemies/    # EnemyShip 多态层次（侦察/劫掠/堡垒/虫群/Boss）
│   │   ├── Combat/     # 结算 / 索敌 / 主动技能
│   │   ├── Loot/       # 掉落表 / 拆解工坊
│   │   ├── WorldGen/   # 星域生成 / 星图任务生成 / 章节目录
│   │   └── Save/       # 多文件命名存档
│   └── Game/       # Godot 表现层（UI 屏幕 / 节点渲染 / 输入 / HUD）
├── tests/           # xUnit 测试项目
├── docs/            # 设计文档 / ULO 证据文档
└── build/           # release exe（git 忽略）
```

设计依据见 `docs/设计文档.md`；过程证据见 `docs/ULO 证据文档.md` 与工程日志。

## 素材

开发期全部为程序化绘制（ColorRect 像素方块）；定稿期可替换 CC0/自绘素材。
