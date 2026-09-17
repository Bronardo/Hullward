using System;
using System.IO;
using System.Linq;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Save;
using Xunit;

namespace Hullward.Tests.Domain;

public class SaveServiceTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"hullward_saves_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllFields()
    {
        string dir = TempDir();
        try
        {
            var service = new SaveService(dir);
            var data = new SaveData
            {
                ZoneLevel = 3,
                Alloy = 42,
                PlayerHull = 88,
                ModulesPicked = 7
            };
            data.Modules.Add(new ModuleDropData { Slot = ModuleType.Weapon, Rarity = ItemRarity.Ancient });

            service.Save("舰长·零", data);
            SaveData? loaded = service.Load("舰长·零");

            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.ZoneLevel);
            Assert.Equal(42, loaded.Alloy);
            Assert.Equal(88, loaded.PlayerHull);
            Assert.Equal(7, loaded.ModulesPicked);
            Assert.Single(loaded.Modules);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void List_ReturnsAllSaveNames_IsolatedPerFile()
    {
        string dir = TempDir();
        try
        {
            var service = new SaveService(dir);
            service.Save("甲", new SaveData());
            service.Save("乙", new SaveData());

            var names = service.List();

            Assert.Equal(2, names.Count);
            Assert.Contains("甲", names);
            Assert.Contains("乙", names);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Delete_RemovesOnlyThatFile()
    {
        string dir = TempDir();
        try
        {
            var service = new SaveService(dir);
            service.Save("甲", new SaveData());
            service.Save("乙", new SaveData());

            Assert.True(service.Delete("甲"));
            Assert.False(service.Exists("甲"));
            Assert.True(service.Exists("乙"));
            Assert.Single(service.List());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_MissingName_ReturnsNull()
    {
        string dir = TempDir();
        try
        {
            var service = new SaveService(dir);
            Assert.Null(service.Load("不存在"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveNameValidator_ValidNames()
    {
        Assert.True(SaveNameValidator.IsValid("零"));
        Assert.True(SaveNameValidator.IsValid("舰长Zero"));
        Assert.True(SaveNameValidator.IsValid("123456789012")); // 恰好 12 字
    }

    [Fact]
    public void SaveNameValidator_RejectsInvalidNames()
    {
        Assert.False(SaveNameValidator.IsValid(""));
        Assert.False(SaveNameValidator.IsValid("   "));
        Assert.False(SaveNameValidator.IsValid("1234567890123")); // 超 12
        Assert.False(SaveNameValidator.IsValid("a/b"));
        Assert.False(SaveNameValidator.IsValid("a\\b"));
        Assert.False(SaveNameValidator.IsValid("a:b"));
        Assert.False(SaveNameValidator.IsValid("a*b"));
        Assert.False(SaveNameValidator.IsValid("a?b"));
    }
}
