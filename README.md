# Hullward (《深空暗骸》 / Deep Space Relics)

![Release](https://img.shields.io/badge/release-v0.7.1-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-5C2D91)
![Godot](https://img.shields.io/badge/Godot-4.7.2-478CBF)
![Tests](https://img.shields.io/badge/tests-228%20passed-brightgreen)
[![GitHub](https://img.shields.io/badge/GitHub-Bronardo%2FHullward-blue)](https://github.com/Bronardo/Hullward)

A top-down space-ship ARPG loot game — Deakin SIT771 "Something Awesome" 7.4H.

**Tech stack:** Godot 4.7.2 (.NET) + C# / .NET 8

![Combat screenshot](Hullward/docs/screenshots/04_combat_hud.png)

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
