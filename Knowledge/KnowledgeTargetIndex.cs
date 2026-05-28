using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdminQoL;

internal sealed class KnowledgeTargetIndex
{
    private static KnowledgeTargetIndex? _current;

    private readonly ObjectDB? _objectDb;
    private readonly int _itemCount;
    private readonly int _recipeCount;
    private readonly int _stationCount;
    private readonly int _pieceCount;
    private readonly Dictionary<string, ItemDrop> _itemsByPrefab;
    private readonly Dictionary<string, List<Recipe>> _recipesByItemPrefab;
    private readonly Dictionary<string, CraftingStation> _stationsByPrefab;
    private readonly Dictionary<string, List<Piece>> _piecesByPrefab;

    internal IReadOnlyList<Recipe> Recipes { get; }
    internal IReadOnlyList<ItemDrop> Items { get; }
    internal IReadOnlyList<CraftingStation> Stations { get; }
    internal IReadOnlyList<Piece> Pieces { get; }
    internal List<string> Options { get; }

    private KnowledgeTargetIndex(
        ObjectDB? objectDb,
        List<Recipe> recipes,
        List<ItemDrop> items,
        List<CraftingStation> stations,
        List<Piece> pieces)
    {
        _objectDb = objectDb;
        _itemCount = objectDb?.m_items?.Count ?? 0;
        _recipeCount = objectDb?.m_recipes?.Count ?? 0;
        _stationCount = stations.Count;
        _pieceCount = pieces.Count;
        Recipes = recipes;
        Items = items;
        Stations = stations;
        Pieces = pieces;
        _itemsByPrefab = new Dictionary<string, ItemDrop>(StringComparer.OrdinalIgnoreCase);
        _recipesByItemPrefab = new Dictionary<string, List<Recipe>>(StringComparer.OrdinalIgnoreCase);
        _stationsByPrefab = new Dictionary<string, CraftingStation>(StringComparer.OrdinalIgnoreCase);
        _piecesByPrefab = new Dictionary<string, List<Piece>>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> options = new(StringComparer.OrdinalIgnoreCase)
        {
            "all"
        };

        foreach (ItemDrop item in items)
        {
            AddItemKey(item.gameObject?.name, item, options);
        }

        foreach (Recipe recipe in recipes)
        {
            string? prefabName = recipe?.m_item?.gameObject?.name;
            AddOption(options, prefabName);
            string key = NormalizePrefabName(prefabName);
            if (key.Length == 0 || recipe == null)
            {
                continue;
            }

            if (!_recipesByItemPrefab.TryGetValue(key, out List<Recipe> itemRecipes))
            {
                itemRecipes = new List<Recipe>();
                _recipesByItemPrefab[key] = itemRecipes;
            }

            itemRecipes.Add(recipe);
        }

        foreach (CraftingStation station in stations)
        {
            AddStationKey(station.gameObject?.name, station, options);
        }

        foreach (Piece piece in pieces)
        {
            AddPieceKey(piece.gameObject?.name, piece, options);
        }

        Options = options.OrderBy(option => option).ToList();
    }

    internal static KnowledgeTargetIndex Current()
    {
        if (_current == null || !_current.IsCurrent())
        {
            _current = Build();
        }

        return _current;
    }

    internal static void Invalidate()
    {
        _current = null;
    }

    internal ItemDrop? FindItem(string target)
    {
        string key = NormalizePrefabName(target);
        if (key.Length > 0 && _itemsByPrefab.TryGetValue(key, out ItemDrop item))
        {
            return item;
        }

        GameObject? prefab = ObjectDB.instance?.GetItemPrefab(target);
        return prefab != null && prefab.TryGetComponent(out ItemDrop directItem) ? directItem : null;
    }

    internal IEnumerable<Recipe> FindRecipesByItem(string target)
    {
        string key = NormalizePrefabName(target);
        return key.Length > 0 && _recipesByItemPrefab.TryGetValue(key, out List<Recipe> recipes)
            ? recipes
            : Enumerable.Empty<Recipe>();
    }

    internal CraftingStation? FindStation(string target)
    {
        string key = NormalizePrefabName(target);
        return key.Length > 0 && _stationsByPrefab.TryGetValue(key, out CraftingStation station) ? station : null;
    }

