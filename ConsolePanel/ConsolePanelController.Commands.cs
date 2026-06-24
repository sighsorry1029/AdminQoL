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
    private void RefreshIfNeeded(bool force)
    {
        int commandCount = global::Terminal.commands.Count;
        if (!force && Time.unscaledTime < _nextRefresh)
        {
            return;
        }

        _nextRefresh = Time.unscaledTime + ConsolePanelLayout.CommandRefreshSeconds;
        if (!force && _lastOwnershipVersion == ConsoleCommandOwnership.Version && _lastCommandCount == commandCount)
        {
            return;
        }

        _lastOwnershipVersion = ConsoleCommandOwnership.Version;
        _lastCommandCount = commandCount;
        RebuildCommandModel();
        RefreshFavoriteProfileButtons();
        RefreshCategoryRows();
        RefreshCommandRows();
    }

    private void RebuildCommandModel()
    {
        _commands.Clear();
        _allRows.Clear();
        _commandsByOwner.Clear();
        _commandsByName.Clear();
        _owners.Clear();

        foreach (CommandEntry entry in ConsoleCommandCatalog.CaptureVisibleCommands(global::Console.instance))
        {
            AddCommandEntry(entry);
        }

        foreach (List<CommandEntry> list in _commandsByOwner.Values)
        {
            list.Sort((left, right) => string.Compare(left.Command, right.Command, StringComparison.OrdinalIgnoreCase));
        }

        _owners.AddRange(_commandsByOwner.Keys.OrderBy(ConsoleCommandCatalog.OwnerSortWeight).ThenBy(owner => owner, StringComparer.OrdinalIgnoreCase));
        _allRows.AddRange(_commands
            .OrderBy(entry => ConsoleCommandCatalog.OwnerSortWeight(entry.Owner))
            .ThenBy(entry => entry.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase));
        ConsolePanelModule.Diagnostic($"captured {_commands.Count} visible commands in {_owners.Count} groups: {string.Join(", ", _owners.Select(owner => $"{owner}={_commandsByOwner[owner].Count}"))}");
        if (!IsBuiltInOwner(_selectedOwner) && !_owners.Contains(_selectedOwner, StringComparer.OrdinalIgnoreCase))
        {
            _selectedOwner = _owners.FirstOrDefault() ?? AllOwner;
            ConsolePanelModule.Diagnostic($"selected owner changed to '{_selectedOwner}' after rebuild");
        }
    }

    private void AddCommandEntry(CommandEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Command) || _commandsByName.ContainsKey(entry.Command))
        {
            return;
        }

        _commands.Add(entry);
        _commandsByName[entry.Command] = entry;
        if (!_commandsByOwner.TryGetValue(entry.Owner, out List<CommandEntry> list))
        {
            list = new List<CommandEntry>();
            _commandsByOwner[entry.Owner] = list;
        }

        list.Add(entry);
    }

    private void RefreshCategoryRows()
    {
        if (_categoryContent == null)
        {
            return;
        }

        ClearChildren(_categoryContent);
        foreach (string owner in GetCategoryDisplayOrder())
        {
            AddCategoryButton(owner, owner);
        }

        void AddCategoryButton(string label, string owner)
        {
            string capturedOwner = owner;
            Button button = CreateTabButton(_categoryContent, label, () =>
            {
                ConsolePanelModule.Diagnostic($"category clicked: '{capturedOwner}'");
                _selectedOwner = string.Equals(_selectedOwner, capturedOwner, StringComparison.OrdinalIgnoreCase)
                    ? AllOwner
                    : capturedOwner;
                RefreshCommandRows();
                RefreshCategoryRows();
                RefreshFavoriteProfileButtons();
            });
            ApplySelectedButtonColor(button, string.Equals(owner, _selectedOwner, StringComparison.OrdinalIgnoreCase));
        }
    }

    private IEnumerable<string> GetCategoryDisplayOrder()
    {
        foreach (string owner in _owners)
        {
            yield return owner;
        }
    }

    private void RefreshFavoriteProfileButtons()
    {
        if (_favoriteProfileContent == null)
        {
            return;
        }

        ClearChildren(_favoriteProfileContent);
        for (int i = 0; i < GetFavoriteTabCount(); i++)
        {
            int capturedIndex = i;
            Button button = CreateCompactButton(_favoriteProfileContent, (i + 1).ToString(), () =>
            {
                string favoriteOwner = FavoriteOwner(capturedIndex);
                _selectedOwner = string.Equals(_selectedOwner, favoriteOwner, StringComparison.OrdinalIgnoreCase)
                    ? AllOwner
                    : favoriteOwner;
                RefreshCommandRows();
                RefreshCategoryRows();
                RefreshFavoriteProfileButtons();
            });
            ApplySelectedButtonColor(button, string.Equals(_selectedOwner, FavoriteOwner(i), StringComparison.OrdinalIgnoreCase));
        }
    }

    private void RefreshCommandRows()
    {
        if (_commandContent == null)
        {
            return;
        }

        if (_commandContent is not RectTransform contentRect)
        {
            return;
        }

        _favoriteActionBindingsByCommand.Clear();
        List<CommandEntry> rows = ApplySearch(GetSelectedRows());
        string renderKey = _selectedOwner + "|" + _searchText;
        bool resetScroll = !string.Equals(_lastRenderedOwner, renderKey, StringComparison.OrdinalIgnoreCase);
        string preview = rows.Count == 0 ? "" : $", preview={string.Join(", ", rows.Take(ConsolePanelLayout.CommandPreviewCount).Select(row => row.Command))}";
        ConsolePanelModule.Diagnostic($"refresh command rows: selected='{_selectedOwner}', rows={rows.Count}, contentActive={_commandContent.gameObject.activeInHierarchy}{preview}");
        if (_headerLabel != null)
        {
            _headerLabel.text = $"{GetOwnerDisplayName(_selectedOwner)} commands ({rows.Count})";
        }

        _renderedCommandRows.Clear();
        _renderedCommandRows.AddRange(rows);
        SetVirtualCommandContentHeight(rows.Count);

        if (rows.Count == 0)
        {
            string hint = TryGetFavoriteIndex(_selectedOwner, out _)
                ? "No favorite commands yet. Use row number buttons to add commands to this profile."
                : "No visible commands were captured for this group.";
            ShowVirtualInfoRow(hint);
            HidePooledCommandRows();
            if (resetScroll)
            {
                ResetScrollToTop(_commandScrollRect, contentRect);
            }

            _lastRenderedOwner = renderKey;
            return;
        }

        HideVirtualInfoRow();
        if (resetScroll)
        {
            ResetScrollToTop(_commandScrollRect, contentRect);
        }

        ClampCommandScrollPosition(contentRect);
        UpdateVirtualCommandRows(force: true);
        _lastRenderedOwner = renderKey;
    }

    private void SetVirtualCommandContentHeight(int rowCount)
    {
        if (_commandContent is not RectTransform contentRect)
        {
            return;
        }

        float rowHeight = ConsolePanelLayout.RowHeight;
        float height = rowCount <= 0
            ? rowHeight + VirtualCommandPadding * 2f
            : VirtualCommandPadding * 2f + rowCount * rowHeight + Mathf.Max(0, rowCount - 1) * VirtualCommandSpacing;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, height);
    }

    private void UpdateVirtualCommandRows(bool force = false)
    {
        if (_commandContent is not RectTransform contentRect || _commandScrollRect?.viewport == null)
        {
            return;
        }

        if (_renderedCommandRows.Count == 0)
        {
            HidePooledCommandRows();
            _lastVirtualFirstIndex = -1;
            _lastVirtualRowCount = -1;
            return;
        }

        HideVirtualInfoRow();
        ClampCommandScrollPosition(contentRect);

        float rowHeight = ConsolePanelLayout.RowHeight;
        float stride = rowHeight + VirtualCommandSpacing;
        float scrollY = Mathf.Max(0f, contentRect.anchoredPosition.y);
        float viewportHeight = Mathf.Max(1f, _commandScrollRect.viewport.rect.height);
        int firstIndex = Mathf.Clamp(Mathf.FloorToInt((scrollY - VirtualCommandPadding) / stride) - ConsolePanelLayout.VirtualCommandLookBehindRows, 0, _renderedCommandRows.Count - 1);
        int visibleCapacity = Mathf.CeilToInt(viewportHeight / stride) + ConsolePanelLayout.VirtualCommandLookAheadRows;
        int visibleCount = Mathf.Clamp(visibleCapacity, 0, _renderedCommandRows.Count - firstIndex);

        if (!force && firstIndex == _lastVirtualFirstIndex && visibleCount == _lastVirtualRowCount)
        {
            return;
        }

        EnsureCommandRowPool(visibleCount);
        _favoriteActionBindingsByCommand.Clear();
        for (int i = 0; i < _commandRowPool.Count; i++)
        {
            CommandRowView row = _commandRowPool[i];
            if (i >= visibleCount)
            {
                row.SetActive(false);
                continue;
            }

            int rowIndex = firstIndex + i;
            row.SetActive(true);
            PositionVirtualCommandRow(row.RectTransform, rowIndex);
            row.Bind(_renderedCommandRows[rowIndex]);
        }

        _lastVirtualFirstIndex = firstIndex;
        _lastVirtualRowCount = visibleCount;
    }

    private void EnsureCommandRowPool(int visibleCount)
    {
        if (_commandContent == null)
        {
            return;
        }

        while (_commandRowPool.Count < visibleCount)
        {
            _commandRowPool.Add(new CommandRowView(this, _commandContent));
        }
    }

    private static void PositionVirtualCommandRow(RectTransform rowRect, int rowIndex)
    {
        float top = -(VirtualCommandPadding + rowIndex * (ConsolePanelLayout.RowHeight + VirtualCommandSpacing));
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(VirtualCommandPadding, top - ConsolePanelLayout.RowHeight);
        rowRect.offsetMax = new Vector2(-VirtualCommandPadding, top);
    }

    private void ClampCommandScrollPosition(RectTransform contentRect)
    {
        if (_commandScrollRect?.viewport == null)
        {
            return;
        }

        float maxY = Mathf.Max(0f, contentRect.rect.height - _commandScrollRect.viewport.rect.height);
        Vector2 position = contentRect.anchoredPosition;
        float clampedY = Mathf.Clamp(position.y, 0f, maxY);
        if (!Mathf.Approximately(position.y, clampedY))
        {
            contentRect.anchoredPosition = new Vector2(position.x, clampedY);
        }
    }

    private void HidePooledCommandRows()
    {
        foreach (CommandRowView row in _commandRowPool)
        {
            row.SetActive(false);
        }

        _favoriteActionBindingsByCommand.Clear();
    }

    private void ShowVirtualInfoRow(string text)
    {
        if (_commandContent is not RectTransform contentRect)
        {
            return;
        }

        if (_emptyCommandRow == null)
        {
            GameObject row = CreateUiObject("InfoRow", contentRect);
            _emptyCommandRow = row.GetComponent<RectTransform>();
            Image image = row.AddComponent<Image>();
            ApplySurfaceImage(image, SurfaceRole.MutedInfo, new Color(0f, 0f, 0f, 0.18f));

            GameObject labelObject = CreateUiObject("Label", row.transform);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);
            EnsureTmpDefaultFont();
            _emptyCommandLabel = labelObject.AddComponent<TextMeshProUGUI>();
            _emptyCommandLabel.font = _font;
            _emptyCommandLabel.fontSize = 14f;
            _emptyCommandLabel.color = ButtonTextColor();
            _emptyCommandLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _emptyCommandLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _emptyCommandLabel.overflowMode = TextOverflowModes.Ellipsis;
            _emptyCommandLabel.raycastTarget = false;
        }

        _emptyCommandRow.gameObject.SetActive(true);
        PositionVirtualCommandRow(_emptyCommandRow, 0);
        if (_emptyCommandLabel != null)
        {
            _emptyCommandLabel.text = text;
        }
    }

    private void HideVirtualInfoRow()
    {
        if (_emptyCommandRow != null)
        {
            _emptyCommandRow.gameObject.SetActive(false);
        }
    }

    private static void ResetScrollToTop(ScrollRect? scrollRect, RectTransform contentRect)
    {
        contentRect.anchoredPosition = Vector2.zero;
        if (scrollRect == null)
        {
            return;
        }

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private RectTransform CreateHeaderLabel(RectTransform parent, out TextMeshProUGUI label)
    {
        GameObject root = CreateUiObject("CommandHeader", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Image image = root.AddComponent<Image>();
        ApplySurfaceImage(image, SurfaceRole.Header, new Color(0.16f, 0.08f, 0.04f, 0.90f));

        GameObject labelObject = CreateUiObject("Label", root.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(10f, 2f);
        labelRect.offsetMax = new Vector2(-10f, -2f);
        EnsureTmpDefaultFont();
        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = "Commands";
        label.fontSize = 15f;
        label.color = new Color(1f, 0.86f, 0.36f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return rootRect;
    }

    private List<CommandEntry> GetSelectedRows()
    {
        if (string.Equals(_selectedOwner, AllOwner, StringComparison.OrdinalIgnoreCase))
        {
            return _allRows;
        }

        if (TryGetFavoriteIndex(_selectedOwner, out int favoriteIndex))
        {
            return _favorites.GetCommands(favoriteIndex)
                .OrderBy(command => command, StringComparer.OrdinalIgnoreCase)
                .Select(command => ResolveFavoriteCommand(command, favoriteIndex))
                .ToList();
        }

        return _commandsByOwner.TryGetValue(_selectedOwner, out List<CommandEntry> rows)
            ? rows
            : new List<CommandEntry>();
    }

    private List<CommandEntry> ApplySearch(List<CommandEntry> rows)
    {
        string query = _searchText.Trim();
        if (query.Length == 0)
        {
            return rows;
        }

        string[] terms = query
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim())
            .Where(term => term.Length > 0)
            .ToArray();
        if (terms.Length == 0)
        {
            return rows;
        }

        return rows
            .Where(entry => terms.All(term =>
                entry.Command.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Owner.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();
    }

    private CommandEntry ResolveFavoriteCommand(string command, int favoriteIndex)
    {
        if (_commandsByName.TryGetValue(command, out CommandEntry entry))
        {
            return entry;
        }

        return new CommandEntry(command, "", FavoriteOwner(favoriteIndex));
    }

    private static string FormatCommandRowText(CommandEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Description)
            ? entry.Command
            : $"{entry.Command}  <color=#d9d1b8>{entry.Description}</color>";
    }

    private void RegisterFavoriteActionButton(string command, int favoriteIndex, Button button)
    {
        if (!_favoriteActionBindingsByCommand.TryGetValue(command, out List<FavoriteActionBinding> bindings))
        {
            bindings = new List<FavoriteActionBinding>();
            _favoriteActionBindingsByCommand[command] = bindings;
        }

        bindings.Add(new FavoriteActionBinding(favoriteIndex, button));
    }

    private void RefreshFavoriteActionButtons(string command)
    {
        if (!_favoriteActionBindingsByCommand.TryGetValue(command, out List<FavoriteActionBinding> bindings))
        {
            return;
        }

        foreach (FavoriteActionBinding binding in bindings)
        {
            if (binding.Button != null)
            {
                ApplySelectedButtonColor(binding.Button, IsFavoriteInTab(binding.FavoriteIndex, command));
            }
        }
    }

    private bool ShouldShowCommandSourceTooltip()
    {
        if (TryGetFavoriteIndex(_selectedOwner, out _))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_searchText)
               && string.Equals(_selectedOwner, AllOwner, StringComparison.OrdinalIgnoreCase);
    }

    private static float GetFavoriteActionAreaWidth()
    {
        int actionButtonCount = GetFavoriteTabCount();
        return actionButtonCount * GetFavoriteActionButtonWidth() + Mathf.Max(0, actionButtonCount - 1) * ConsolePanelLayout.FavoriteActionSpacing;
    }

    private static float GetFavoriteActionButtonWidth()
    {
        return ConsolePanelLayout.FavoriteActionButtonWidth;
    }
}
