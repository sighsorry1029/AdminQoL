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
internal static class EpicLootItemSetIntegration
{
    private const string AssemblyName = "EpicLoot";
    private static bool _warnedMissing;
    private static Assembly? _assembly;
    private static Type? _epicLootType;
    private static Type? _itemRarityType;
    private static Type? _magicItemType;
    private static Type? _magicItemEffectType;
    private static Type? _magicItemEffectDefinitionsType;
    private static Type? _lootRollerType;
    private static Type? _itemDataExtensionsType;
    private static Type? _uniqueLegendaryHelperType;
    private static MethodInfo? _canBeMagicItemMethod;
    private static MethodInfo? _rollMagicItemMethod;
    private static MethodInfo? _rollEffectMethod;
    private static MethodInfo? _getAvailableEffectsMethod;
    private static MethodInfo? _getEffectDefinitionMethod;
    private static MethodInfo? _saveMagicItemMethod;
    private static MethodInfo? _initializeMagicItemMethod;
    private static MethodInfo? _tryGetLegendaryInfoMethod;
    private static MethodInfo? _tryGetLegendarySetInfoMethod;
    private static MethodInfo? _getSetForLegendaryItemMethod;
    private static FieldInfo? _allDefinitionsField;
    private static FieldInfo? _legendaryInfoDictionaryField;
    private static FieldInfo? _mythicInfoDictionaryField;
    private static FieldInfo? _cheatForceLegendaryField;
    private static FieldInfo? _cheatForceMythicField;
    private static FieldInfo? _cheatDisableGatingField;

