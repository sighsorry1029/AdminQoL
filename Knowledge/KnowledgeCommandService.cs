using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdminQoL;

internal static class KnowledgeCommandService
{
    internal static KnowledgeCommandResult LearnAll(Player localPlayer)
    {
        KnowledgeTargetIndex index = KnowledgeTargetIndex.Current();
        int learnedRecipes = 0;
        int learnedPieces = 0;
        int learnedStations = 0;
        int learnedItems = 0;

        foreach (Recipe recipe in index.Recipes)
        {
            string? recipeName = recipe?.m_item?.m_itemData?.m_shared?.m_name;
            if (!string.IsNullOrWhiteSpace(recipeName) && localPlayer.m_knownRecipes.Add(recipeName))
            {
                learnedRecipes++;
            }
        }

        foreach (Piece piece in index.Pieces)
        {
            if (!string.IsNullOrWhiteSpace(piece.m_name) && localPlayer.m_knownRecipes.Add(piece.m_name))
            {
                learnedPieces++;
            }
        }

        foreach (CraftingStation station in index.Stations)
        {
            if (station == null || string.IsNullOrWhiteSpace(station.m_name))
            {
                continue;
            }

            int before = localPlayer.m_knownStations.TryGetValue(station.m_name, out int knownLevel) ? knownLevel : 0;
            localPlayer.AddKnownStation(station);
            int after = localPlayer.m_knownStations.TryGetValue(station.m_name, out int newLevel) ? newLevel : 0;
            if (after > before)
            {
                learnedStations++;
            }
        }

        foreach (ItemDrop itemDrop in index.Items)
        {
            if (itemDrop == null || string.IsNullOrWhiteSpace(itemDrop.m_itemData?.m_shared?.m_name))
            {
                continue;
            }

            learnedItems += SetItemAndTrophyDiscovery(localPlayer, itemDrop, learn: true);
        }

        localPlayer.UpdateKnownRecipesList();
        return KnowledgeCommandResult.Ok($"AdminQoL learned {learnedRecipes} recipes, {learnedPieces} build pieces, {learnedStations} station entries, and {learnedItems} item/trophy discoveries.");
    }

    internal static KnowledgeCommandResult ResetAll(Player localPlayer)
    {
        localPlayer.ResetCharacterKnownItems();
        return KnowledgeCommandResult.Ok("AdminQoL reset known recipes, stations, materials, and trophies.");
    }

    internal static KnowledgeCommandResult ApplyTarget(Player localPlayer, string target, bool learn)
    {
        KnowledgeTargetIndex index = KnowledgeTargetIndex.Current();
        CraftingStation? station = index.FindStation(target);
        if (station != null)
        {
            List<Recipe> recipes = FindRecipesForStation(index, station).ToList();
            int stationChanged = SetStationKnowledge(localPlayer, station, learn);
            int changed = SetRecipeKnowledge(localPlayer, recipes, learn);

            string stationName = BestStationName(station);
            string message = learn
                ? $"AdminQoL learned {changed}/{recipes.Count} recipes for crafting station '{stationName}' and {stationChanged} station entry."
                : $"AdminQoL unlearned {changed}/{recipes.Count} recipes for crafting station '{stationName}' and {stationChanged} station entry.";
            return KnowledgeCommandResult.Ok(message);
        }

        ItemDrop? itemDrop = index.FindItem(target);
        if (itemDrop != null)
        {
            List<Recipe> itemRecipes = index.FindRecipesByItem(target).ToList();
            int discoveryChanged = SetItemAndTrophyDiscovery(localPlayer, itemDrop, learn);
            int changed = SetRecipeKnowledge(localPlayer, itemRecipes, learn);

            string recipeNames = itemRecipes.Count == 0
                ? "none"
                : string.Join(", ", itemRecipes.Select(BestRecipeName).Distinct(StringComparer.OrdinalIgnoreCase));
            string itemName = BestItemName(itemDrop);
            string message = learn
                ? $"AdminQoL learned item discovery '{itemName}' ({discoveryChanged}) and {changed}/{itemRecipes.Count} recipe(s): {recipeNames}."
                : $"AdminQoL unlearned item discovery '{itemName}' ({discoveryChanged}) and {changed}/{itemRecipes.Count} recipe(s): {recipeNames}.";
            return KnowledgeCommandResult.Ok(message);
        }

        List<Piece> pieces = index.FindPieces(target).ToList();
        if (pieces.Count > 0)
        {
            int changed = SetPieceKnowledge(localPlayer, pieces, learn);
            if (learn)
            {
                localPlayer.UpdateKnownRecipesList();
            }

            string pieceNames = string.Join(", ", pieces.Select(BestPieceName).Distinct(StringComparer.OrdinalIgnoreCase));
            string message = learn
                ? $"AdminQoL learned {changed}/{pieces.Count} build piece(s): {pieceNames}."
                : $"AdminQoL unlearned {changed}/{pieces.Count} build piece(s): {pieceNames}.";
            return KnowledgeCommandResult.Ok(message);
        }

        return KnowledgeCommandResult.Error($"AdminQoL could not find an item recipe, crafting station, or build piece matching '{target}'.");
    }

