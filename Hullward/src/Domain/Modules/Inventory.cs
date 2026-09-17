using System;
using System.Collections.Generic;
using Hullward.Domain.Loot;

namespace Hullward.Domain.Modules;

/// <summary>
/// 玩家背包（纯 C# 域层）：模块 + 合金。
/// 装配消耗模块；拆解回收合金。
/// </summary>
public sealed class Inventory
{
    public List<ModuleDrop> Modules { get; } = new();
    public int Alloy { get; private set; }

    public void AddModule(ModuleDrop module) => Modules.Add(module);

    public void AddAlloy(int amount) => Alloy += Math.Max(0, amount);

    /// <summary>消费合金（工坊/商店）。不足返回 false。</summary>
    public bool SpendAlloy(int amount)
    {
        if (amount < 0 || Alloy < amount)
        {
            return false;
        }
        Alloy -= amount;
        return true;
    }

    public bool TryRemoveModule(int index, out ModuleDrop? removed)
    {
        if (index < 0 || index >= Modules.Count)
        {
            removed = null;
            return false;
        }
        removed = Modules[index];
        Modules.RemoveAt(index);
        return true;
    }
}
