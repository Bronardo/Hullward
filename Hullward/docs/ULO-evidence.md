# Hullward — ULO1-5 Evidence Document

> Version: v1.2 (includes sprint 3: mothership + affixes + adaptive UI; updated through iteration 28) ｜ Date: 2026-09-19 ｜ Purpose: SIT771 "Something Awesome" 7.4H final evidence
> Mapping to requirements: ULO1 (code conventions / debugging), ULO2 (abstraction/encapsulation/inheritance/polymorphism), ULO3 (implement and test), ULO4 (diagrams/text expressing design), ULO5 (evidentiary reasoning)

---

## ULO1: Code conventions and debugging

### Evidence 1: Engineering log (second-precision timestamps + full bug-fix chain)

`工程日志.md` (vault side) records everything, format enforced `YYYY-MM-DD HH:mm:ss`; each bug follows the four-part pattern **symptom -> investigation -> root cause -> fix**. Excerpt:

| Iteration | Symptom | Root cause | Fix |
|---|---|---|---|
| Iter 1 | Enemies invisible | spawn range outside camera viewport | Clamp into viewport (`318f24f`) |
| Iter 1 | Enemies still invisible | Domain object initial position (0,0) not synced to node, dragged to origin on first frame | Sync domain position at Setup (`1ef5924`) |
| Iter 1 | No combat happens | Refactor missed `SetTargets` call; targeting list empty; enemies had no attack logic | Inject targets + melee attack (`d728233`) |
| Iter 2 | Fitting a Common item adds no shield | `Rarity > Common` always false in `AutoEquipBest`; lowest rarity never equipped | Unit-test repro + `bestIndex < 0` fallback (`06cc140`) |
| Iter 4 | Exported exe crashes | solution location not where Godot .NET export expects; C# assembly not published | Move solution into project dir (`64c65f9`) |
| Iter 5 | Starmap nodes overflow window | Node radius 260-430 exceeds 960x540 viewport | Coordinate scale mapping 0.55 (`4561ff9`) |
| Iter 5 | No save on restart | Save was manual F5 only; user never triggered it | Auto-save on settlement + record hull=1 on fail (`048f3fd`) |
| Iter 6 | UI not centered on window | `Center` preset only places origin at window center; controls extend bottom-right; starmap nodes were absolute pixels | True CenterContainer + nodes anchored to window center (`b684287`) |

### Evidence 2: Code conventions

- Naming: PascalCase for types/methods, camelCase for locals, `_camelCase` for fields; XML doc comments cover all public types (see `src/Domain/**`)
- Layering iron rule: Domain has zero Godot dependency (`using Godot` count under `src/Domain` = 0); presentation layer is pure bridge
- `dotnet build` throughout: 0 error 0 warning; `dotnet test` 228/228 pass

### Evidence 3: Git workflow

Feature commits, each a single temporal unit (scaffold -> design -> domain -> presentation -> fix -> release); see `git log --oneline`.

---

## ULO2: Abstraction / Encapsulation / Inheritance / Polymorphism

### Inheritance hierarchy (ShipBase abstract base + 4 player hulls)

```csharp
public abstract class ShipBase : IShip          // abstract: hull commonalities
public sealed class ScoutShip : ShipBase        // Scout: 2 slots
public sealed class AssaultShip : ShipBase      // Assault: 3 slots
public sealed class Battleship : ShipBase       // Battleship: 4 slots
public sealed class FortressShip : ShipBase     // Fortress: 5 slots
```

### Enemy behavior polymorphism (EnemyShip abstract method, subclasses, zero if-else behavior branches)

```csharp
public abstract class EnemyShip : ShipBase, ITargetable
{
    public abstract void UpdateBehavior(float dt, float playerX, float playerY);
}
public sealed class ReconDrone : EnemyShip     // fast straight chase
public sealed class RaiderShip : EnemyShip     // orbit at medium range
public sealed class HeavyFortress : EnemyShip  // slow, heavy armor
public sealed class SwarmDrone : EnemyShip     // swarm: serpentine approach (sector 3 new enemy)
public sealed class GuardianBoss : EnemyShip   // full-map targeting boss
```
`Main.SpawnWave` injects different subclasses by sector/power via a factory; sector unlock introduces new subclasses — **sector progression = evidence increment for polymorphism**.

### Interface polymorphism (IShipModule / ITargetable)

```csharp
public interface IShipModule { ModuleType Type; string Name; void ApplyEffect(ShipBase ship); }
public sealed class WeaponModule : IShipModule  // firepower/attack-speed bonus
public sealed class ArmorModule : IShipModule   // shield/hull/resist bonus
public sealed class PowerModule : IShipModule   // energy/skill bonus
public sealed class SpecialModule : IShipModule // Magic Find etc.
```
Fitting dispatches by slot type polymorphically (`ShipFitting.CreateModule` switches on `ModuleType`); affixes fold into the bonus — **loot depth = interface polymorphism + data-driven evidence**.

### Affix system (data-driven polymorphism)

```csharp
public enum AffixStat { FirepowerPercent, AttackSpeedPercent, CritChance, ..., MagicFind, ... }
public sealed class Affix { string Name; AffixStat Stat; float Value; }
public sealed class AffixPool { public static IReadOnlyList<AffixEntry> ForSlot(ModuleType slot); }
public static class ModuleRoller { /* rarity affix count / weight draw / value range / reroll */ }
```
LD affix table (§3) entered verbatim: 4 slot affix pools, weights, rarity ranges; roll rules covered by unit tests (count boundaries / no-duplicate / weights / value ranges).