    internal IEnumerable<Piece> FindPieces(string target)
    {
        string key = NormalizePrefabName(target);
        return key.Length > 0 && _piecesByPrefab.TryGetValue(key, out List<Piece> pieces)
            ? pieces
            : Enumerable.Empty<Piece>();
    }

    internal static bool MatchesPrefabName(string input, string? candidate)
    {
        string inputKey = NormalizePrefabName(input);
        string candidateKey = NormalizePrefabName(candidate);
        return inputKey.Length > 0
               && candidateKey.Length > 0
               && string.Equals(inputKey, candidateKey, StringComparison.OrdinalIgnoreCase);
    }

    private static KnowledgeTargetIndex Build()
    {
        ObjectDB? objectDb = ObjectDB.instance;
        List<Recipe> recipes = objectDb?.m_recipes?.Where(recipe => recipe != null).ToList() ?? new List<Recipe>();
        List<ItemDrop> items = new();
        if (objectDb?.m_items != null)
        {
            foreach (GameObject itemObject in objectDb.m_items)
            {
                ItemDrop? itemDrop = itemObject != null ? itemObject.GetComponent<ItemDrop>() : null;
                if (itemDrop != null)
                {
                    items.Add(itemDrop);
                }
            }
        }

        List<CraftingStation> stations = Resources.FindObjectsOfTypeAll<CraftingStation>()
            .Where(station => station != null)
            .ToList();
        List<Piece> pieces = new();
        foreach (PieceTable pieceTable in Resources.FindObjectsOfTypeAll<PieceTable>())
        {
            foreach (GameObject pieceObject in pieceTable.m_pieces)
            {
                Piece? piece = pieceObject != null ? pieceObject.GetComponent<Piece>() : null;
                if (piece != null)
                {
                    pieces.Add(piece);
                }
            }
        }

        return new KnowledgeTargetIndex(objectDb, recipes, items, stations, pieces);
    }

    private bool IsCurrent()
    {
        ObjectDB? objectDb = ObjectDB.instance;
        if (!ReferenceEquals(_objectDb, objectDb))
        {
            return false;
        }

        if ((objectDb?.m_items?.Count ?? 0) != _itemCount || (objectDb?.m_recipes?.Count ?? 0) != _recipeCount)
        {
            return false;
        }

        int stationCount = Resources.FindObjectsOfTypeAll<CraftingStation>().Count(station => station != null);
        if (stationCount != _stationCount)
        {
            return false;
        }

        int pieceCount = 0;
        foreach (PieceTable pieceTable in Resources.FindObjectsOfTypeAll<PieceTable>())
        {
            pieceCount += pieceTable.m_pieces.Count(pieceObject => pieceObject != null && pieceObject.GetComponent<Piece>() != null);
        }

        return pieceCount == _pieceCount;
    }

    private void AddItemKey(string? prefabName, ItemDrop item, HashSet<string> options)
    {
        AddOption(options, prefabName);
        string key = NormalizePrefabName(prefabName);
        if (key.Length > 0 && !_itemsByPrefab.ContainsKey(key))
        {
            _itemsByPrefab[key] = item;
        }
    }

    private void AddStationKey(string? prefabName, CraftingStation station, HashSet<string> options)
    {
        AddOption(options, prefabName);
        string key = NormalizePrefabName(prefabName);
        if (key.Length > 0 && !_stationsByPrefab.ContainsKey(key))
        {
            _stationsByPrefab[key] = station;
        }
    }

    private void AddPieceKey(string? prefabName, Piece piece, HashSet<string> options)
    {
        AddOption(options, prefabName);
        string key = NormalizePrefabName(prefabName);
        if (key.Length == 0)
        {
            return;
        }

        if (!_piecesByPrefab.TryGetValue(key, out List<Piece> pieces))
        {
            pieces = new List<Piece>();
            _piecesByPrefab[key] = pieces;
        }

        pieces.Add(piece);
    }

    private static void AddOption(HashSet<string> options, string? value)
    {
        string key = NormalizePrefabName(value);
        if (key.Length > 0)
        {
            options.Add(key);
        }
    }

    private static string NormalizePrefabName(string? value)
    {
        string normalized = value?.Trim() ?? "";
        return normalized.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase)
            ? normalized.Substring(0, normalized.Length - "(Clone)".Length)
            : normalized;
    }
}
