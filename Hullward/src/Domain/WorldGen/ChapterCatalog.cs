namespace Hullward.Domain.WorldGen;

/// <summary>章节信息（UI 规格 v0.2 §5）：解锁条件 / 强度基准 / Boss 存在。</summary>
public sealed class ChapterInfo
{
    public int Chapter { get; init; }
    public string Name { get; init; } = "";
    public int UnlockMothershipLevel { get; init; }
    public bool HasBoss { get; init; }
    public int BaseStrength { get; init; }
}

/// <summary>
/// 章节目录（统一配置源）：章节随母舰等级解锁，每章强度基准递进。
/// 星图生成与波次生成均以本章数据为准。
/// </summary>
public static class ChapterCatalog
{
    public static readonly ChapterInfo[] Chapters =
    {
        new() { Chapter = 1, Name = "航标", UnlockMothershipLevel = 1, HasBoss = true, BaseStrength = 5 },
        new() { Chapter = 2, Name = "星港", UnlockMothershipLevel = 2, HasBoss = true, BaseStrength = 8 },
        new() { Chapter = 3, Name = "深空", UnlockMothershipLevel = 3, HasBoss = true, BaseStrength = 12 },
        new() { Chapter = 4, Name = "坍缩", UnlockMothershipLevel = 4, HasBoss = true, BaseStrength = 16 }
    };

    public const int MaxChapter = 4;

    public static ChapterInfo Get(int chapter) => chapter switch
    {
        <= 1 => Chapters[0],
        >= MaxChapter => Chapters[^1],
        _ => Chapters[chapter - 1]
    };

    /// <summary>章节是否已随母舰等级解锁。</summary>
    public static bool IsUnlocked(int mothershipLevel, int chapter)
        => mothershipLevel >= chapter;
}
