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
internal sealed partial class ConsolePanelController
{
    private void LoadFavorites()
    {
        _favorites.LoadFromConfig();
    }

    internal void ReloadFavoritesFromConfig()
    {
        LoadFavorites();
        if (!_visible)
        {
            return;
        }

        if (TryGetFavoriteIndex(_selectedOwner, out int favoriteIndex) && favoriteIndex >= GetFavoriteTabCount())
        {
            _selectedOwner = AllOwner;
        }

        RefreshCommandRows();
        RefreshCategoryRows();
        RefreshFavoriteProfileButtons();
    }

    internal void OnFavoriteConfigChanged()
    {
        if (_favorites.IsSaving)
        {
            return;
        }

        ReloadFavoritesFromConfig();
    }

    private void SaveFavorites()
    {
        _favorites.SaveToConfig();
    }

    private void ToggleFavorite(int targetIndex, string command)
    {
        if (!_favorites.Toggle(targetIndex, command))
        {
            return;
        }

        SaveFavorites();
        if (TryGetFavoriteIndex(_selectedOwner, out int selectedFavoriteIndex) && selectedFavoriteIndex == targetIndex)
        {
            RefreshCommandRows();
            return;
        }

        RefreshFavoriteActionButtons(command);
    }

    private bool IsFavoriteInTab(int index, string command)
    {
        return _favorites.Contains(index, command);
    }

    private static bool IsValidFavoriteIndex(int index)
    {
        return ConsolePanelFavoriteStore.IsValidIndex(index);
    }

    private static int GetFavoriteTabCount()
    {
        return ConsolePanelFavoriteStore.GetVisibleTabCount();
    }

    private static string FavoriteOwner(int index)
    {
        return $"{FavoriteOwnerPrefix}{index + 1}";
    }

    private static bool TryGetFavoriteIndex(string owner, out int index)
    {
        index = -1;
        if (!owner.StartsWith(FavoriteOwnerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = owner.Substring(FavoriteOwnerPrefix.Length);
        if (!int.TryParse(suffix, out int oneBased))
        {
            return false;
        }

        index = oneBased - 1;
        return IsValidFavoriteIndex(index);
    }

    private static bool IsBuiltInOwner(string owner)
    {
        return string.Equals(owner, AllOwner, StringComparison.OrdinalIgnoreCase) || TryGetFavoriteIndex(owner, out _);
    }

    private static string GetOwnerDisplayName(string owner)
    {
        if (TryGetFavoriteIndex(owner, out int favoriteIndex))
        {
            return $"Favorite {favoriteIndex + 1}";
        }

        return owner;
    }

}
