using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;

namespace ConsolePanel;

internal static class ConsoleCommandCatalog
{
    internal static List<CommandEntry> CaptureVisibleCommands(global::Console? console)
    {
        List<CommandEntry> entries = new();
        foreach (KeyValuePair<string, global::Terminal.ConsoleCommand> pair in global::Terminal.commands)
        {
            global::Terminal.ConsoleCommand command = pair.Value;
            if (command == null || string.IsNullOrWhiteSpace(command.Command))
            {
                continue;
            }

            if (console != null)
            {
                try
                {
                    if (!command.ShowCommand(console))
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }
            }

            string owner = ConsoleCommandOwnership.GetOwner(command.Command);
            if (string.Equals(owner, "Other Mods", StringComparison.OrdinalIgnoreCase) && IsLikelyValheimCommand(command.Command))
            {
                owner = "Valheim";
            }

            entries.Add(new CommandEntry(command.Command, command.Description ?? "", owner));
        }

        AddCompatibilityCommands(entries);
        return entries;
    }

    internal static int OwnerSortWeight(string owner)
    {
        if (string.Equals(owner, "Valheim", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(owner, "AdminQoL", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(owner, "Other Mods", StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        return 10;
    }

    private static void AddCompatibilityCommands(List<CommandEntry> entries)
    {
        AddCreatureLevelControlCommands(entries);
    }

    private static void AddCreatureLevelControlCommands(List<CommandEntry> entries)
    {
        if (!IsPluginLoaded("org.bepinex.plugins.creaturelevelcontrol"))
        {
            return;
        }

        const string owner = "Creature Level & Loot Control";
        entries.Add(new CommandEntry("cllc", "Show Creature Level & Loot Control command help.", owner));
        entries.Add(new CommandEntry("cllc killall", "Remove all nearby creatures.", owner));
        entries.Add(new CommandEntry("cllc killhighstars", "Remove nearby creatures with at least the given stars. Optional: [stars], default 6.", owner));
        entries.Add(new CommandEntry("cllc worldlevel", "Display the current world level.", owner));
        entries.Add(new CommandEntry("cllc sectorkills", "Display creatures killed in the current sector.", owner));
        entries.Add(new CommandEntry("cllc resetsector", "Reset nearby creature sectors.", owner));
        entries.Add(new CommandEntry("cllc starchance", "Display current star chances. Optional: [creature].", owner));
        entries.Add(new CommandEntry("cllc spawn", "Spawn a creature. Args: [creature] [level] [affix] [extraeffect] [infusion].", owner));
    }

    private static bool IsPluginLoaded(string guid)
    {
        return Chainloader.PluginInfos.Values.Any(pluginInfo =>
            string.Equals(pluginInfo.Metadata.GUID, guid, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLikelyValheimCommand(string command)
    {
        string[] vanillaHints =
        {
            "help", "devcommands", "debugmode", "spawn", "itemset", "god", "fly", "ghost", "freefly", "save", "ping",
            "kick", "ban", "unban", "banned", "resetknownitems", "resetcharacter", "resetplayerprefs", "setkey",
            "removekey", "resetkeys", "players", "fov", "info", "gc"
        };
        return vanillaHints.Any(hint => string.Equals(command, hint, StringComparison.OrdinalIgnoreCase));
    }
}
