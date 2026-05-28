using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace AdminQoL;
[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
internal static class AdminQoLCommands
{
    private static void Postfix()
    {
        AddCommand("adminqol_learn", "Learns a prefab target item recipe/build piece/crafting station recipe group. Use 'all' to learn everything.", LearnCommand, GetLearnTargetOptions);
        AddCommand("adminqol_unlearn", "Unlearns a prefab target item recipe/build piece/crafting station recipe group. Use 'all' to reset known items.", UnlearnCommand, GetLearnTargetOptions);
        AddCommand("adminqol_clearinventory", "Permanently removes every item from the local player's inventory.", ClearInventory);
        AddCommand("adminqol_itemsets_reload", "Reloads AdminQoL.ItemSets.yml and reinjects custom itemsets.", ReloadItemSets);
        AddCommand("adminqol_itemsets_list", "Lists YAML itemsets currently loaded by AdminQoL.", ListItemSets, CustomItemSetManager.GetLoadedSetNames);
    }

    private static void AddCommand(string name, string description, Func<Terminal.ConsoleEventArgs, object> action, Terminal.ConsoleOptionsFetcher? options = null)
    {
        new Terminal.ConsoleCommand(name, description, args => AdminQoLPlugin.RequireAdmin(args, action), optionsFetcher: options, alwaysRefreshTabOptions: true);
    }

    private static object LearnCommand(Terminal.ConsoleEventArgs args)
    {
        Player localPlayer = Player.m_localPlayer;
        if (localPlayer == null || ObjectDB.instance == null)
        {
            return "Local player or ObjectDB is not ready.";
        }

        string target = GetCommandTarget(args);
        if (string.IsNullOrWhiteSpace(target))
        {
            return "Usage: adminqol_learn <item prefab|crafting station prefab|build piece prefab|all>";
        }

        if (IsAllTarget(target))
        {
            return CompleteKnowledgeCommand(args, KnowledgeCommandService.LearnAll(localPlayer));
        }

        return CompleteKnowledgeCommand(args, KnowledgeCommandService.ApplyTarget(localPlayer, target, learn: true));
    }

    private static object UnlearnCommand(Terminal.ConsoleEventArgs args)
    {
        Player localPlayer = Player.m_localPlayer;
        if (localPlayer == null || ObjectDB.instance == null)
        {
            return "Local player or ObjectDB is not ready.";
        }

        string target = GetCommandTarget(args);
        if (string.IsNullOrWhiteSpace(target))
        {
            return "Usage: adminqol_unlearn <item prefab|crafting station prefab|build piece prefab|all>";
        }

        if (IsAllTarget(target))
        {
            return CompleteKnowledgeCommand(args, KnowledgeCommandService.ResetAll(localPlayer));
        }

        return CompleteKnowledgeCommand(args, KnowledgeCommandService.ApplyTarget(localPlayer, target, learn: false));
    }


    private static object ClearInventory(Terminal.ConsoleEventArgs args)
    {
        Player localPlayer = Player.m_localPlayer;
        if (localPlayer == null)
        {
            return "Local player is not ready.";
        }

        Inventory inventory = localPlayer.GetInventory();
        int removedItems = inventory.NrOfItems();

        localPlayer.UnequipAllItems();
        inventory.RemoveAll();

        if (InventoryGui.instance != null)
        {
            InventoryGui.instance.m_playerGrid.UpdateInventory(inventory, localPlayer, null);
        }

        string message = $"AdminQoL removed {removedItems} inventory item entries.";
        AddMessage(args.Context, message);
        AdminQoLPlugin.Log.LogInfo(message);
        return true;
    }

    private static object ReloadItemSets(Terminal.ConsoleEventArgs args)
    {
        CustomItemSetManager.Reload();
        string message = $"AdminQoL loaded {CustomItemSetManager.LoadedSetCount} YAML itemsets.";
        AddMessage(args.Context, message);
        return true;
    }

    private static object ListItemSets(Terminal.ConsoleEventArgs args)
    {
        List<string> names = CustomItemSetManager.GetLoadedSetNames();
        args.Context.AddString(names.Count == 0 ? "AdminQoL has no YAML itemsets loaded." : "AdminQoL YAML itemsets: " + string.Join(", ", names));
        return true;
    }

    private static List<string> GetLearnTargetOptions()
    {
        return KnowledgeCommandService.GetTargetOptions();
    }

    private static string GetCommandTarget(Terminal.ConsoleEventArgs args)
    {
        return args.ArgsAll?.Trim() ?? "";
    }

    private static bool IsAllTarget(string target)
    {
        return string.Equals(target.Trim(), "all", StringComparison.OrdinalIgnoreCase);
    }

    private static object CompleteKnowledgeCommand(Terminal.ConsoleEventArgs args, KnowledgeCommandResult result)
    {
        if (!result.Success)
        {
            return result.Message;
        }

        AddMessage(args.Context, result.Message);
        AdminQoLPlugin.Log.LogInfo(result.Message);
        return true;
    }

    private static void AddMessage(Terminal context, string message)
    {
        context.AddString(message);
        if (Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, message);
        }
    }
}
