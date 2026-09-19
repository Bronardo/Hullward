# Hullward (《深空暗骸》 / Deep Space Relics)

A top-down space-ship ARPG loot game — Deakin SIT771 "Something Awesome" 7.4H.

**Tech stack:** Godot 4.7.2 (.NET) + C# / .NET 8

![Combat screenshot](Hullward/docs/screenshots/combat-sector4.png)

## Quick Start

```powershell
# Download the release zip from GitHub Releases, extract, and double-click Hullward.exe
# Or build from source:
dotnet build Hullward\Hullward.sln
dotnet test Hullward\Hullward.sln   # 228 tests
```

## Documentation

- [Full README](Hullward/README.md) — gameplay, controls, architecture
- [Design doc](Hullward/docs/design.md) — class diagram, scene tree, formulas
- [ULO evidence](Hullward/docs/ULO-evidence.md) — ULO1-5 evidence mapping

## Project Structure

```
├── Hullward/          # Godot project (source, scenes, assets, docs)
├── tests/             # xUnit test project
└── run_game.bat       # Dev launcher
```

## License

CC0 assets (Kenney / OpenGameArt / Joth).
