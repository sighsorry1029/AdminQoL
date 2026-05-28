using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ConsolePanel;
internal static class ConsoleCommandOwnership
{
    private static readonly Dictionary<string, string> Owners = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> CommonAliasTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "the", "mod", "mods", "plugin", "plugins", "valheim", "configuration", "config",
        "manager", "command", "commands", "data", "world", "expand", "json", "yaml", "dotnet", "detector"
    };
    private static int _terminalInitDepth;

    internal static int Version { get; private set; }

    internal static void EnterTerminalInit()
    {
        _terminalInitDepth++;
    }

    internal static void ExitTerminalInit()
    {
        _terminalInitDepth = Mathf.Max(0, _terminalInitDepth - 1);
    }

    internal static void MarkDirty()
    {
        Version++;
    }

    internal static void Record(global::Terminal.ConsoleCommand command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Command))
        {
            return;
        }

        string owner = DetectOwner(command.Command);
        if (!string.IsNullOrWhiteSpace(owner))
        {
            Owners[command.Command] = owner;
        }

        Version++;
    }

    internal static string GetOwner(string commandName)
    {
        if (!string.IsNullOrWhiteSpace(commandName) && Owners.TryGetValue(commandName, out string owner))
        {
            return owner;
        }

        return InferOwner(commandName);
    }

    private static string DetectOwner(string commandName)
    {
        string? explicitOwner = InferOwnerFromExplicitCommandName(commandName);
        if (!string.IsNullOrWhiteSpace(explicitOwner))
        {
            return explicitOwner!;
        }

        StackTrace stack = new(false);
        bool sawValheim = _terminalInitDepth > 0;
        Assembly thisAssembly = Assembly.GetExecutingAssembly();

        for (int i = 0; i < stack.FrameCount; i++)
        {
            MethodBase? method = stack.GetFrame(i)?.GetMethod();
            Assembly? assembly = method?.DeclaringType?.Assembly;
            if (assembly == null)
            {
                continue;
            }

            string assemblyName = assembly.GetName().Name ?? "";
            if (assemblyName.Equals("assembly_valheim", StringComparison.OrdinalIgnoreCase))
            {
                sawValheim = true;
                continue;
            }

            if (ReferenceEquals(assembly, thisAssembly) || ShouldSkipAssembly(assemblyName))
            {
                continue;
            }

            string? pluginName = TryFindPluginName(assembly);
            if (!string.IsNullOrWhiteSpace(pluginName))
            {
                if (!ShouldSkipOwner(pluginName!))
                {
                    return pluginName!;
                }

                continue;
            }

            return assemblyName;
        }

        string inferred = InferOwner(commandName);
        if (!string.Equals(inferred, "Other Mods", StringComparison.OrdinalIgnoreCase))
        {
            return inferred;
        }

        return sawValheim ? "Valheim" : inferred;
    }

    private static bool ShouldSkipAssembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return true;
        }

        string normalized = assemblyName.ToLowerInvariant();
        return normalized is "0harmony"
            or "bepinex"
            or "mscorlib"
            or "system"
            or "system.core"
            or "unityengine"
            or "unityengine.coremodule"
            or "jotunn";
    }

    private static bool ShouldSkipOwner(string owner)
    {
        string normalized = NormalizeIdentifier(owner);
        return normalized is "jotunn" or "valheimmoddingjotunn";
    }

    private static string? TryFindPluginName(Assembly assembly)
    {
        string location = SafeLocation(assembly);
        foreach (PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
        {
            BaseUnityPlugin? instance = pluginInfo.Instance;
            if (instance != null && ReferenceEquals(instance.GetType().Assembly, assembly))
            {
                return pluginInfo.Metadata.Name;
            }

            if (!string.IsNullOrWhiteSpace(location) && string.Equals(SafePath(pluginInfo.Location), location, StringComparison.OrdinalIgnoreCase))
            {
                return pluginInfo.Metadata.Name;
            }
        }

        return null;
    }

    private static string InferOwner(string commandName)
    {
        string command = commandName?.Trim().ToLowerInvariant() ?? "";
        if (command.Length == 0)
        {
            return "Other Mods";
        }

        string? explicitOwner = InferOwnerFromExplicitCommandName(command);
        if (!string.IsNullOrWhiteSpace(explicitOwner))
        {
            return explicitOwner!;
        }

        string? pluginOwner = InferOwnerFromLoadedPlugins(command);
        if (!string.IsNullOrWhiteSpace(pluginOwner))
        {
            return pluginOwner!;
        }

        return "Other Mods";
    }

    private static string? InferOwnerFromExplicitCommandName(string? commandName)
    {
        string command = commandName?.Trim().ToLowerInvariant() ?? "";
        if (command.Length == 0)
        {
            return null;
        }

        if (command.StartsWith("adminqol_", StringComparison.OrdinalIgnoreCase))
        {
            return "AdminQoL";
        }

        if (command.StartsWith("dns:", StringComparison.OrdinalIgnoreCase) || command.StartsWith("dns_", StringComparison.OrdinalIgnoreCase))
        {
            return "DropNSpawn";
        }

        if (command.StartsWith("jewelcrafting", StringComparison.OrdinalIgnoreCase))
        {
            return "Jewelcrafting";
        }

        if (command.StartsWith("epicloot", StringComparison.OrdinalIgnoreCase) || command.StartsWith("magic", StringComparison.OrdinalIgnoreCase))
        {
            return "Epic Loot";
        }

        if (command.StartsWith("vnei_", StringComparison.OrdinalIgnoreCase))
        {
            return "VNEI";
        }

        if (command.StartsWith("esp_", StringComparison.OrdinalIgnoreCase) || string.Equals(command, "esp", StringComparison.OrdinalIgnoreCase))
        {
            return "ESP";
        }

        if (command.StartsWith("ew_mus", StringComparison.OrdinalIgnoreCase) || command.StartsWith("ew_music", StringComparison.OrdinalIgnoreCase))
        {
            return "Expand World Music";
        }

        if (command.StartsWith("ew_event", StringComparison.OrdinalIgnoreCase) || command.StartsWith("expand_events", StringComparison.OrdinalIgnoreCase))
        {
            return "Expand World Events";
        }

        if (command.StartsWith("ew_faction", StringComparison.OrdinalIgnoreCase) || command.StartsWith("expand_factions", StringComparison.OrdinalIgnoreCase))
        {
            return "Expand World Factions";
        }

        if (command.StartsWith("ew_", StringComparison.OrdinalIgnoreCase) || command.StartsWith("expand_", StringComparison.OrdinalIgnoreCase))
        {
            return "Expand World Data";
        }

        if (command.StartsWith("hammer_", StringComparison.OrdinalIgnoreCase) || string.Equals(command, "hammer", StringComparison.OrdinalIgnoreCase))
        {
            return "Infinity Hammer";
        }

        if (command.StartsWith("world_edit", StringComparison.OrdinalIgnoreCase))
        {
            return "World Edit Commands";
        }

        if (command.StartsWith("xray", StringComparison.OrdinalIgnoreCase))
        {
            return "XRayVision";
        }

        return null;
    }

    private static string? InferOwnerFromLoadedPlugins(string commandName)
    {
        string command = commandName?.Trim().ToLowerInvariant() ?? "";
        if (command.Length == 0)
        {
            return null;
        }

        string normalizedCommand = NormalizeIdentifier(command);
        string? bestOwner = null;
        int bestLength = 0;
        foreach (PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
        {
            if (ShouldSkipOwner(pluginInfo.Metadata.Name))
            {
                continue;
            }

            foreach (string alias in GetPluginAliases(pluginInfo))
            {
                if (alias.Length <= bestLength)
                {
                    continue;
                }

                if (CommandStartsWithAlias(command, normalizedCommand, alias))
                {
                    bestOwner = pluginInfo.Metadata.Name;
                    bestLength = alias.Length;
                }
            }
        }

        return bestOwner;
    }

    private static bool CommandStartsWithAlias(string command, string normalizedCommand, string alias)
    {
        if (command.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
        {
            return command.Length == alias.Length || IsAliasBoundary(command[alias.Length]);
        }

        return normalizedCommand.StartsWith(alias, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAliasBoundary(char value)
    {
        return value is '_' or '-' or ':' or ' ' or '.';
    }

    private static IEnumerable<string> GetPluginAliases(PluginInfo pluginInfo)
    {
        HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
        AddAlias(pluginInfo.Metadata.Name);
        AddAlias(pluginInfo.Metadata.GUID);

        string location = pluginInfo.Location;
        if (!string.IsNullOrWhiteSpace(location))
        {
            AddAlias(Path.GetFileNameWithoutExtension(location));
            string? folderName = Path.GetFileName(Path.GetDirectoryName(location) ?? "");
            AddAlias(folderName);
            if (!string.IsNullOrWhiteSpace(folderName))
            {
                int separator = folderName!.IndexOf('-');
                if (separator >= 0 && separator + 1 < folderName.Length)
                {
                    AddAlias(folderName.Substring(separator + 1));
                }
            }
        }

        foreach (string alias in aliases)
        {
            yield return alias;
        }

        void AddAlias(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            string source = raw!;
            string normalized = NormalizeIdentifier(source);
            if (normalized.Length >= 3 && !CommonAliasTokens.Contains(normalized))
            {
                aliases.Add(normalized);
            }

            List<string> tokens = SplitIdentifierTokens(source)
                .Select(NormalizeIdentifier)
                .Where(token => token.Length >= 3 && !CommonAliasTokens.Contains(token))
                .ToList();
            foreach (string token in tokens)
            {
                aliases.Add(token);
            }

            string acronym = string.Concat(tokens.Select(token => token[0]));
            if (acronym.Length >= 2 && !CommonAliasTokens.Contains(acronym))
            {
                aliases.Add(acronym);
            }
        }
    }

    private static IEnumerable<string> SplitIdentifierTokens(string value)
    {
        return value
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string NormalizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string SafeLocation(Assembly assembly)
    {
        try
        {
            return SafePath(assembly.Location);
        }
        catch
        {
            return "";
        }
    }

    private static string SafePath(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);
        }
        catch
        {
            return path ?? "";
        }
    }
}
