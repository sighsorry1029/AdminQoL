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
    private sealed class CommandRowView
    {
        private readonly ConsolePanelController _owner;
        private readonly Button _commandButton;
        private readonly TextMeshProUGUI? _commandLabel;
        private readonly Transform _actionsTransform;
        private readonly LayoutElement _actionElement;
        private readonly List<Button> _favoriteButtons = new();
        private readonly List<LayoutElement> _favoriteButtonLayouts = new();
        private ManualHoverTooltip? _tooltip;
        private CommandEntry _entry;
        private bool _hasEntry;

        internal RectTransform RectTransform { get; }

        internal CommandRowView(ConsolePanelController owner, Transform parent)
        {
            _owner = owner;
            GameObject row = CreateUiObject("CommandRow", parent);
            RectTransform = row.GetComponent<RectTransform>();
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 2, 2);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            _commandButton = owner.CreateButton(row.transform, "", () =>
            {
                if (_hasEntry)
                {
                    _owner.PutCommandInInput(_entry);
                }
            });
            _commandLabel = _commandButton.GetComponentInChildren<TextMeshProUGUI>(true);
            LayoutElement commandLayout = _commandButton.gameObject.AddComponent<LayoutElement>();
            commandLayout.flexibleWidth = 1f;

            GameObject actions = CreateUiObject("FavoriteActions", row.transform);
            _actionsTransform = actions.transform;
            HorizontalLayoutGroup actionLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 3f;
            actionLayout.padding = new RectOffset(0, 0, 0, 0);
            actionLayout.childControlHeight = true;
            actionLayout.childControlWidth = true;
            actionLayout.childForceExpandHeight = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            _actionElement = actions.AddComponent<LayoutElement>();
        }

        internal void SetActive(bool active)
        {
            if (RectTransform.gameObject.activeSelf != active)
            {
                RectTransform.gameObject.SetActive(active);
            }
        }

        internal void Bind(CommandEntry entry)
        {
            _entry = entry;
            _hasEntry = true;
            if (_commandLabel != null)
            {
                _commandLabel.text = FormatCommandRowText(entry);
            }

            bool showTooltip = _owner.ShouldShowCommandSourceTooltip();
            if (showTooltip)
            {
                _tooltip ??= _commandButton.gameObject.AddComponent<ManualHoverTooltip>();
                _tooltip.enabled = true;
                _tooltip.Configure($"Source: {entry.Owner}", _owner.ShowOwnerTooltip, _owner.HideOwnerTooltip);
            }
            else if (_tooltip != null)
            {
                _tooltip.enabled = false;
            }

            EnsureFavoriteButtons();
            float actionWidth = GetFavoriteActionAreaWidth();
            _actionElement.minWidth = actionWidth;
            _actionElement.preferredWidth = actionWidth;
            _actionElement.flexibleWidth = 0f;

            int count = GetFavoriteTabCount();
            for (int i = 0; i < _favoriteButtons.Count; i++)
            {
                Button button = _favoriteButtons[i];
                bool visible = i < count;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                if (_favoriteButtonLayouts[i] != null)
                {
                    float width = GetFavoriteActionButtonWidth();
                    _favoriteButtonLayouts[i].minWidth = width;
                    _favoriteButtonLayouts[i].preferredWidth = width;
                }

                _owner.RegisterFavoriteActionButton(entry.Command, i, button);
                _owner.ApplySelectedButtonColor(button, _owner.IsFavoriteInTab(i, entry.Command));
            }

            LayoutRebuilder.MarkLayoutForRebuild(RectTransform);
        }

        private void EnsureFavoriteButtons()
        {
            while (_favoriteButtons.Count < ConsolePanelLayout.MaxFavoriteTabs)
            {
                int targetIndex = _favoriteButtons.Count;
                Button button = _owner.CreateFavoriteActionButton(_actionsTransform, (targetIndex + 1).ToString(), () =>
                {
                    if (_hasEntry)
                    {
                        _owner.ToggleFavorite(targetIndex, _entry.Command);
                    }
                });
                _favoriteButtons.Add(button);
                _favoriteButtonLayouts.Add(button.GetComponent<LayoutElement>());
            }
        }
    }
}
