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
internal static partial class CustomItemSetManager
{
    private static ItemSets.ItemSet BuildItemSet(YamlItemSet yamlSet, out List<ItemSetItemModifier> itemModifiers)
    {
        ItemSets.ItemSet itemSet = new()
        {
            m_name = yamlSet.Name
        };
        itemModifiers = new List<ItemSetItemModifier>();

        foreach (YamlSetItem yamlItem in yamlSet.Items)
        {
            ItemDrop? itemDrop = FindItemDrop(yamlItem.Prefab);
            if (itemDrop == null)
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{yamlSet.Name}' skipped missing item '{yamlItem.Prefab}'.");
                continue;
            }

            string resolvedPrefabName = GetItemPrefabName(itemDrop);
            itemSet.m_items.Add(new ItemSets.SetItem
            {
                m_item = itemDrop,
                m_quality = Math.Max(1, yamlItem.Quality),
                m_stack = Math.Max(1, yamlItem.Stack),
                m_use = yamlItem.Use,
                m_hotbarSlot = Mathf.Clamp(yamlItem.HotbarSlot, 0, 8)
            });
            itemModifiers.Add(new ItemSetItemModifier(yamlSet.Name, resolvedPrefabName, yamlItem.Jewelcrafting, yamlItem.EpicLoot));
        }

        foreach (YamlSetSkill yamlSkill in yamlSet.Skills)
        {
            if (!Enum.TryParse(yamlSkill.Skill, ignoreCase: true, out Skills.SkillType skillType) || skillType is Skills.SkillType.None)
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{yamlSet.Name}' skipped invalid skill '{yamlSkill.Skill}'.");
                continue;
            }

            itemSet.m_skills.Add(new ItemSets.SetSkill
            {
                m_skill = skillType,
                m_level = Mathf.Clamp(yamlSkill.Level, 0, 100)
            });
        }

        foreach (string stationName in yamlSet.KnownStations)
        {
            CraftingStation? station = FindCraftingStation(stationName);
            if (station == null)
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{yamlSet.Name}' skipped missing station '{stationName}'.");
                continue;
            }

            itemSet.m_knownStations.Add(station);
        }

        foreach (string itemName in yamlSet.KnownItems)
        {
            ItemDrop? itemDrop = FindItemDrop(itemName);
            if (itemDrop == null)
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{yamlSet.Name}' skipped missing known item '{itemName}'.");
                continue;
            }

            itemSet.m_knownItems.Add(itemDrop);
        }

        itemSet.m_inheritKnownFromItemSet.AddRange(yamlSet.InheritKnownFromItemSet.Where(name => !string.IsNullOrWhiteSpace(name)));
        return itemSet;
    }

    private static ItemDrop? FindItemDrop(string prefabOrSharedName)
    {
        if (ObjectDB.instance == null || string.IsNullOrWhiteSpace(prefabOrSharedName))
        {
            return null;
        }

        GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabOrSharedName);
        if (prefab != null && prefab.TryGetComponent(out ItemDrop itemDrop))
        {
            return itemDrop;
        }

        foreach (GameObject itemObject in ObjectDB.instance.m_items)
        {
            if (itemObject == null)
            {
                continue;
            }

            ItemDrop? candidate = itemObject.GetComponent<ItemDrop>();
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(itemObject.name, prefabOrSharedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.m_itemData.m_shared.m_name, prefabOrSharedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Localization.instance?.Localize(candidate.m_itemData.m_shared.m_name), prefabOrSharedName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static CraftingStation? FindCraftingStation(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (CraftingStation station in Resources.FindObjectsOfTypeAll<CraftingStation>())
        {
            if (station == null)
            {
                continue;
            }

            if (string.Equals(station.name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(station.gameObject.name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(station.m_name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Localization.instance?.Localize(station.m_name), name, StringComparison.OrdinalIgnoreCase))
            {
                return station;
            }
        }

        return null;
    }
}
