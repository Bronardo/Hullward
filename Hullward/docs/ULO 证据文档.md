# Hullward《深空暗骸》ULO1-5 证据文档

> 版本：v1.2（含 sprint 3：母舰内部 + 词缀 + UI 自适应）｜ 日期：2026-09-17 ｜ 用途：SIT771 "Something Awesome" 7.4H 任务最终证据
> 对应作业要求：ULO1（代码规范/调试定位问题）ULO2（抽象/封装/继承/多态）ULO3（实现并测试）ULO4（图表文字表达设计）ULO5（证据论证）

---

## ULO1：代码规范与调试能力

### 证据 1：工程日志（秒级时间戳 + 问题修复全链路）

`工程日志.md`（vault 侧）全程记录，格式强制 `YYYY-MM-DD HH:mm:ss`，每条问题按 **现象 → 排查 → 根因 → 修复** 四段式记录。摘录：

| 迭代 | 问题 | 根因 | 修复 |
|---|---|---|---|
| 迭代1 | 敌舰不可见 | 生成范围超出相机视口 | 收进视口（`318f24f`） |
| 迭代1 | 敌舰仍不可见 | 域对象初始位置 (0,0) 未同步节点，首帧被拽回原点 | Setup 时同步域位置（`1ef5924`） |
| 迭代1 | 无战斗发生 | 重构漏调 `SetTargets`，索敌列表为空；敌人无攻击逻辑 | 注入目标 + 近身攻击（`d728233`） |
| 迭代2 | 装配白装不加护盾 | `AutoEquipBest` 中 `Rarity > Common` 恒假，最低品质永不装配 | 单测复现 + `bestIndex < 0` 兜底（`06cc140`） |
| 迭代4 | 导出 exe 崩溃 | solution 位置不符合 Godot .NET 导出预期，C# 程序集未发布 | solution 移入项目目录（`64c65f9`） |
| 迭代5 | 星图节点溢出窗口 | 节点半径带 260-430 超出 960×540 视口 | 坐标缩放映射 0.55（`4561ff9`） |
| 迭代5 | 重开无存档 | 存档是手动 F5，用户未触发 | 任务结算自动存档 + 失败耐久记 1（`048f3fd`） |
| 迭代6 | UI 不随窗口居中 | `Center` 锚点预设只把原点放窗口中心，控件向右下延伸；星图节点为绝对像素 | CenterContainer 真居中 + 节点锚定窗口中心（`b684287`） |

### 证据 2：代码规范

- 统一命名：PascalCase 类型/方法、camelCase 局部、`_camelCase` 字段；XML doc 注释覆盖全部公开类型（见 `src/Domain/**`）
- 分层铁律：Domain 零 Godot 依赖（`using Godot` 在 `src/Domain` 下出现次数 = 0）；表现层只做桥接
- `dotnet build` 全程 0 error 0 warning；`dotnet test` 105/105 通过

### 证据 3：Git 工作流

27 个功能提交，每个提交单一时序逻辑（scaffold → 设计 → 域层 → 表现层 → 修复 → 发布），见 `git log --oneline`。

---

## ULO2：抽象 / 封装 / 继承 / 多态

### 继承层次（ShipBase 抽象基类 + 4 玩家船体派生）

```csharp
public abstract class ShipBase : IShip          // 抽象：船体共性
public sealed class ScoutShip : ShipBase        // 轻巡：2 槽
public sealed class AssaultShip : ShipBase      // 突击：3 槽
public sealed class Battleship : ShipBase       // 战列：4 槽
public sealed class FortressShip : ShipBase     // 要塞：5 槽
```

### 敌舰行为多态（EnemyShip 抽象方法，5 个子类，零 if-else 行为分支）

```csharp
public abstract class EnemyShip : ShipBase, ITargetable
{
    public abstract void UpdateBehavior(float dt, float playerX, float playerY);
}
public sealed class ReconDrone : EnemyShip     // 高速直线追击
public sealed class RaiderShip : EnemyShip     // 环形游走
public sealed class HeavyFortress : EnemyShip  // 慢速重甲
public sealed class SwarmDrone : EnemyShip     // 虫群：蛇形逼近（第3章新敌人）
public sealed class GuardianBoss : EnemyShip   // 全图索敌 Boss
```
`Main.SpawnWave` 按章节/强度用工厂注入不同子类；章节解锁即新子类登场——**章节制 = 多态扩展的证据增量**。

### 接口多态（IShipModule / ITargetable）

```csharp
public interface IShipModule { ModuleType Type; string Name; void ApplyEffect(ShipBase ship); }
public sealed class WeaponModule : IShipModule  // 火力/攻速加成
public sealed class ArmorModule : IShipModule   // 护盾/耐久/抗性加成
public sealed class PowerModule : IShipModule   // 能源/技能系
public sealed class SpecialModule : IShipModule // 寻宝增效等
```
装配按槽位类型多态分发（`ShipFitting.CreateModule` switch on `ModuleType`），词缀并入加成——**刷装深度 = 接口多态 + 数据驱动证据**。

### 词缀系统（数据驱动多态）