    internal static void Apply(string setName, string prefabName, ItemDrop.ItemData itemData, YamlEpicLootItem config)
    {
        if (!Resolve())
        {
            LogMissingOnce("EpicLoot is not loaded; skipped epicLoot block.");
            return;
        }

        try
        {
            if (_canBeMagicItemMethod != null && !((bool)_canBeMagicItemMethod.Invoke(null, new object[] { itemData })!))
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not apply EpicLoot data to '{prefabName}' because EpicLoot does not allow this item as a magic item.");
                return;
            }

            object rarity = ChooseRarity(config);
            object magicItem = ShouldUseEpicLootRoller(config) ? RollMagicItem(itemData, rarity, config) : BuildMagicItem(itemData, rarity, config, setName, prefabName);
            ApplyMagicItemFields(magicItem, config, setName, prefabName);
            _saveMagicItemMethod?.Invoke(null, new[] { itemData, magicItem });
            _initializeMagicItemMethod?.Invoke(null, new[] { itemData });
        }
        catch (Exception ex)
        {
            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' failed to apply EpicLoot data to '{prefabName}': {OptionalModReflection.UnwrapInvocation(ex).Message}");
        }
    }

    private static bool Resolve()
    {
        if (_magicItemType != null && _saveMagicItemMethod != null)
        {
            return true;
        }

        _assembly = OptionalModReflection.FindAssembly(AssemblyName);
        if (_assembly == null)
        {
            return false;
        }

        _epicLootType = OptionalModReflection.GetType(_assembly, "EpicLoot.EpicLoot");
        _itemRarityType = OptionalModReflection.GetType(_assembly, "EpicLoot.ItemRarity");
        _magicItemType = OptionalModReflection.GetType(_assembly, "EpicLoot.MagicItem");
        _magicItemEffectType = OptionalModReflection.GetType(_assembly, "EpicLoot.MagicItemEffect");
        _magicItemEffectDefinitionsType = OptionalModReflection.GetType(_assembly, "EpicLoot.MagicItemEffectDefinitions");
        _lootRollerType = OptionalModReflection.GetType(_assembly, "EpicLoot.LootRoller");
        _itemDataExtensionsType = OptionalModReflection.GetType(_assembly, "EpicLoot.ItemDataExtensions");
        _uniqueLegendaryHelperType = OptionalModReflection.GetType(_assembly, "EpicLoot.LegendarySystem.UniqueLegendaryHelper");
        _canBeMagicItemMethod = OptionalModReflection.GetPublicStaticMethod(_epicLootType, "CanBeMagicItem");
        _rollMagicItemMethod = _lootRollerType?.GetMethods(OptionalModReflection.PublicStatic)
            .FirstOrDefault(method => method.Name == "RollMagicItem" && method.GetParameters().Length == 4 && method.GetParameters()[0].ParameterType == _itemRarityType);
        _rollEffectMethod = _lootRollerType?.GetMethods(OptionalModReflection.PublicStatic)
            .FirstOrDefault(method => method.Name == "RollEffect" && method.GetParameters().Length == 4);
        _getAvailableEffectsMethod = OptionalModReflection.GetPublicStaticMethod(_magicItemEffectDefinitionsType, "GetAvailableEffects");
        _getEffectDefinitionMethod = OptionalModReflection.GetPublicStaticMethod(_magicItemEffectDefinitionsType, "Get");
        _saveMagicItemMethod = OptionalModReflection.GetPublicStaticMethod(_itemDataExtensionsType, "SaveMagicItem");
        _initializeMagicItemMethod = _lootRollerType?.GetMethod("InitializeMagicItem", OptionalModReflection.NonPublicStatic);
        _tryGetLegendaryInfoMethod = OptionalModReflection.GetPublicStaticMethod(_uniqueLegendaryHelperType, "TryGetLegendaryInfo");
        _tryGetLegendarySetInfoMethod = OptionalModReflection.GetPublicStaticMethod(_uniqueLegendaryHelperType, "TryGetLegendarySetInfo");
        _getSetForLegendaryItemMethod = OptionalModReflection.GetPublicStaticMethod(_uniqueLegendaryHelperType, "GetSetForLegendaryItem");
        _allDefinitionsField = OptionalModReflection.GetPublicStaticField(_magicItemEffectDefinitionsType, "AllDefinitions");
        _legendaryInfoDictionaryField = OptionalModReflection.GetPublicStaticField(_uniqueLegendaryHelperType, "LegendaryInfo");
        _mythicInfoDictionaryField = OptionalModReflection.GetPublicStaticField(_uniqueLegendaryHelperType, "MythicInfo");
        _cheatForceLegendaryField = OptionalModReflection.GetPublicStaticField(_lootRollerType, "CheatForceLegendary");
        _cheatForceMythicField = OptionalModReflection.GetPublicStaticField(_lootRollerType, "CheatForceMythic");
        _cheatDisableGatingField = OptionalModReflection.GetPublicStaticField(_lootRollerType, "CheatDisableGating");

        return _itemRarityType != null && _magicItemType != null && _magicItemEffectType != null && _saveMagicItemMethod != null;
    }

    private static bool ShouldUseEpicLootRoller(YamlEpicLootItem config)
    {
        return config.Effects.Count == 0 && !config.EffectCount.HasValue;
    }

    private static object RollMagicItem(ItemDrop.ItemData itemData, object rarity, YamlEpicLootItem config)
    {
        if (_rollMagicItemMethod == null)
        {
            return BuildMagicItem(itemData, rarity, config, "", itemData.m_shared.m_name);
        }

        object? previousLegendary = _cheatForceLegendaryField?.GetValue(null);
        object? previousMythic = _cheatForceMythicField?.GetValue(null);
        object? previousDisableGating = _cheatDisableGatingField?.GetValue(null);

        try
        {
            string rarityName = rarity.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(config.LegendaryId))
            {
                if (string.Equals(rarityName, "Mythic", StringComparison.OrdinalIgnoreCase))
                {
                    _cheatForceMythicField?.SetValue(null, config.LegendaryId);
                }
                else
                {
                    _cheatForceLegendaryField?.SetValue(null, config.LegendaryId);
                }

                _cheatDisableGatingField?.SetValue(null, true);
            }

            return _rollMagicItemMethod.Invoke(null, new object[] { rarity, itemData, 0f, 1f })!;
        }
        finally
        {
            _cheatForceLegendaryField?.SetValue(null, previousLegendary);
            _cheatForceMythicField?.SetValue(null, previousMythic);
            _cheatDisableGatingField?.SetValue(null, previousDisableGating);
        }
    }

    private static object BuildMagicItem(ItemDrop.ItemData itemData, object rarity, YamlEpicLootItem config, string setName, string prefabName)
    {
        object magicItem = Activator.CreateInstance(_magicItemType!)!;
        _magicItemType!.GetField("Rarity")?.SetValue(magicItem, rarity);
        IList effects = (IList)_magicItemType.GetField("Effects")!.GetValue(magicItem)!;

        foreach (YamlEpicLootEffect configuredEffect in config.Effects)
        {
            object? effect = CreateConfiguredEffect(itemData, magicItem, rarity, configuredEffect, setName, prefabName);
            if (effect != null)
            {
                effects.Add(effect);
            }
        }

        int targetEffectCount = config.EffectCount ?? effects.Count;
        targetEffectCount = Mathf.Clamp(targetEffectCount, 0, 32);
        while (effects.Count < targetEffectCount)
        {
            object? randomEffect = CreateRandomEffect(itemData, magicItem, rarity, setName, prefabName);
            if (randomEffect == null)
            {
                break;
            }

            effects.Add(randomEffect);
        }

        return magicItem;
    }

    private static object? CreateConfiguredEffect(ItemDrop.ItemData itemData, object magicItem, object rarity, YamlEpicLootEffect configuredEffect, string setName, string prefabName)
    {
        string effectType = configuredEffect.Type?.Trim() ?? "";
        object? effectDefinition = string.Equals(effectType, "random", StringComparison.OrdinalIgnoreCase)
            ? ChooseAvailableEffect(itemData, magicItem)
            : GetKnownEffectDefinition(effectType);

        if (effectDefinition == null)
        {
            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not resolve EpicLoot effect '{effectType}' for '{prefabName}'.");
            return null;
        }

        string resolvedType = GetEffectType(effectDefinition, effectType);
        if (ShouldRollEffectValue(configuredEffect.Value))
        {
            return RollEffect(effectDefinition, rarity);
        }

        float value = ParseEffectValue(configuredEffect.Value, setName, prefabName, resolvedType);
        return Activator.CreateInstance(_magicItemEffectType!, resolvedType, value);
    }

    private static object? CreateRandomEffect(ItemDrop.ItemData itemData, object magicItem, object rarity, string setName, string prefabName)
    {
        object? effectDefinition = ChooseAvailableEffect(itemData, magicItem);
        if (effectDefinition == null)
        {
            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not find any available EpicLoot random effects for '{prefabName}'.");
            return null;
        }

        return RollEffect(effectDefinition, rarity);
    }

    private static object? ChooseAvailableEffect(ItemDrop.ItemData itemData, object magicItem)
    {
        if (_getAvailableEffectsMethod == null)
        {
            return null;
        }

        object? available = _getAvailableEffectsMethod.Invoke(null, new object[] { itemData, magicItem, -1, true, false, false });
        if (available is not IEnumerable enumerable)
        {
            return null;
        }

        List<object> effects = enumerable.Cast<object>().ToList();
        return ChooseWeighted(effects, effect => Convert.ToSingle(effect.GetType().GetField("SelectionWeight")?.GetValue(effect) ?? 1f, CultureInfo.InvariantCulture));
    }

    private static object? GetKnownEffectDefinition(string effectType)
    {
        if (string.IsNullOrWhiteSpace(effectType))
        {
            return null;
        }

        if (_allDefinitionsField?.GetValue(null) is IDictionary definitions && !definitions.Contains(effectType))
        {
            string? matchedKey = definitions.Keys
                .Cast<object>()
                .OfType<string>()
                .FirstOrDefault(key => string.Equals(key, effectType, StringComparison.OrdinalIgnoreCase));
            if (matchedKey == null)
            {
                return null;
            }

            effectType = matchedKey;
        }

        return _getEffectDefinitionMethod?.Invoke(null, new object[] { effectType });
    }

    private static object? RollEffect(object effectDefinition, object rarity)
    {
        return _rollEffectMethod?.Invoke(null, new object?[] { effectDefinition, rarity, null, 1f });
    }

    private static bool ShouldRollEffectValue(object? value)
    {
        return value == null || value is string text && string.Equals(text.Trim(), "random", StringComparison.OrdinalIgnoreCase);
    }

    private static float ParseEffectValue(object? value, string setName, string prefabName, string effectType)
    {
        if (value == null)
        {
            return 1f;
        }

        try
        {
            return value switch
            {
                float f => f,
                double d => (float)d,
                int i => i,
                long l => l,
                string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) => parsed,
                string s when float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out float parsed) => parsed,
                _ => Convert.ToSingle(value, CultureInfo.InvariantCulture)
            };
        }
        catch
        {
            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not parse EpicLoot value '{value}' for effect '{effectType}' on '{prefabName}'; using 1.");
            return 1f;
        }
    }

    private static string GetEffectType(object effectDefinition, string fallback)
    {
        return effectDefinition.GetType().GetProperty("Type")?.GetValue(effectDefinition) as string ?? fallback;
    }

    private static void ApplyMagicItemFields(object magicItem, YamlEpicLootItem config, string setName, string prefabName)
    {
        string? setId = config.SetId?.Trim();
        if (string.IsNullOrWhiteSpace(setId))
        {
            setId = null;
        }
        string? displayName = config.DisplayName;
        string legendaryId = config.LegendaryId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(legendaryId))
        {
            object? legendaryInfo = TryGetEpicLootLegendaryInfo(legendaryId);
            if (legendaryInfo == null)
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' references EpicLoot legendaryId '{legendaryId}' for '{prefabName}', but EpicLoot has not loaded that legendary item.");
            }

            if (string.IsNullOrWhiteSpace(setId))
            {
                setId = GetEpicLootSetIdForLegendaryInfo(legendaryInfo);
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = GetStringField(legendaryInfo, "Name");
            }
        }

        if (!string.IsNullOrWhiteSpace(setId) && !TryGetEpicLootSetRarityName(setId, out _))
        {
            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' references EpicLoot setId '{setId}' for '{prefabName}', but EpicLoot has not loaded that legendary set.");
        }

        SetStringField(magicItem, "LegendaryID", legendaryId);
        SetStringField(magicItem, "SetID", setId);
        SetStringField(magicItem, "DisplayName", displayName);
        SetStringField(magicItem, "TypeNameOverride", config.TypeNameOverride);

        if (config.Unidentified.HasValue)
        {
            _magicItemType!.GetField("IsUnidentified")?.SetValue(magicItem, config.Unidentified.Value);
        }

        if (config.AugmentedEffectIndices.Count > 0 && _magicItemType!.GetField("AugmentedEffectIndices")?.GetValue(magicItem) is IList augmentedIndices)
        {
            augmentedIndices.Clear();
            foreach (int index in config.AugmentedEffectIndices.Where(index => index >= 0))
            {
                augmentedIndices.Add(index);
            }
        }
    }

    private static object? TryGetEpicLootLegendaryInfo(string legendaryId)
    {
        if (_tryGetLegendaryInfoMethod == null || string.IsNullOrWhiteSpace(legendaryId))
        {
            return null;
        }

        object?[] parameters = { legendaryId, null };
        bool found = _tryGetLegendaryInfoMethod.Invoke(null, parameters) is true;
        return found ? parameters[1] : null;
    }

    private static string? GetEpicLootSetIdForLegendaryInfo(object? legendaryInfo)
    {
        if (legendaryInfo == null || _getSetForLegendaryItemMethod == null)
        {
            return null;
        }

        return _getSetForLegendaryItemMethod.Invoke(null, new[] { legendaryInfo }) as string;
    }

    private static bool TryGetEpicLootSetRarityName(string? setId, out string rarityName)
    {
        rarityName = "";
        if (_tryGetLegendarySetInfoMethod == null || string.IsNullOrWhiteSpace(setId))
        {
            return false;
        }

        object?[] parameters = { setId, null, null };
        bool found = _tryGetLegendarySetInfoMethod.Invoke(null, parameters) is true;
        if (!found)
        {
            return false;
        }

        rarityName = parameters[2]?.ToString() ?? "";
        return !string.IsNullOrWhiteSpace(rarityName);
    }

    private static string? GetStringField(object? target, string fieldName)
    {
        return target?.GetType().GetField(fieldName, OptionalModReflection.PublicInstance)?.GetValue(target) as string;
    }

    private static void SetStringField(object target, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _magicItemType!.GetField(fieldName)?.SetValue(target, value);
        }
    }

    private static object ChooseRarity(YamlEpicLootItem config)
    {
        if (config.RarityWeights.Count > 0)
        {
            List<object> weightedRarities = new();
            Dictionary<object, float> weights = new();
            foreach (KeyValuePair<string, float> entry in config.RarityWeights)
            {
                if (entry.Value <= 0f || !TryParseRarity(entry.Key, out object rarity))
                {
                    continue;
                }

                weightedRarities.Add(rarity);
                weights[rarity] = entry.Value;
            }

            object? selected = ChooseWeighted(weightedRarities, rarity => weights[rarity]);
            if (selected != null)
            {
                return selected;
            }
        }

        string? rarityName = config.Rarity;
        string legendaryId = config.LegendaryId ?? "";
        if (string.IsNullOrWhiteSpace(rarityName) && !string.IsNullOrWhiteSpace(legendaryId))
        {
            rarityName = InferEpicLootLegendaryRarityName(legendaryId, config.SetId);
        }

        if (string.IsNullOrWhiteSpace(rarityName) && !string.IsNullOrWhiteSpace(legendaryId))
        {
            rarityName = "Legendary";
        }

        if (string.Equals(rarityName, "random", StringComparison.OrdinalIgnoreCase))
        {
            Array values = Enum.GetValues(_itemRarityType!);
            return values.GetValue(UnityEngine.Random.Range(0, values.Length))!;
        }

        return TryParseRarity(rarityName ?? "Magic", out object parsedRarity) ? parsedRarity : Enum.Parse(_itemRarityType!, "Magic", ignoreCase: true);
    }

    private static string? InferEpicLootLegendaryRarityName(string legendaryId, string? configuredSetId)
    {
        if (!string.IsNullOrWhiteSpace(configuredSetId) && TryGetEpicLootSetRarityName(configuredSetId, out string setRarity))
        {
            return setRarity;
        }

        object? legendaryInfo = TryGetEpicLootLegendaryInfo(legendaryId);
        string? inferredSetId = GetEpicLootSetIdForLegendaryInfo(legendaryInfo);
        if (TryGetEpicLootSetRarityName(inferredSetId, out setRarity))
        {
            return setRarity;
        }

        if (DictionaryContainsStringKey(_mythicInfoDictionaryField, legendaryId))
        {
            return "Mythic";
        }

        if (DictionaryContainsStringKey(_legendaryInfoDictionaryField, legendaryId))
        {
            return "Legendary";
        }

        return null;
    }

    private static bool DictionaryContainsStringKey(FieldInfo? dictionaryField, string key)
    {
        if (dictionaryField?.GetValue(null) is not IDictionary dictionary || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (dictionary.Contains(key))
        {
            return true;
        }

        return dictionary.Keys
            .Cast<object>()
            .OfType<string>()
            .Any(existingKey => string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseRarity(string rarityName, out object rarity)
    {
        rarity = null!;
        if (string.IsNullOrWhiteSpace(rarityName) || _itemRarityType == null)
        {
            return false;
        }

        try
        {
            rarity = Enum.Parse(_itemRarityType, rarityName.Trim(), ignoreCase: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? ChooseWeighted(IReadOnlyList<object> values, Func<object, float> getWeight)
    {
        if (values.Count == 0)
        {
            return null;
        }

        float totalWeight = values.Sum(value => Mathf.Max(0f, getWeight(value)));
        if (totalWeight <= 0f)
        {
            return values[UnityEngine.Random.Range(0, values.Count)];
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        foreach (object value in values)
        {
            roll -= Mathf.Max(0f, getWeight(value));
            if (roll <= 0f)
            {
                return value;
            }
        }

        return values[values.Count - 1];
    }

    private static void LogMissingOnce(string message)
    {
        if (_warnedMissing)
        {
            return;
        }

        _warnedMissing = true;
        AdminQoLPlugin.Log.LogWarning(message);
    }

}
