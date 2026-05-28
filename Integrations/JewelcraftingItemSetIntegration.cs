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
internal static class JewelcraftingItemSetIntegration
{
    private const string AssemblyName = "Jewelcrafting";
    private static bool _warnedMissing;
    private static Assembly? _assembly;
    private static Type? _apiType;
    private static Type? _gemInfoType;
    private static Type? _gemStoneSetupType;
    private static MethodInfo? _setGemsMethod;
    private static MethodInfo? _setSocketsLockMethod;
    private static MethodInfo? _setSocketSlotsLockMethod;
    private static FieldInfo? _gemsField;

    internal static void Apply(string setName, string prefabName, ItemDrop.ItemData itemData, YamlJewelcraftingItem config)
    {
        if (!Resolve())
        {
            LogMissingOnce("Jewelcrafting is not loaded; skipped jewelcrafting block.");
            return;
        }

        try
        {
            IList gems = CreateGemInfoList(setName, prefabName, config.Sockets);
            bool applied = (bool)(_setGemsMethod?.Invoke(null, new object[] { itemData, gems }) ?? false);
            if (!applied)
            {
                AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not apply Jewelcrafting sockets to '{prefabName}' because the item is not socketable.");
                return;
            }

            if (config.SocketsLocked.HasValue)
            {
                _setSocketsLockMethod?.Invoke(null, new object[] { itemData, config.SocketsLocked.Value });
            }

            if (config.SocketSlotsLocked.HasValue)
            {
                _setSocketSlotsLockMethod?.Invoke(null, new object[] { itemData, config.SocketSlotsLocked.Value });
            }
        }
        catch (Exception ex)
        {
            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' failed to apply Jewelcrafting sockets to '{prefabName}': {OptionalModReflection.UnwrapInvocation(ex).Message}");
        }
    }

    private static bool Resolve()
    {
        if (_setGemsMethod != null && _gemInfoType != null)
        {
            return true;
        }

        _assembly = OptionalModReflection.FindAssembly(AssemblyName);
        if (_assembly == null)
        {
            return false;
        }

        _apiType = OptionalModReflection.GetType(_assembly, "Jewelcrafting.API");
        _gemInfoType = OptionalModReflection.GetType(_assembly, "Jewelcrafting.API+GemInfo");
        _gemStoneSetupType = OptionalModReflection.GetType(_assembly, "Jewelcrafting.GemStoneSetup");
        _setGemsMethod = OptionalModReflection.GetPublicStaticMethod(_apiType, "SetGems");
        _setSocketsLockMethod = OptionalModReflection.GetPublicStaticMethod(_apiType, "SetSocketsLock");
        _setSocketSlotsLockMethod = OptionalModReflection.GetPublicStaticMethod(_apiType, "SetSocketSlotsLock");
        _gemsField = OptionalModReflection.GetPublicStaticField(_gemStoneSetupType, "Gems");

        return _apiType != null && _gemInfoType != null && _setGemsMethod != null;
    }

    private static IList CreateGemInfoList(string setName, string prefabName, IEnumerable<string> socketTokens)
    {
        Type listType = typeof(List<>).MakeGenericType(_gemInfoType!);
        IList gems = (IList)Activator.CreateInstance(listType)!;

        foreach (string token in socketTokens)
        {
            string? gemPrefab = ResolveSocketToken(setName, prefabName, token);
            gems.Add(string.IsNullOrEmpty(gemPrefab) ? null : CreateGemInfo(gemPrefab!));
        }

        return gems;
    }

    private static object CreateGemInfo(string gemPrefab)
    {
        Dictionary<string, float> effects = new();
        Dictionary<string, float[]> effectRanges = new();
        return Activator.CreateInstance(_gemInfoType!, gemPrefab, null, effects, effectRanges, null)!;
    }

    private static string? ResolveSocketToken(string setName, string prefabName, string token)
    {
        string value = token?.Trim() ?? "";
        if (value.Length == 0 || string.Equals(value, "empty", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        if (TryGetRandomTier(value, out int tier))
        {
            if (TryGetRandomTierGem(tier, out string randomGem))
            {
                return randomGem;
            }

            AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not find Jewelcrafting gems for socket token '{value}' on '{prefabName}'.");
            return "";
        }

        GameObject? prefab = ObjectDB.instance?.GetItemPrefab(value);
        if (prefab != null)
        {
            return prefab.name;
        }

        AdminQoLPlugin.Log.LogWarning($"Itemset '{setName}' could not find Jewelcrafting gem prefab '{value}' for '{prefabName}'; leaving that socket empty.");
        return "";
    }

    private static bool TryGetRandomTier(string token, out int tier)
    {
        tier = token.ToLowerInvariant() switch
        {
            "random_simple" => 0,
            "random_advanced" => 1,
            "random_perfect" => 2,
            _ => -1
        };
        return tier >= 0;
    }

    private static bool TryGetRandomTierGem(int tier, out string prefabName)
    {
        prefabName = "";
        if (_gemsField?.GetValue(null) is not IEnumerable gemsDictionary)
        {
            return false;
        }

        List<string> pool = new();
        foreach (object entry in gemsDictionary)
        {
            object? value = entry.GetType().GetProperty("Value")?.GetValue(entry);
            if (value is not IEnumerable gems)
            {
                continue;
            }

            List<object> tieredGems = gems.Cast<object>().ToList();
            if (tieredGems.Count < 3 || tier >= tieredGems.Count)
            {
                continue;
            }

            object gemDefinition = tieredGems[tier];
            if (gemDefinition.GetType().GetField("Prefab")?.GetValue(gemDefinition) is GameObject prefab && prefab != null)
            {
                pool.Add(prefab.name);
            }
        }

        if (pool.Count == 0)
        {
            return false;
        }

        prefabName = pool[UnityEngine.Random.Range(0, pool.Count)];
        return true;
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
