using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AdminQoL;
internal sealed class ActiveItemSetApplication
{
    private readonly string _setName;
    private readonly List<ItemSetItemModifier> _modifiers;
    private int _nextIndex;

    internal ActiveItemSetApplication(string setName, List<ItemSetItemModifier> modifiers)
    {
        _setName = setName;
        _modifiers = modifiers;
    }

    internal void ApplyNext(string prefabName, ItemDrop.ItemData itemData)
    {
        if (_nextIndex >= _modifiers.Count)
        {
            return;
        }

        int matchIndex = -1;
        for (int index = _nextIndex; index < _modifiers.Count; index++)
        {
            if (string.Equals(_modifiers[index].PrefabName, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                matchIndex = index;
                break;
            }
        }

        if (matchIndex < 0)
        {
            return;
        }

        ItemSetItemModifier modifier = _modifiers[matchIndex];
        _nextIndex = matchIndex + 1;

        if (!modifier.HasModData)
        {
            return;
        }

        OptionalItemSetModSupport.Apply(_setName, modifier, itemData);
    }
}
internal sealed class ItemSetItemModifier
{
    internal string SetName { get; }
    internal string PrefabName { get; }
    internal YamlJewelcraftingItem? Jewelcrafting { get; }
    internal YamlEpicLootItem? EpicLoot { get; }
    internal bool HasModData => Jewelcrafting != null || EpicLoot != null;

    internal ItemSetItemModifier(string setName, string prefabName, YamlJewelcraftingItem? jewelcrafting, YamlEpicLootItem? epicLoot)
    {
        SetName = setName;
        PrefabName = prefabName;
        Jewelcrafting = jewelcrafting;
        EpicLoot = epicLoot;
    }
}
internal sealed class YamlJewelcraftingItem
{
    public bool? SocketsLocked { get; set; }
    public bool? SocketSlotsLocked { get; set; }
    public List<string> Sockets { get; set; } = new();
}

internal sealed class YamlEpicLootItem
{
    public string? Rarity { get; set; }
    public Dictionary<string, float> RarityWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LegendaryId { get; set; }
    public string? SetId { get; set; }
    public string? DisplayName { get; set; }
    public string? TypeNameOverride { get; set; }
    public bool? Unidentified { get; set; }
    public int? EffectCount { get; set; }
    public List<YamlEpicLootEffect> Effects { get; set; } = new();
    public List<int> AugmentedEffectIndices { get; set; } = new();
}

internal sealed class YamlEpicLootEffect
{
    public string Type { get; set; } = "";
    public object? Value { get; set; }
}
