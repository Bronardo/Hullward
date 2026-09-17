using System.IO;
using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Hullward.Domain.Save;
using Xunit;

namespace Hullward.Tests.Domain;

public class SaveServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsAllFields()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hullward_save_{System.Guid.NewGuid():N}.json");
        try
        {
            var data = new SaveData
            {
                ZoneLevel = 3,
                Alloy = 42,
                PlayerHull = 88,
                ModulesPicked = 7
            };
            data.Modules.Add(new ModuleDropData { Slot = ModuleType.Weapon, Rarity = ItemRarity.Ancient });
            data.Modules.Add(new ModuleDropData { Slot = ModuleType.Armor, Rarity = ItemRarity.Rare });

            SaveService.Save(data, path);
            SaveData? loaded = SaveService.Load(path);

            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.ZoneLevel);
            Assert.Equal(42, loaded.Alloy);
            Assert.Equal(88, loaded.PlayerHull);
            Assert.Equal(7, loaded.ModulesPicked);
            Assert.Equal(2, loaded.Modules.Count);
            Assert.Equal(ItemRarity.Ancient, loaded.Modules[0].Rarity);
            Assert.Equal(ModuleType.Armor, loaded.Modules[1].Slot);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"hullward_none_{System.Guid.NewGuid():N}.json");
        Assert.Null(SaveService.Load(missing));
    }

    [Fact]
    public void Save_WritesValidJsonFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hullward_json_{System.Guid.NewGuid():N}.json");
        try
        {
            SaveService.Save(new SaveData { ZoneLevel = 4 }, path);
            string json = File.ReadAllText(path);
            Assert.Contains("\"ZoneLevel\": 4", json);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
