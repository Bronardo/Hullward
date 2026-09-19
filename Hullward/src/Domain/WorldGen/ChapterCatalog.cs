namespace Hullward.Domain.WorldGen;

/// <summary>sectorinfo（UI spec v0.2 §5）：unlock条件 / 强度benchmark / Boss 存在。</summary>
public sealed class ChapterInfo
{
    public int Chapter { get; init; }
    public string Name { get; init; } = "";
    public int UnlockMothershipLevel { get; init; }
    public bool HasBoss { get; init; }
    public int BaseStrength { get; init; }
}

/// <summary>
/// sector目录（统一config源）：sector随mothership levelunlock，每章强度benchmark递进。
/// starmap生成与wave生成均以本章data为准。
/// </summary>
public static class ChapterCatalog
{
    public static readonly ChapterInfo[] Chapters =
    {
        new() { Chapter = 1, Name = "Beacon", UnlockMothershipLevel = 1, HasBoss = true, BaseStrength = 5 },
        new() { Chapter = 2, Name = "Starport", UnlockMothershipLevel = 2, HasBoss = true, BaseStrength = 8 },
        new() { Chapter = 3, Name = "Deep Space", UnlockMothershipLevel = 3, HasBoss = true, BaseStrength = 12 },
        new() { Chapter = 4, Name = "Collapse", UnlockMothershipLevel = 4, HasBoss = true, BaseStrength = 16 }
    };

    public const int MaxChapter = 4;

    public static ChapterInfo Get(int chapter) => chapter switch
    {
        <= 1 => Chapters[0],
        >= MaxChapter => Chapters[^1],
        _ => Chapters[chapter - 1]
    };

    /// <summary>sector是否已随mothership levelunlock。</summary>
    public static bool IsUnlocked(int mothershipLevel, int chapter)
        => mothershipLevel >= chapter;
}