```csharp
public enum AffixStat { FirepowerPercent, AttackSpeedPercent, CritChance, ..., MagicFind, ... }
public sealed class Affix { string Name; AffixStat Stat; float Value; }
public sealed class AffixPool { public static IReadOnlyList<AffixEntry> ForSlot(ModuleType slot); }
public static class ModuleRoller { /* 品质词缀数 / 权重抽取 / 数值区间 / 洗练 */ }
```
LD 词缀表（§3）原样录入：4 槽位词缀池、权重、品质区间；roll 规则单测覆盖（数量边界/不重复/权重/数值区间）。

### 抽象技能（ActiveSkill）

```csharp
public abstract class ActiveSkill { bool TryUse(PlayerContext); void Tick(float); }
public sealed class OverdriveCannon : ActiveSkill  // Q：3× 火力直击
public sealed class ShieldBurst : ActiveSkill      // E：回复 50% 护盾
```

### 封装

- `ShipBase` 属性 `protected set` / `internal` 加成方法（`AddFirepower/AddShield/AddMaxHull/AddFireRate/AddMagicFind/AddArmor`），外部无法篡改船体数值
- `EnemyShip` 行为速度/索敌范围 `protected abstract`，子类决定
- 域层状态（位置/耐久/背包/装配槽位/存档/星图/章节）与表现层渲染完全隔离
- 手动装配不直接持有船体：`ShipFittingService` 只在背包与槽位列表之间移动模块引用，属性由 `ApplyToShip` 统一重算——**装配逻辑零 Godot 依赖，可单测**
- 存档多文件制：`SaveService` 封装目录/文件操作，`SaveDataMapper` 负责词缀/装配槽位 ↔ 存档条目转换

---

## ULO3：实现并测试

### 测试统计

```
dotnet test Hullward.sln
Passed! - Failed: 0, Passed: 105, Skipped: 0, Total: 105
```

| 测试类 | 覆盖系统 | 用例数 |
|---|---|---|
| StarSystemGeneratorTests | 星域生成规则/连通/种子复现 | 7 |
| StarMapGeneratorTests | 星图任务生成：数量/强度区间/确定性/Boss 固定/防重叠 | 12 |
| ChapterCatalogTests | 章节目录：数据/解锁边界/基准单调 | 6 |
| TargetingSystemTests | 自动索敌优先级/失效过滤 | 5 |
| CombatCalculatorTests | 伤害公式/护盾吸收/下限 | 7 |
| EnemyShipTests | 多态行为/Boss/区域缩放 | 9 |
| LootTableTests | 品质权重/合金/确定性 | 7 |
| InventoryAndFittingTests | 背包/装配/拆解/重置 | 9 |
| SaveServiceTests | 多文件存档往返/列表/删除/命名校验 | 6 |
| ActiveSkillTests | 技能施放/冷却/上限 | 6 |
| MothershipSystemsTests | 词缀生成（LD §3）/工坊（拆解/洗练/维修）/商店/手动装配/配装方案 | 24 |

### 测试要点

- 全部为纯 C# 域层测试，不依赖 Godot 运行时（`dotnet test` 直接跑）
- 随机逻辑注入 `Random` 种子保证确定性；边界情况（空列表/槽满/护盾封顶/最低品质/存档重名/章节越界/太古不可拆/合金不足）均有用例
- 回归价值实证：`AutoEquip_ArmorModule_IncreasesShield` 捕获"白装永不装配"真实 bug；`ModuleRoller_*` 覆盖 LD 词缀表 roll 规则边界（数量范围/暗金区间/不重复/槽位池合法性）

---

## ULO4：图表与文字表达设计

设计文档 `docs/设计文档.md` 包含：
- 分层架构表（Domain / Presentation 职责与依赖）
- Mermaid 类图（ShipBase/EnemyShip/模块/词缀/服务完整关系）
- Godot 场景树（Main → StarSystemView/PlayerShipView/Enemies/UI）
- 系统实现顺序表（迭代 1-6 映射）+ 验收标准
- 战斗结算公式、掉落品质权重表、星域难度缩放公式、词缀表（LD §3）
- UI 流程：主菜单 → 命名/选档 → 星图 → **母舰内部（装配/仓库/工坊/维修/商店）** → 战斗 → 结算 → 星图重随机

---

## ULO5：证据论证

| 声明 | 证据位置 |
|---|---|
| 代码规范良好、构建无警告 | `dotnet build` 0W/0E；`工程日志.md` |
| 调试定位问题能力 | `工程日志.md` 8 条"现象→根因→修复"记录 |
| 抽象/继承/多态运用 | `src/Domain/Ships/ShipBase.cs`、`src/Domain/Enemies/EnemyShip.cs`、`src/Domain/Combat/ActiveSkill.cs`、`src/Domain/Modules/{IShipModule,Affix}.cs`、`src/Domain/WorldGen/ChapterCatalog.cs` |
| 实现并测试 | `tests/Hullward.Tests/` 11 个测试类 105 用例全绿 |
| 设计表达能力 | `docs/设计文档.md`（类图/场景树/表格/公式/词缀表/UI 流程） |
| 完整可交付 | release exe `build/Hullward.exe`（含 .NET 运行时，启动验证通过） |
| 可复现 | `git log` 27 提交；README 运行/测试命令 |

---

## 相关
- [[设计文档]]
- [[工程日志]]
- [[游戏策划文档]]
- [[Hullward_UI开发规格_LD]]（v0.2 依据）
- [[Hullward_继续开发规划_LD]]（词缀表 §3 / 空间站清单 §4 依据）
