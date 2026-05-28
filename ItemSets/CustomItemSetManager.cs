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
    internal const string FileName = "AdminQoL.ItemSets.yml";
    internal const string FileWatcherFilter = "AdminQoL.ItemSets*.yml";
    private const string EpicLootFileName = "AdminQoL.ItemSets.EpicLoot.yml";
    private const string JewelcraftingFileName = "AdminQoL.ItemSets.Jewelcrafting.yml";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly object ApplyLock = new();
    private static string _configPath = "";
    private static string _filePath = "";
    private static string _epicLootFilePath = "";
    private static string _jewelcraftingFilePath = "";
    private static ItemSets? _snapshotSource;
    private static List<ItemSets.ItemSet>? _originalSets;
    private static List<string> _loadedSetNames = new();
    private static Dictionary<string, List<ItemSetItemModifier>> _itemModifiersBySetName = new(StringComparer.OrdinalIgnoreCase);

    [ThreadStatic]
    private static ActiveItemSetApplication? _activeApplication;

    internal static int LoadedSetCount => _loadedSetNames.Count;

    internal static void Initialize(string configPath)
    {
        _configPath = configPath;
        _filePath = Path.Combine(configPath, FileName);
        _epicLootFilePath = Path.Combine(configPath, EpicLootFileName);
        _jewelcraftingFilePath = Path.Combine(configPath, JewelcraftingFileName);
    }

    internal static List<string> GetLoadedSetNames()
    {
        return _loadedSetNames.OrderBy(name => name).ToList();
    }

    internal static void Reload()
    {
        lock (ApplyLock)
        {
            _loadedSetNames = new List<string>();
            _itemModifiersBySetName = new Dictionary<string, List<ItemSetItemModifier>>(StringComparer.OrdinalIgnoreCase);
        }

        ApplyYamlSetsToItemSets(ItemSets.instance);
    }

    internal static void ApplyYamlSetsToItemSets(ItemSets? itemSets)
    {
        if (!AdminQoLPlugin.ShouldLoadYamlItemSets() || itemSets == null || ObjectDB.instance == null)
        {
            return;
        }

        lock (ApplyLock)
        {
            if (_originalSets == null || !ReferenceEquals(_snapshotSource, itemSets))
            {
                _snapshotSource = itemSets;
                _originalSets = itemSets.m_sets.ToList();
            }

            itemSets.m_sets.Clear();
            itemSets.m_sets.AddRange(_originalSets);

            List<YamlItemSet> yamlSets = LoadConfig();

            List<string> loadedNames = new();
            Dictionary<string, List<ItemSetItemModifier>> modifiersBySetName = new(StringComparer.OrdinalIgnoreCase);

            foreach (YamlItemSet yamlSet in yamlSets)
            {
                if (yamlSet.Enabled == false)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(yamlSet.Name))
                {
                    AdminQoLPlugin.Log.LogWarning("Skipped an itemset with no name.");
                    continue;
                }

                if (yamlSet.ReplaceExisting == true)
                {
                    itemSets.m_sets.RemoveAll(set => string.Equals(set.m_name, yamlSet.Name, StringComparison.OrdinalIgnoreCase));
                }
                else if (itemSets.m_sets.Any(set => string.Equals(set.m_name, yamlSet.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    AdminQoLPlugin.Log.LogWarning($"Skipped YAML itemset '{yamlSet.Name}' because a set with that name already exists.");
                    continue;
                }

                ItemSets.ItemSet itemSet = BuildItemSet(yamlSet, out List<ItemSetItemModifier> itemModifiers);
                itemSets.m_sets.Add(itemSet);
                loadedNames.Add(itemSet.m_name);

                if (itemModifiers.Any(modifier => modifier.HasModData))
                {
                    modifiersBySetName[itemSet.m_name] = itemModifiers;
                }
            }

            _loadedSetNames = loadedNames;
            _itemModifiersBySetName = modifiersBySetName;
            AdminQoLPlugin.Log.LogInfo($"Loaded {_loadedSetNames.Count} YAML itemsets.");
        }
    }

    internal static ActiveItemSetApplication? BeginItemSetApplication(string setName)
    {
        if (string.IsNullOrWhiteSpace(setName))
        {
            return null;
        }

        List<ItemSetItemModifier>? modifiers;
        lock (ApplyLock)
        {
            if (!_itemModifiersBySetName.TryGetValue(setName, out modifiers) || modifiers.All(modifier => !modifier.HasModData))
            {
                return null;
            }

            modifiers = modifiers.ToList();
        }

        ActiveItemSetApplication application = new(setName, modifiers);
        _activeApplication = application;
        return application;
    }

    internal static void EndItemSetApplication(ActiveItemSetApplication? application)
    {
        if (application != null && ReferenceEquals(_activeApplication, application))
        {
            _activeApplication = null;
        }
    }

    internal static void ApplyItemModifiers(string prefabName, ItemDrop.ItemData itemData)
    {
        _activeApplication?.ApplyNext(prefabName, itemData);
    }




}
