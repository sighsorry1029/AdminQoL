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
    private static List<YamlItemSet> LoadConfig()
    {
        EnsureYamlFileFromVanillaSets();
        EnsureOptionalExampleFiles();

        List<YamlItemSet> itemSets = new();
        foreach (string path in GetConfigFilePaths())
        {
            try
            {
                string yaml = File.ReadAllText(path);
                YamlItemSetFile? file = Deserializer.Deserialize<YamlItemSetFile>(yaml);
                if (file?.ItemSets != null)
                {
                    itemSets.AddRange(file.ItemSets);
                }
            }
            catch (Exception ex)
            {
                AdminQoLPlugin.Log.LogError($"Failed to read {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        return itemSets;
    }

    private static List<string> GetConfigFilePaths()
    {
        if (string.IsNullOrWhiteSpace(_configPath) || !Directory.Exists(_configPath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(_configPath, FileWatcherFilter)
            .Where(ShouldLoadConfigFile)
            .OrderBy(path => string.Equals(Path.GetFileName(path), FileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldLoadConfigFile(string path)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, EpicLootFileName, StringComparison.OrdinalIgnoreCase))
        {
            return IsAssemblyLoaded("EpicLoot");
        }

        if (string.Equals(fileName, JewelcraftingFileName, StringComparison.OrdinalIgnoreCase))
        {
            return IsAssemblyLoaded("Jewelcrafting");
        }

        return true;
    }

    private static bool IsAssemblyLoaded(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureOptionalExampleFiles()
    {
        WriteFileIfMissing(_epicLootFilePath, BuildEpicLootExampleYaml());
        WriteFileIfMissing(_jewelcraftingFilePath, BuildJewelcraftingExampleYaml());
    }

    private static void WriteFileIfMissing(string path, string contents)
    {
        if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        AdminQoLPlugin.Log.LogInfo($"Created {Path.GetFileName(path)}.");
    }
}