### Abstract skills (ActiveSkill)

```csharp
public abstract class ActiveSkill { bool TryUse(PlayerContext); void Tick(float); }
public sealed class OverdriveCannon : ActiveSkill  // Q: 3x firepower direct hit
public sealed class ShieldBurst : ActiveSkill      // E: restore 50% shield
```

### Encapsulation

- `ShipBase` properties use `protected set` / `internal` bonus methods (`AddFirepower/AddShield/AddMaxHull/AddFireRate/AddMagicFind/AddArmor`); external code cannot tamper with hull stats
- `EnemyShip` behavior speed / targeting range are `protected abstract`, decided by subclasses
- Domain state (position/hull/inventory/fit slots/save/starmap/sector) fully isolated from presentation rendering
- Manual fitting does not hold the hull directly: `ShipFittingService` only moves module references between inventory and slot list; stats are recomputed uniformly by `ApplyToShip` — **fitting logic has zero Godot dependency, unit-testable**
- Multi-file save: `SaveService` encapsulates directory/file operations; `SaveDataMapper` handles affix/fit-slot <-> save entry conversion

---

## ULO3: Implement and test

### Test statistics

```
dotnet test Hullward.sln
Passed! - Failed: 0, Passed: 228, Skipped: 0, Total: 228
```

| Test class | System under test | Cases |
|---|---|---|
| StarSystemGeneratorTests | Sector generation rules / connectivity / seed reproducibility | 7 |
| StarMapGeneratorTests | Starmap mission generation: count / power range / determinism / boss fixed / no overlap | 12 |
| ChapterCatalogTests | Sector catalog: data / unlock boundaries / baseline monotonic | 6 |
| TargetingSystemTests | Auto-target priority / stale-target filtering | 5 |
| CombatCalculatorTests | Damage formula / shield absorption / floor | 7 |
| EnemyShipTests | Polymorphic behavior / boss / sector scaling | 9 |
| LootTableTests | Rarity weights / alloy / determinism | 7 |
| InventoryAndFittingTests | Inventory / fitting / disassemble / reset | 9 |
| SaveServiceTests | Multi-file save round-trip / list / delete / name validation | 6 |
| ActiveSkillTests | Skill cast / cooldown / cap | 6 |
| MothershipSystemsTests | Affix generation (LD §3) / workshop (disassemble/reroll/repair) / shop / manual fit / loadouts | 24 |
| BossPhaseTests | Boss phase thresholds / state transitions | 30 |
| Sprint3AffixAndEnemiesTests | Sprint 3 affix and enemy integration | 34 |
| Sprint4ChapterContentTests | Sprint 4 sector content | 34 |
| Sprint4EnergySystemTests | Sprint 4 energy system | 23 |
| DockAndShipClassTests | Dock and ship class switching | 9 |
| Sprint5ModuleNamingTests | Sprint 5 module naming | 5 |

### Test highlights

- All tests are pure C# domain-layer tests, no Godot runtime dependency (`dotnet test` runs directly)
- Random logic injects a `Random` seed for determinism; edge cases covered (empty list / full slots / shield cap / lowest rarity / duplicate save name / out-of-range sector / Ancient cannot disassemble / insufficient alloy)
- Regression evidence: `AutoEquip_ArmorModule_IncreasesShield` caught the real "Common never equipped" bug; `ModuleRoller_*` covers LD affix table roll-rule boundaries (count range / ancient tier / no duplicate / slot-pool validity)

---

## ULO4: Diagrams and text expressing design

Design doc `docs/design.md` contains:
- Layered architecture table (Domain / Presentation responsibilities and dependencies)
- Mermaid class diagram (ShipBase/EnemyShip/modules/affixes/services complete relations)
- Godot scene tree (Main -> StarSystemView/PlayerShipView/Enemies/UI)
- System implementation order table (iterations 1-4 mapping) + acceptance criteria
- Combat settlement formula, loot rarity weight table, sector difficulty scaling, affix table (LD §3)
- UI flow: main menu -> naming/save select -> starmap -> **mothership interior (dock/fit/inventory/workshop/repair/shop)** -> combat -> settlement -> starmap re-randomizes

---

## ULO5: Evidentiary reasoning

| Claim | Evidence location |
|---|---|
| Good code conventions, build has no warnings | `dotnet build` 0W/0E; `工程日志.md` |
| Ability to debug and locate issues | `工程日志.md` 8 "symptom -> root cause -> fix" entries |
| Abstraction / inheritance / polymorphism applied | `src/Domain/Ships/ShipBase.cs`, `src/Domain/Enemies/EnemyShip.cs`, `src/Domain/Combat/ActiveSkill.cs`, `src/Domain/Modules/{IShipModule,Affix}.cs`, `src/Domain/WorldGen/ChapterCatalog.cs` |
| Implement and test | `tests/Hullward.Tests/` 17 test classes, 228 cases all green |
| Design expression | `docs/design.md` (class diagram / scene tree / tables / formulas / affix table / UI flow) |
| Complete deliverable | release exe `build/Hullward.exe` (bundles .NET runtime; launch verified) |
| Reproducible | `git log` feature commits; README run/test commands |

---

## See also
- [[设计文档]] (design doc, Chinese original)
- [[工程日志]] (engineering log)
- [[游戏策划文档]] (LD game design doc)
- [[Hullward_UI开发规格_LD]] (v0.2 basis)
- [[Hullward_继续开发规划_LD]] (affix table §3 / station checklist §4 basis)
