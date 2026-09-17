using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Hullward.Domain.Save;

/// <summary>主角名称校验（存档唯一标识）：≤12 字，禁系统非法文件字符。</summary>
public static class SaveNameValidator
{
    public const int MaxLength = 12;

    public static bool IsValid(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= MaxLength
        && !name.Any(Path.GetInvalidFileNameChars().Contains);
}

/// <summary>
/// 多文件制存档服务（UI 规格 v0.2 §3）：
/// 每个存档独立文件 `saves/&lt;主角名&gt;.json`，主角名为唯一标识。
/// </summary>
public sealed class SaveService
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SaveService(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public string SavePathFor(string name) => Path.Combine(_directory, $"{name}.json");

    /// <summary>写入（重名=覆盖，需调用方先检测）。</summary>
    public void Save(string name, SaveData data)
    {
        File.WriteAllText(SavePathFor(name), JsonSerializer.Serialize(data, Options));
    }

    public SaveData? Load(string name)
    {
        string path = SavePathFor(name);
        if (!File.Exists(path))
        {
            return null;
        }
        return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
    }

    /// <summary>列出全部存档名（文件名去扩展名，仅用于展示）。</summary>
    public List<string> List()
    {
        return Directory.EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n)
            .ToList()!;
    }

    public bool Exists(string name) => File.Exists(SavePathFor(name));

    /// <summary>删除存档（调用方需二次确认）。</summary>
    public bool Delete(string name)
    {
        string path = SavePathFor(name);
        if (!File.Exists(path))
        {
            return false;
        }
        File.Delete(path);
        return true;
    }
}
