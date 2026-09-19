using Hullward.Domain.Loot;
using Hullward.Domain.Modules;
using Xunit;

namespace Hullward.Tests;

/// <summary>Sprint 5 迭代 16：模块品类命名系统（LD 命名映射 v0.1 §2-4）。</summary>
public class Sprint5ModuleNamingTests
{
    [Theory]
    [InlineData(ModuleCategory.PulseCannon, "Pulse Cannon")]
    [InlineData(ModuleCategory.RailGun, "Railgun")]
    [InlineData(ModuleCategory.LaserArray, "Laser Array")]
    [InlineData(ModuleCategory.Gatling, "Autocannon")]
    [InlineData(ModuleCategory.Composite, "Composite Armor")]
    [InlineData(ModuleCategory.Reactive, "Reactive Armor")]
    [InlineData(ModuleCategory.NanoCoating, "Nano Plating")]
    [InlineData(ModuleCategory.Phase, "Phase Armor")]
    [InlineData(ModuleCategory.FusionCore, "Fusion Core")]
    [InlineData(ModuleCategory.Capacitor, "Capacitor Cell")]
    [InlineData(ModuleCategory.Reactor, "Reactor")]
    [InlineData(ModuleCategory.EnergyNode, "Power Node")]
    [InlineData(ModuleCategory.Scanner, "Scanner")]
    [InlineData(ModuleCategory.Jammer, "Jammer")]
    [InlineData(ModuleCategory.WarpEngine, "Warp Engine")]
    [InlineData(ModuleCategory.EmergencyShield, "Emergency Shield")]
    public void CategoryNames_MatchLdTable(ModuleCategory category, string expected)
    {
        Assert.Equal(expected, ModuleNames.CategoryName(category));
    }

    [Theory]
    [InlineData(ItemRarity.Common, "")]
    [InlineData(ItemRarity.Magic, "Improved ")]
    [InlineData(ItemRarity.Rare, "Elite ")]
    [InlineData(ItemRarity.Set, "Ember ")]
    [InlineData(ItemRarity.Ancient, "Ancient ")]
    public void RarityPrefix_MatchLdTable(ItemRarity rarity, string expected)
    {
        Assert.Equal(expected, ModuleNames.RarityPrefix(rarity));
    }

    [Fact]
    public void ForSlot_ReturnsFourCategories_OfThatSlot()
    {
        Assert.Equal(4, ModuleNames.ForSlot(ModuleType.Weapon).Length);
        Assert.All(ModuleNames.ForSlot(ModuleType.Weapon), c => Assert.InRange((int)c, (int)ModuleCategory.PulseCannon, (int)ModuleCategory.Gatling));
        Assert.All(ModuleNames.ForSlot(ModuleType.Armor), c => Assert.InRange((int)c, (int)ModuleCategory.Composite, (int)ModuleCategory.Phase));
        Assert.All(ModuleNames.ForSlot(ModuleType.Power), c => Assert.InRange((int)c, (int)ModuleCategory.FusionCore, (int)ModuleCategory.EnergyNode));
        Assert.All(ModuleNames.ForSlot(ModuleType.Special), c => Assert.InRange((int)c, (int)ModuleCategory.Scanner, (int)ModuleCategory.EmergencyShield));
    }

    [Fact]
    public void RandomCategory_StaysInSlotPool()
    {
        var rng = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            foreach (var slot in new[] { ModuleType.Weapon, ModuleType.Armor, ModuleType.Power, ModuleType.Special })
            {
                var cat = ModuleNames.RandomCategory(slot, rng);
                Assert.Contains(cat, ModuleNames.ForSlot(slot));
            }
        }
    }

    [Fact]
    public void DisplayName_PrefixPlusCategory_NoLegacyClassName()
    {
        var drop = new ModuleDrop(ModuleType.Armor, ItemRarity.Rare) { Category = ModuleCategory.Reactive };
        Assert.Equal("Elite Reactive Armor", drop.DisplayName);
        Assert.DoesNotContain("Rare", drop.DisplayName);
        Assert.Contains("Reactive Armor", drop.DisplayName);
    }

    [Fact]
    public void DisplayName_Common_HasNoPrefix()
    {
        var drop = new ModuleDrop(ModuleType.Weapon, ItemRarity.Common) { Category = ModuleCategory.RailGun };
        Assert.Equal("Railgun", drop.DisplayName);
    }

    [Fact]
    public void DisplayName_Ancient_UsesTaiduPrefix()
    {
        var drop = new ModuleDrop(ModuleType.Power, ItemRarity.Ancient) { Category = ModuleCategory.FusionCore };
        Assert.Equal("Ancient Fusion Core", drop.DisplayName);
    }

    [Fact]
    public void DisplayName_LegacySave_DeterministicAndStable()
    {
        // 旧档无品类字段 → Category=null → 确定性 fallback：同一模块两次显示一致
        var a = new ModuleDrop(ModuleType.Special, ItemRarity.Magic);
        var b = new ModuleDrop(ModuleType.Special, ItemRarity.Magic);
        Assert.Equal(a.DisplayName, b.DisplayName);
        Assert.StartsWith("Improved ", a.DisplayName);
        // 品类必须来自特殊槽池
        Assert.Contains(ModuleNames.DeterministicFallback(a.Slot, a.Name), ModuleNames.ForSlot(ModuleType.Special));
    }

    [Fact]
    public void LegacyFallback_CoversAllSlots()
    {
        foreach (var slot in new[] { ModuleType.Weapon, ModuleType.Armor, ModuleType.Power, ModuleType.Special })
        {
            var cat = ModuleNames.DeterministicFallback(slot, "CommonWeapon模块");
            Assert.Contains(cat, ModuleNames.ForSlot(slot));
        }
    }

    [Fact]
    public void SaveRoundTrip_PreservesCategory()
    {
        var drop = new ModuleDrop(ModuleType.Power, ItemRarity.Set) { Category = ModuleCategory.Reactor };
        var data = Hullward.Domain.Save.SaveDataMapper.ToData(drop);
        var restored = Hullward.Domain.Save.SaveDataMapper.ToDomain(data);
        Assert.Equal(ModuleCategory.Reactor, restored.Category);
        Assert.Equal("Ember Reactor", restored.DisplayName);
    }

    [Fact]
    public void SaveRoundTrip_LegacyWithoutCategory_IsNullAndStable()
    {
        var data = new Hullward.Domain.Save.ModuleDropData
        {
            Slot = ModuleType.Weapon,
            Rarity = ItemRarity.Ancient,
            Category = -1
        };
        var restored = Hullward.Domain.Save.SaveDataMapper.ToDomain(data);
        Assert.Null(restored.Category);
        Assert.Equal(restored.DisplayName, restored.DisplayName); // 稳定
        Assert.StartsWith("Ancient ", restored.DisplayName);
    }
}
