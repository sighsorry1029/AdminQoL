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
    private void ConfigureVirtualCommandContent()
    {
        if (_commandContent is not RectTransform contentRect)
        {
            return;
        }

        foreach (VerticalLayoutGroup layout in contentRect.GetComponents<VerticalLayoutGroup>())
        {
            layout.enabled = false;
            Destroy(layout);
        }

        foreach (ContentSizeFitter fitter in contentRect.GetComponents<ContentSizeFitter>())
        {
            fitter.enabled = false;
            Destroy(fitter);
        }

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        if (_commandScrollRect != null)
        {
            _commandScrollRect.onValueChanged.AddListener(_ => UpdateVirtualCommandRows());
        }
    }

    private RectTransform CreateScrollArea(RectTransform parent, string name, out Transform content)
    {
        GameObject root = CreateUiObject(name, parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Image rootImage = root.AddComponent<Image>();
        ApplySurfaceImage(rootImage, SurfaceRole.Scroll, new Color(0.08f, 0.10f, 0.10f, 0.45f));

        GameObject viewport = CreateUiObject("Viewport", rootRect);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMax = new Vector2(-(ConsolePanelLayout.ScrollbarWidth + 3f), 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUiObject("Content", viewportRect);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vertical = contentObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 4f;
        vertical.padding = new RectOffset(4, 4, 4, 4);
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content = contentObject.transform;

        Scrollbar scrollbar = CreateScrollbar(rootRect);
        ScrollRect scrollRect = root.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = ConsolePanelLayout.ScrollSensitivity;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 2f;
        root.AddComponent<ManualScrollForwarder>().Configure(scrollRect);

        return rootRect;
    }

    private static void ApplyScrollSettings(ScrollRect? scrollRect)
    {
        if (scrollRect == null)
        {
            return;
        }

        scrollRect.scrollSensitivity = ConsolePanelLayout.ScrollSensitivity;
        Scrollbar? scrollbar = scrollRect.verticalScrollbar;
        if (scrollbar == null)
        {
            return;
        }

        RectTransform? scrollbarRect = scrollbar.transform as RectTransform;
        if (scrollbarRect != null)
        {
            scrollbarRect.offsetMin = new Vector2(-ConsolePanelLayout.ScrollbarWidth, 0f);
            scrollbarRect.offsetMax = Vector2.zero;
        }

        if (scrollbar.targetGraphic != null)
        {
            scrollbar.targetGraphic.color = AccentColor();
        }
    }

    private Scrollbar CreateScrollbar(RectTransform parent)
    {
        GameObject scrollbarObject = CreateUiObject("Scrollbar", parent);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-ConsolePanelLayout.ScrollbarWidth, 0f);
        scrollbarRect.offsetMax = Vector2.zero;

        Image background = scrollbarObject.AddComponent<Image>();
        ApplySurfaceImage(background, SurfaceRole.ScrollbarBackground, new Color(0f, 0f, 0f, 0.15f));

        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarRect);
        Stretch(slidingArea.GetComponent<RectTransform>());

        GameObject handle = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        Stretch(handleRect);
        Image handleImage = handle.AddComponent<Image>();
        ApplySurfaceImage(handleImage, SurfaceRole.ScrollbarHandle, AccentColor());

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        return scrollbar;
    }

    private Button CreateTabButton(Transform parent, string text, Action onClick)
    {
        Button button = CreateButton(parent, text, onClick);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = ConsolePanelLayout.RowHeight;
        layout.preferredHeight = ConsolePanelLayout.RowHeight;
        return button;
    }

    private Button CreateCompactButton(Transform parent, string text, Action onClick)
    {
        Button button = CreateButton(parent, text, onClick);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = ConsolePanelLayout.CompactButtonMinWidth;
        layout.preferredWidth = ConsolePanelLayout.CompactButtonPreferredWidth;
        layout.flexibleWidth = 1f;

        TextMeshProUGUI? label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 13f;
        }

        return button;
    }

    private Button CreateFavoriteActionButton(Transform parent, string text, Action onClick)
    {
        Button button = CreateButton(parent, text, onClick);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        float width = GetFavoriteActionButtonWidth();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;

        TextMeshProUGUI? label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 13f;
        }

        RectTransform? labelRect = label?.rectTransform;
        if (labelRect != null)
        {
            labelRect.offsetMin = new Vector2(2f, 1f);
            labelRect.offsetMax = new Vector2(-2f, -1f);
        }

        return button;
    }

    private Button CreateButton(Transform parent, string text, Action onClick)
    {
        GameObject buttonObject = CreateUiObject("Button", parent);
        Image image = buttonObject.AddComponent<Image>();
        ApplySurfaceImage(image, SurfaceRole.Button, new Color(0.28f, 0.22f, 0.12f, 0.92f));
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        int lastClickFrame = -1;
        int suppressNonManualUntilFrame = -1;
        bool clickHandledForCurrentPress = false;
        void DispatchClick(bool fromManualMouseDown)
        {
            bool mouseHeld = IsPrimaryMouseHeld();
            bool mouseDownThisFrame = fromManualMouseDown || IsPrimaryMouseDown();
            if (clickHandledForCurrentPress && mouseDownThisFrame && lastClickFrame != Time.frameCount)
            {
                clickHandledForCurrentPress = false;
            }

            if (clickHandledForCurrentPress && !mouseHeld)
            {
                clickHandledForCurrentPress = false;
                suppressNonManualUntilFrame = Time.frameCount + ConsolePanelLayout.ManualClickSuppressionFrames;
                return;
            }

            if (!fromManualMouseDown && Time.frameCount <= suppressNonManualUntilFrame)
            {
                return;
            }

            if (clickHandledForCurrentPress)
            {
                return;
            }

            if (lastClickFrame == Time.frameCount)
            {
                clickHandledForCurrentPress = clickHandledForCurrentPress || mouseHeld || mouseDownThisFrame;
                return;
            }

            clickHandledForCurrentPress = mouseHeld || mouseDownThisFrame;
            suppressNonManualUntilFrame = Time.frameCount + ConsolePanelLayout.ManualClickSuppressionFrames;
            lastClickFrame = Time.frameCount;
            onClick();
        }

        button.onClick.AddListener(() => DispatchClick(false));
        ManualClickForwarder manualClick = buttonObject.AddComponent<ManualClickForwarder>();
        manualClick.Configure(() => DispatchClick(true));

        ColorBlock colors = button.colors;
        colors.normalColor = ButtonNormalColor();
        colors.highlightedColor = ButtonHighlightColor();
        colors.pressedColor = ButtonPressedColor();
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(7f, 2f);
        labelRect.offsetMax = new Vector2(-7f, -2f);
        EnsureTmpDefaultFont();
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;

        label.text = text;
        label.fontSize = 15f;
        label.color = ButtonTextColor();
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return button;
    }

    private RectTransform CreateSearchInput(RectTransform parent)
    {
        GameObject inputObject = CreateUiObject("SearchInput", parent);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        Image image = inputObject.AddComponent<Image>();
        ApplySurfaceImage(image, SurfaceRole.Search, new Color(0.16f, 0.08f, 0.04f, 0.90f));

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 96;

        GameObject viewportObject = CreateUiObject("TextViewport", inputObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(30f, 2f);
        viewportRect.offsetMax = new Vector2(-8f, -2f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject textObject = CreateUiObject("Text", viewportObject.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);
        EnsureTmpDefaultFont();
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = 15f;
        text.color = new Color(1f, 0.92f, 0.70f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        GameObject placeholderObject = CreateUiObject("Placeholder", viewportObject.transform);
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        Stretch(placeholderRect);
        TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholder.font = _font;
        placeholder.text = "Search commands";
        placeholder.fontSize = 15f;
        placeholder.color = new Color(0.78f, 0.70f, 0.55f, 0.75f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.overflowMode = TextOverflowModes.Ellipsis;
        placeholder.raycastTarget = false;

        GameObject iconObject = CreateUiObject("SearchIcon", inputObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(15f, 0f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        TextMeshProUGUI icon = iconObject.AddComponent<TextMeshProUGUI>();
        icon.font = _font;
        icon.text = "/";
        icon.fontSize = 16f;
        icon.color = new Color(1f, 0.55f, 0.35f, 1f);
        icon.alignment = TextAlignmentOptions.Center;
        icon.raycastTarget = false;

        input.textComponent = text;
        input.placeholder = placeholder;
        input.textViewport = viewportRect;
        input.targetGraphic = image;
        input.onValueChanged.AddListener(value =>
        {
            _searchText = value ?? "";
            RefreshCommandRows();
        });
        void ActivateSearchInput()
        {
            BeginSearchInputFocus();
            EventSystem.current?.SetSelectedGameObject(inputObject);
            input.Select();
            input.ActivateInputField();
            input.caretPosition = input.text?.Length ?? 0;
        }

        ManualClickForwarder manualClick = inputObject.AddComponent<ManualClickForwarder>();
        manualClick.Configure(ActivateSearchInput);
        _searchInputField = input;
        return inputRect;
    }

    private RectTransform CreateFavoriteProfileBar(RectTransform parent, out Transform content)
    {
        GameObject root = CreateUiObject("FavoriteProfiles", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Image image = root.AddComponent<Image>();
        ApplySurfaceImage(image, SurfaceRole.FavoriteBar, new Color(0.34f, 0.25f, 0.10f, 0.92f));

        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(6, 6, 4, 4);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;
        content = root.transform;
        return rootRect;
    }

    private RectTransform CreateTooltip(RectTransform parent, out TextMeshProUGUI label)
    {
        GameObject root = CreateUiObject("OwnerTooltip", parent);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.sizeDelta = new Vector2(180f, 28f);
        Image image = root.AddComponent<Image>();
        ApplySurfaceImage(image, SurfaceRole.Tooltip, new Color(0.02f, 0.02f, 0.02f, 0.92f));

        GameObject labelObject = CreateUiObject("Label", root.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(8f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);
        EnsureTmpDefaultFont();
        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSize = 14f;
        label.color = new Color(1f, 0.92f, 0.70f, 1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        root.SetActive(false);
        return rootRect;
    }

    private void ShowOwnerTooltip(string text, Vector3 screenPosition)
    {
        if (_tooltipRoot == null || _tooltipLabel == null || _canvas == null)
        {
            return;
        }

        _tooltipLabel.text = text;
        Vector2 preferred = _tooltipLabel.GetPreferredValues(text);
        Vector2 size = new(Mathf.Clamp(preferred.x + 18f, 90f, 320f), 28f);
        _tooltipRoot.sizeDelta = size;
        RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint))
        {
            Vector2 position = localPoint + new Vector2(14f, 18f);
            Rect canvasBounds = canvasRect.rect;
            position.x = Mathf.Clamp(position.x, canvasBounds.xMin + 4f, canvasBounds.xMax - size.x - 4f);
            position.y = Mathf.Clamp(position.y, canvasBounds.yMin + 4f, canvasBounds.yMax - size.y - 4f);
            _tooltipRoot.anchoredPosition = position;
        }

        _tooltipRoot.gameObject.SetActive(true);
        _tooltipRoot.SetAsLastSibling();
    }

    private void HideOwnerTooltip()
    {
        if (_tooltipRoot != null)
        {
            _tooltipRoot.gameObject.SetActive(false);
        }
    }

    private void ApplySelectedButtonColor(Button button, bool selected)
    {
        Image? image = button.targetGraphic as Image;
        TextMeshProUGUI? label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (image != null)
        {
            if (selected)
            {
                ApplySelectedButtonImage(image);
            }
            else
            {
                ApplySurfaceImage(image, SurfaceRole.Button, new Color(0.28f, 0.22f, 0.12f, 0.92f));
            }
        }

        if (label != null)
        {
            label.color = selected ? SelectedButtonTextColor() : ButtonTextColor();
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = selected ? Color.white : ButtonNormalColor();
        colors.highlightedColor = selected ? Color.white : ButtonHighlightColor();
        colors.pressedColor = selected ? Color.white : ButtonPressedColor();
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.targetGraphic?.CrossFadeColor(colors.normalColor, 0f, true, true);
    }

    private void ApplySelectedButtonImage(Image image)
    {
        if (ConsolePanelModule.VisualStyle.Value == PanelVisualStyle.Modern)
        {
            image.sprite = GetSolidUiSprite();
            image.material = null;
            image.type = Image.Type.Simple;
            image.color = SelectedButtonBackgroundColor(0.94f);
            return;
        }

        _inventoryPanelStyle ??= FindInventoryPanelStyle();
        PanelImageStyle? style = _inventoryPanelStyle;
        if (style != null)
        {
            image.sprite = style.Sprite;
            image.material = style.Material;
            image.type = style.Type;
            image.color = SelectedButtonBackgroundColor(0.84f);
            return;
        }

        image.sprite = GetSolidUiSprite();
        image.material = null;
        image.type = Image.Type.Simple;
        image.color = SelectedButtonBackgroundColor(0.84f);
    }

    private static void ApplyDangerButtonColor(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.42f, 0.16f, 0.10f, 0.95f);
        colors.highlightedColor = new Color(0.62f, 0.24f, 0.16f, 1f);
        colors.pressedColor = new Color(0.78f, 0.32f, 0.20f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }
}