    internal static List<string> GetTargetOptions()
    {
        return KnowledgeTargetIndex.Current().Options.ToList();
    }

    private static int SetRecipeKnowledge(Player player, IEnumerable<Recipe> recipes, bool learn)
    {
        int changed = 0;
        HashSet<string> processed = new(StringComparer.OrdinalIgnoreCase);
        foreach (Recipe recipe in recipes)
        {
            string? knownName = recipe?.m_item?.m_itemData?.m_shared?.m_name;
            if (string.IsNullOrWhiteSpace(knownName))
            {
                continue;
            }

            string recipeName = knownName!;
            if (!processed.Add(recipeName))
            {
                continue;
            }

            if (learn)
            {
                if (player.m_knownRecipes.Add(recipeName))
                {
                    changed++;
                }
            }
            else if (player.m_knownRecipes.Remove(recipeName))
            {
                changed++;
            }
        }

        return changed;
    }

    private static int SetPieceKnowledge(Player player, IEnumerable<Piece> pieces, bool learn)
    {
        int changed = 0;
        HashSet<string> processed = new(StringComparer.OrdinalIgnoreCase);
        foreach (Piece piece in pieces)
        {
            string? knownName = piece?.m_name;
            if (string.IsNullOrWhiteSpace(knownName))
            {
                continue;
            }

            string pieceName = knownName!;
            if (!processed.Add(pieceName))
            {
                continue;
            }

            if (learn)
            {
                if (player.m_knownRecipes.Add(pieceName))
                {
                    changed++;
                }
            }
            else if (player.m_knownRecipes.Remove(pieceName))
            {
                changed++;
            }
        }

        return changed;
    }

    private static int SetStationKnowledge(Player player, CraftingStation station, bool learn)
    {
        if (string.IsNullOrWhiteSpace(station.m_name))
        {
            return 0;
        }

        if (learn)
        {
            int before = player.m_knownStations.TryGetValue(station.m_name, out int knownLevel) ? knownLevel : 0;
            player.AddKnownStation(station);
            int after = player.m_knownStations.TryGetValue(station.m_name, out int newLevel) ? newLevel : 0;
            return after > before ? 1 : 0;
        }

        return player.m_knownStations.Remove(station.m_name) ? 1 : 0;
    }

    private static int SetItemAndTrophyDiscovery(Player player, ItemDrop itemDrop, bool learn)
    {
        string? sharedName = itemDrop.m_itemData?.m_shared?.m_name;
        if (string.IsNullOrWhiteSpace(sharedName))
        {
            return 0;
        }

        string itemName = sharedName!;
        if (learn)
        {
            int learnedEntries = player.m_knownMaterial.Add(itemName) ? 1 : 0;
            string? learnedTrophyKey = GetTrophyKey(itemDrop);
            if (!string.IsNullOrWhiteSpace(learnedTrophyKey) && player.m_trophies.Add(learnedTrophyKey))
            {
                learnedEntries++;
            }

            return learnedEntries;
        }

        int changed = player.m_knownMaterial.Remove(itemName) ? 1 : 0;
        string? trophyKey = GetTrophyKey(itemDrop);
        if (!string.IsNullOrWhiteSpace(trophyKey) && player.m_trophies.Remove(trophyKey))
        {
            changed++;
        }

        return changed;
    }

    private static string? GetTrophyKey(ItemDrop itemDrop)
    {
        if (itemDrop.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy)
        {
            return null;
        }

        if (itemDrop.m_itemData.m_dropPrefab != null)
        {
            return itemDrop.m_itemData.m_dropPrefab.name;
        }

        GameObject prefab = ObjectDB.instance.GetItemPrefab(itemDrop.m_itemData.m_shared);
        return prefab != null ? prefab.name : itemDrop.gameObject?.name;
    }

    private static IEnumerable<Recipe> FindRecipesForStation(KnowledgeTargetIndex index, CraftingStation station)
    {
        foreach (Recipe recipe in index.Recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            CraftingStation? recipeStation = recipe.m_craftingStation;
            if (recipeStation == null)
            {
                continue;
            }

            if (string.Equals(recipeStation.m_name, station.m_name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(recipeStation.gameObject?.name, station.gameObject?.name, StringComparison.OrdinalIgnoreCase))
            {
                yield return recipe;
            }
        }
    }

    private static string BestRecipeName(Recipe recipe)
    {
        return recipe.m_item?.gameObject?.name ?? recipe.m_item?.m_itemData?.m_shared?.m_name ?? "<unknown>";
    }

    private static string BestStationName(CraftingStation station)
    {
        return station.gameObject?.name ?? station.m_name ?? "<unknown>";
    }

    private static string BestItemName(ItemDrop itemDrop)
    {
        return itemDrop.gameObject?.name ?? itemDrop.m_itemData?.m_shared?.m_name ?? "<unknown>";
    }

    private static string BestPieceName(Piece piece)
    {
        return piece.gameObject?.name ?? piece.m_name ?? "<unknown>";
    }
}
