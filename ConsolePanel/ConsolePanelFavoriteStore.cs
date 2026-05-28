using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ConsolePanel;

internal sealed class ConsolePanelFavoriteStore
{
    private readonly List<HashSet<string>> _tabs = new();

    internal bool IsSaving { get; private set; }

    internal void LoadFromConfig()
    {
        _tabs.Clear();
        for (int i = 0; i < ConsolePanelLayout.MaxFavoriteTabs; i++)
        {
            HashSet<string> commands = new(StringComparer.OrdinalIgnoreCase);
            foreach (string command in SplitCommands(ConsolePanelModule.FavoriteTabCommands[i].Value))
            {
                commands.Add(command);
            }

            _tabs.Add(commands);
        }
    }

    internal void SaveToConfig()
    {
        EnsureStorage();
        IsSaving = true;
        try
        {
            ConsolePanelModule.UpdateConfigWithoutImmediateSave(() =>
            {
                bool changed = false;
                for (int i = 0; i < ConsolePanelLayout.MaxFavoriteTabs; i++)
                {
                    string serialized = string.Join("|", _tabs[i].OrderBy(command => command, StringComparer.OrdinalIgnoreCase));
                    if (string.Equals(ConsolePanelModule.FavoriteTabCommands[i].Value, serialized, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ConsolePanelModule.FavoriteTabCommands[i].Value = serialized;
                    changed = true;
                }

                return changed;
            });
        }
        finally
        {
            IsSaving = false;
        }
    }

    internal IEnumerable<string> GetCommands(int index)
    {
        EnsureStorage();
        return IsValidIndex(index) ? _tabs[index] : Enumerable.Empty<string>();
    }

    internal bool Toggle(int index, string command)
    {
        EnsureStorage();
        if (!IsValidIndex(index) || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (_tabs[index].Contains(command))
        {
            _tabs[index].Remove(command);
        }
        else
        {
            _tabs[index].Add(command);
        }

        return true;
    }

    internal bool Contains(int index, string command)
    {
        EnsureStorage();
        return IsValidIndex(index) && _tabs[index].Contains(command);
    }

    internal static bool IsValidIndex(int index)
    {
        return index >= 0 && index < ConsolePanelLayout.MaxFavoriteTabs;
    }

    internal static int GetVisibleTabCount()
    {
        return Mathf.Clamp(ConsolePanelModule.FavoriteTabCount.Value, 1, ConsolePanelLayout.MaxFavoriteTabs);
    }

    private void EnsureStorage()
    {
        while (_tabs.Count < ConsolePanelLayout.MaxFavoriteTabs)
        {
            _tabs.Add(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> SplitCommands(string value)
    {
        return (value ?? "")
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(command => command.Trim())
            .Where(command => command.Length > 0);
    }
}
