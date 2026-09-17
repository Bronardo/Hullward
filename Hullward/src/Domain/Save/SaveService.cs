using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;

namespace Hullward.Domain.Save;

/// <summary>存档 DTO（模块仅存槽位+品质，与具体数值解耦）。</summary>
public sealed class ModuleDropData
{
    public ModuleType Slot { get; set; }
    public ItemRarity Rarity { get; set; }
}

/// <summary>单机永久存档数据。</summary>
public sealed class SaveData
{
    public int ZoneLevel { get; set; } = 1;
    public int Alloy { get; set; }
    public int PlayerHull { get; set; } = 120;
    public int ModulesPicked { get; set; }
    public List<ModuleDropData> Modules { get; set; } = new();
}

/// <summary>JSON 存档服务（纯 C# 域层，System.Text.Json，可单测）。</summary>
public static class SaveService
{
    public static void Save(SaveData data, string path)
    {
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static SaveData? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SaveData>(json);
    }
}
