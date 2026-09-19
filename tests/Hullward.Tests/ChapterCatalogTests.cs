using Hullward.Domain.WorldGen;
using Xunit;

namespace Hullward.Tests.Domain;

public class ChapterCatalogTests
{
    [Theory]
    [InlineData(1, "Beacon", 1, 5)]
    [InlineData(2, "Starport", 2, 8)]
    [InlineData(3, "Deep Space", 3, 12)]
    [InlineData(4, "Collapse", 4, 16)]
    public void Get_ReturnsChapterData(int chapter, string name, int unlockLevel, int baseStrength)
    {
        var info = ChapterCatalog.Get(chapter);
        Assert.Equal(name, info.Name);
        Assert.Equal(unlockLevel, info.UnlockMothershipLevel);
        Assert.Equal(baseStrength, info.BaseStrength);
        Assert.True(info.HasBoss);
    }

    [Fact]
    public void Get_ClampsOutOfRange()
    {
        Assert.Equal(1, ChapterCatalog.Get(0).Chapter);
        Assert.Equal(1, ChapterCatalog.Get(-3).Chapter);
        Assert.Equal(ChapterCatalog.MaxChapter, ChapterCatalog.Get(9).Chapter);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    [InlineData(4, 4, true)]
    [InlineData(3, 4, false)]
    public void IsUnlocked_RequiresMothershipLevel(int mothershipLevel, int chapter, bool expected)
    {
        Assert.Equal(expected, ChapterCatalog.IsUnlocked(mothershipLevel, chapter));
    }

    [Fact]
    public void BaseStrength_MonotonicIncreasing()
    {
        for (int i = 1; i < ChapterCatalog.MaxChapter; i++)
        {
            Assert.True(ChapterCatalog.Get(i + 1).BaseStrength > ChapterCatalog.Get(i).BaseStrength);
        }
    }

    [Fact]
    public void StarMapGenerator_ChapterBase_MatchesCatalog()
    {
        for (int chapter = 1; chapter <= ChapterCatalog.MaxChapter; chapter++)
        {
            Assert.Equal(ChapterCatalog.Get(chapter).BaseStrength, StarMapGenerator.ChapterBase(chapter));
        }
    }
}
