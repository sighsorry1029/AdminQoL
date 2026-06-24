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

internal static class ConsolePanelModule
{
    internal static ConfigEntry<float> PanelWidth = null!;
    internal static ConfigEntry<float> PanelHeight = null!;
    internal static ConfigEntry<float> VanillaConsoleBottomOffset = null!;
    internal static ConfigEntry<bool> ReleaseMouseCursor = null!;
    internal static ConfigEntry<KeyboardShortcut> TogglePanelKey = null!;
    internal static ConfigEntry<PanelVisualStyle> VisualStyle = null!;
    internal static ConfigEntry<int> FavoriteTabCount = null!;
    internal static ConfigEntry<string>[] FavoriteTabCommands = null!;

    private static ConfigFile? ConfigFile;
    private static ConsolePanelController? Controller;

    internal static void Initialize(GameObject host, ConfigFile config)
    {
        ConfigFile = config;
        VisualStyle = config.Bind(ConsolePanelConfig.PanelSection, "Panel Visual Style", PanelVisualStyle.Modern, OrderedDescription("Visual style for ConsolePanel. Off disables ConsolePanel entirely, Wood uses Valheim-style wood panels, Modern uses ConfigurationManager-like flat panels.", 600));
        TogglePanelKey = config.Bind(ConsolePanelConfig.PanelSection, "Toggle Panel Key", new KeyboardShortcut(KeyCode.F6), OrderedDescription("Keyboard shortcut that toggles ConsolePanel while Valheim's F5 console is visible.", 500));
        PanelWidth = config.Bind(ConsolePanelConfig.PanelSection, "Panel Width", ConsolePanelLayout.DefaultPanelWidth, OrderedDescription("Width of the command browser panel.", 400));
        PanelHeight = config.Bind(ConsolePanelConfig.PanelSection, "Panel Height", ConsolePanelLayout.DefaultPanelHeight, OrderedDescription("Maximum height of the command browser panel. The panel shrinks if needed to preserve the fixed gap below the console.", 300));
        VanillaConsoleBottomOffset = config.Bind(ConsolePanelConfig.PanelSection, "Vanilla Console Bottom Offset", ConsolePanelLayout.DefaultVanillaConsoleBottomOffset, OrderedDescription("Bottom Y offset applied to Valheim's own console rect while the console panel is visible.", 200));
        ReleaseMouseCursor = config.Bind(ConsolePanelConfig.PanelSection, "Release Mouse Cursor", true, OrderedDescription("Unlock and show the mouse cursor while the F5 console panel is visible so category, command, and favorite buttons can be clicked.", 100));
        FavoriteTabCount = config.Bind(ConsolePanelConfig.FavoritesSection, "Favorite Tab Count", 3, new ConfigDescription("Number of numbered favorite command tabs to show.", new AcceptableValueRange<int>(1, ConsolePanelLayout.MaxFavoriteTabs)));
        FavoriteTabCommands = new ConfigEntry<string>[ConsolePanelLayout.MaxFavoriteTabs];
        for (int i = 0; i < ConsolePanelLayout.MaxFavoriteTabs; i++)
        {
            FavoriteTabCommands[i] = config.Bind(ConsolePanelConfig.FavoritesSection, $"Favorite {i + 1} Commands", "", $"Pipe-separated favorite console commands for numbered favorite tab {i + 1}.");
        }
        FavoriteTabCount.SettingChanged += (_, _) => Controller?.OnFavoriteConfigChanged();
        for (int i = 0; i < ConsolePanelLayout.MaxFavoriteTabs; i++)
        {
            FavoriteTabCommands[i].SettingChanged += (_, _) => Controller?.OnFavoriteConfigChanged();
        }

        if (Controller == null)
        {
            Controller = host.AddComponent<ConsolePanelController>();
        }
    }

    internal static void Shutdown()
    {
        if (Controller != null)
        {
            Object.Destroy(Controller);
            Controller = null;
        }

        ConfigFile?.Save();
        ConfigFile = null;
    }

    internal static void SaveConfig()
    {
        ConfigFile?.Save();
    }

    internal static void UpdateConfigWithoutImmediateSave(Func<bool> update)
    {
        if (ConfigFile != null)
        {
            bool previousSaveOnConfigSet = ConfigFile.SaveOnConfigSet;
            ConfigFile.SaveOnConfigSet = false;
            try
            {
                if (update())
                {
                    ConfigFile.Save();
                }
            }
            finally
            {
                ConfigFile.SaveOnConfigSet = previousSaveOnConfigSet;
            }

            return;
        }

        if (update())
        {
            SaveConfig();
        }
    }

    [Conditional("ADMINQOL_DIAGNOSTICS")]
    internal static void Diagnostic(string message)
    {
    }

    private static ConfigDescription OrderedDescription(string description, int order)
    {
        return new ConfigDescription(description, null, new ConfigurationManagerAttributes { Order = order });
    }

    private sealed class ConfigurationManagerAttributes
    {
        public int? Order { get; set; }
    }
}
[HarmonyPatch]
internal static class ConsoleCommandConstructorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(global::Terminal.ConsoleCommand).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
    }

    private static void Postfix(global::Terminal.ConsoleCommand __instance)
    {
        ConsoleCommandOwnership.Record(__instance);
    }
}
[HarmonyPatch(typeof(global::Terminal), nameof(global::Terminal.InitTerminal))]
internal static class TerminalInitPatch
{
    private static void Prefix()
    {
        ConsoleCommandOwnership.EnterTerminalInit();
    }

    private static void Postfix()
    {
        ConsoleCommandOwnership.ExitTerminalInit();
        ConsoleCommandOwnership.MarkDirty();
    }
}

[HarmonyPatch(typeof(global::Console), nameof(global::Console.Update))]
internal static class ConsoleUpdateLayoutPatch
{
    [HarmonyPriority(800)]
    private static void Postfix()
    {
        ConsolePanelController.ApplyAfterVanillaConsoleUpdate();
    }
}

[HarmonyPatch(typeof(TMP_InputField), nameof(TMP_InputField.ActivateInputField))]
internal static class ConsoleInputActivatePatch
{
    private static bool Prefix(TMP_InputField __instance)
    {
        return !ConsolePanelController.ShouldBlockConsoleInputActivation(__instance);
    }
}

[HarmonyPatch(typeof(Player), "TakeInput")]
internal static class PlayerTakeInputPatch
{
    private static void Postfix(ref bool __result)
    {
        if (ConsolePanelInputBlock.IsActive)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(PlayerController), "TakeInput")]
internal static class PlayerControllerTakeInputPatch
{
    private static void Postfix(ref bool __result)
    {
        if (ConsolePanelInputBlock.IsActive)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
internal static class ConsolePanelTextInputVisiblePatch
{
    private static void Postfix(ref bool __result)
    {
        if (ConsolePanelInputBlock.IsActive && ConsolePanelModule.ReleaseMouseCursor.Value)
        {
            __result = true;
        }
    }
}

internal sealed partial class ConsolePanelController : MonoBehaviour
{
    private static ConsolePanelController? ActiveInstance;
    private static Sprite? SolidUiSprite;
    private enum SurfaceRole
    {
        Panel,
        Header,
        Search,
        Scroll,
        Info,
        MutedInfo,
        Button,
        FavoriteBar,
        Tooltip,
        ScrollbarBackground,
        ScrollbarHandle
    }

    private readonly List<CommandEntry> _commands = new();
    private readonly List<CommandEntry> _allRows = new();
    private readonly Dictionary<string, List<CommandEntry>> _commandsByOwner = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CommandEntry> _commandsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<FavoriteActionBinding>> _favoriteActionBindingsByCommand = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _owners = new();
    private readonly ConsolePanelFavoriteStore _favorites = new();
    private readonly List<CommandEntry> _renderedCommandRows = new();
    private readonly List<CommandRowView> _commandRowPool = new();
    private static readonly Vector3[] RectWorldCorners = new Vector3[4];

    private Canvas? _canvas;
    private RectTransform? _panel;
    private RectTransform? _headerRoot;
    private TextMeshProUGUI? _headerLabel;
    private RectTransform? _searchInputRoot;
    private TMP_InputField? _searchInputField;
    private RectTransform? _favoriteProfileRoot;
    private Transform? _favoriteProfileContent;
    private RectTransform? _categoryScroll;
    private RectTransform? _commandScroll;
    private RectTransform? _tooltipRoot;
    private TextMeshProUGUI? _tooltipLabel;
    private ScrollRect? _commandScrollRect;
    private ScrollRect? _categoryScrollRect;
    private Transform? _categoryContent;
    private Transform? _commandContent;
    private RectTransform? _emptyCommandRow;
    private TextMeshProUGUI? _emptyCommandLabel;
    private PanelImageStyle? _inventoryPanelStyle;
    private TMPro.TMP_FontAsset? _font;
    private RectTransform? _vanillaConsoleRect;
    private RectTransformState? _originalVanillaConsoleRectState;
    private string _selectedOwner = AllOwner;
    private string _searchText = "";
    private string _consoleInputSnapshot = "";
    private bool _visible;
    private bool _hasConsoleInputSnapshot;
    private bool _searchInputWanted;
    private bool _hasConsoleReadOnlySnapshot;
    private bool _consoleReadOnlySnapshot;
    private bool _consoleInteractableSnapshot;
    private bool _cursorCaptured;
    private bool _panelHiddenByHotkey;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLockState;
    private float _nextRefresh;
    private int _lastOwnershipVersion = -1;
    private int _lastCommandCount = -1;
    private bool _tmpDefaultFontApplied;
    private float _vanillaConsoleBottomFromPanelCanvas = -1f;
    private string? _lastRenderedOwner;
    private int _lastVirtualFirstIndex = -1;
    private int _lastVirtualRowCount = -1;
    private PanelVisualStyle _createdVisualStyle = (PanelVisualStyle)(-1);

    private void Awake()
    {
        ActiveInstance = this;
        LoadFavorites();
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        ConsolePanelInputBlock.SetActive(false);
        RestoreCursor();
        RestoreVanillaConsoleLayout();

        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
        }
    }

    internal static void ApplyAfterVanillaConsoleUpdate()
    {
        ActiveInstance?.ApplyAfterVanillaConsoleUpdateInternal();
    }

    internal static bool ShouldBlockConsoleInputActivation(TMP_InputField input)
    {
        if (ActiveInstance?._searchInputWanted != true || global::Console.instance?.m_input == null)
        {
            return false;
        }

        if (!ReferenceEquals(input, (object)global::Console.instance.m_input))
        {
            return false;
        }

        return !ActiveInstance.ReleaseSearchFocusForConsoleClick();
    }

    private void Update()
    {
        bool shouldManageConsole = ShouldManageVanillaConsole();
        if (shouldManageConsole)
        {
            HandlePanelToggleHotkey();
        }

        bool shouldShow = ShouldShowPanel();
        if (shouldManageConsole)
        {
            ApplyVanillaConsoleLayout();
        }

        if (shouldShow)
        {
            EnsureUi();
            bool wasVisible = _visible;
            SetVisible(true);
            ApplyLayout();
            RefreshIfNeeded(force: !wasVisible);
            ConsolePanelInputBlock.SetActive(true, ConsolePanelModule.ReleaseMouseCursor.Value);
            ApplyCursorRelease();
            if (!ReleaseSearchFocusForConsoleClick())
            {
                EnforceExclusiveInputFocus();
            }
        }
        else
        {
            SetVisible(false);
            ClearSearchFocusState();
            ConsolePanelInputBlock.SetActive(false);
            RestoreCursor();
            if (!shouldManageConsole)
            {
                RestoreVanillaConsoleLayout();
            }
        }
    }

    private static bool ShouldManageVanillaConsole()
    {
        return IsPanelEnabled()
               && global::Console.IsVisible()
               && global::Console.instance != null;
    }

    private static bool IsPanelEnabled()
    {
        return ConsolePanelModule.VisualStyle.Value != PanelVisualStyle.Off;
    }

    private bool ShouldShowPanel()
    {
        return ShouldManageVanillaConsole()
               && !_panelHiddenByHotkey
               && global::Console.instance != null;
    }

    private void HandlePanelToggleHotkey()
    {
        try
        {
            if (ConsolePanelModule.TogglePanelKey.Value.IsDown())
            {
                _panelHiddenByHotkey = !_panelHiddenByHotkey;
            }
        }
        catch
        {
        }
    }

    private void ApplyAfterVanillaConsoleUpdateInternal()
    {
        bool shouldManageConsole = ShouldManageVanillaConsole();
        if (shouldManageConsole)
        {
            ApplyVanillaConsoleLayout();
        }

        if (!_visible || !ShouldShowPanel())
        {
            return;
        }
        if (!ReleaseSearchFocusForConsoleClick())
        {
            EnforceExclusiveInputFocus();
        }
    }

    private void EnsureUi()
    {
        PanelVisualStyle visualStyle = ConsolePanelModule.VisualStyle.Value;
        if (_canvas != null && _createdVisualStyle != visualStyle)
        {
            Destroy(_canvas.gameObject);
            ClearUiReferences();
        }

        if (_canvas != null)
        {
            return;
        }

        _font = FindFontAsset();
        EnsureTmpDefaultFont();
        _inventoryPanelStyle = FindInventoryPanelStyle();

        GameObject canvasObject = new("ConsolePanel.Canvas");
        DontDestroyOnLoad(canvasObject);
        canvasObject.layer = ConsolePanelLayout.UiLayer;
        canvasObject.AddComponent<GuiPixelFix>();
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                                            | AdditionalCanvasShaderChannels.Normal
                                            | AdditionalCanvasShaderChannels.Tangent;
        _canvas.sortingOrder = ConsolePanelLayout.CanvasSortingOrder;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        GameObject panelObject = CreateUiObject("Panel", canvasRect);
        _panel = panelObject.GetComponent<RectTransform>();
        Image panelImage = panelObject.AddComponent<Image>();
        ApplySurfaceImage(panelImage, SurfaceRole.Panel, new Color(0.33f, 0.22f, 0.12f, 0.88f), preferInventoryPanel: true);
        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.06f, 0.035f, 0.01f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        _headerRoot = CreateHeaderLabel(_panel, out _headerLabel);
        _searchInputRoot = CreateSearchInput(_panel);
        _favoriteProfileRoot = CreateFavoriteProfileBar(_panel, out _favoriteProfileContent);
        _commandScroll = CreateScrollArea(_panel, "Commands", out _commandContent);
        _commandScrollRect = _commandScroll.GetComponent<ScrollRect>();
        ConfigureVirtualCommandContent();
        _categoryScroll = CreateScrollArea(_panel, "Categories", out _categoryContent);
        _categoryScrollRect = _categoryScroll.GetComponent<ScrollRect>();
        _tooltipRoot = CreateTooltip(canvasRect, out _tooltipLabel);
        _createdVisualStyle = visualStyle;
        ApplyLayout();
        SetVisible(false);
    }

    private void ClearUiReferences()
    {
        _canvas = null;
        _panel = null;
        _headerRoot = null;
        _headerLabel = null;
        _searchInputRoot = null;
        _searchInputField = null;
        _favoriteProfileRoot = null;
        _favoriteProfileContent = null;
        _categoryScroll = null;
        _commandScroll = null;
        _tooltipRoot = null;
        _tooltipLabel = null;
        _commandScrollRect = null;
        _categoryScrollRect = null;
        _categoryContent = null;
        _commandContent = null;
        _emptyCommandRow = null;
        _emptyCommandLabel = null;
        _commandRowPool.Clear();
        _renderedCommandRows.Clear();
        _favoriteActionBindingsByCommand.Clear();
        _lastVirtualFirstIndex = -1;
        _lastVirtualRowCount = -1;
        _lastRenderedOwner = null;
        ClearSearchFocusState();
    }

    private void ApplyLayout()
    {
        if (_panel == null || _headerRoot == null || _searchInputRoot == null || _favoriteProfileRoot == null || _commandScroll == null || _categoryScroll == null)
        {
            return;
        }

        if (_canvas != null)
        {
            _canvas.sortingOrder = ConsolePanelLayout.CanvasSortingOrder;
        }

        float width = Mathf.Max(ConsolePanelLayout.MinPanelWidth, ConsolePanelModule.PanelWidth.Value);
        float desiredHeight = Mathf.Max(ConsolePanelLayout.MinPanelHeight, ConsolePanelModule.PanelHeight.Value);
        float panelTop = GetPanelTopOffset();
        float height = Mathf.Max(1f, Mathf.Min(desiredHeight, panelTop));
        float categoryWidth = Mathf.Clamp(ConsolePanelLayout.CategoryWidth, ConsolePanelLayout.MinCategoryWidth, width * ConsolePanelLayout.MaxCategoryWidthRatio);
        float rowHeight = ConsolePanelLayout.RowHeight;
        float gap = ConsolePanelLayout.Gutter;
        float searchHeight = rowHeight;
        float availableBeforeCategory = Mathf.Max(ConsolePanelLayout.MinCommandHeaderWidth, width - categoryWidth - gap * 3f);
        float actionWidth = GetFavoriteActionAreaWidth();
        float searchMaxWidth = Mathf.Max(ConsolePanelLayout.SearchExtraWidth, availableBeforeCategory - ConsolePanelLayout.SearchReservedWidth - gap);
        float searchWidth = Mathf.Min(Mathf.Max(actionWidth + ConsolePanelLayout.SearchExtraWidth, ConsolePanelLayout.SearchMinWidth), searchMaxWidth);

        _panel.anchorMin = new Vector2(0f, 0f);
        _panel.anchorMax = new Vector2(0f, 0f);
        _panel.pivot = new Vector2(0f, 0f);
        _panel.anchoredPosition = new Vector2(ConsolePanelLayout.PanelLeft, Mathf.Max(0f, panelTop - height));
        _panel.sizeDelta = new Vector2(width, height);

        _headerRoot.anchorMin = new Vector2(0f, 1f);
        _headerRoot.anchorMax = new Vector2(1f, 1f);
        _headerRoot.pivot = new Vector2(0f, 1f);
        _headerRoot.offsetMin = new Vector2(gap, -(searchHeight + gap));
        _headerRoot.offsetMax = new Vector2(-(categoryWidth + searchWidth + gap * 3f), -gap);

        _searchInputRoot.anchorMin = new Vector2(1f, 1f);
        _searchInputRoot.anchorMax = new Vector2(1f, 1f);
        _searchInputRoot.pivot = new Vector2(1f, 1f);
        _searchInputRoot.anchoredPosition = new Vector2(-(categoryWidth + gap * 2f), -gap);
        _searchInputRoot.sizeDelta = new Vector2(searchWidth, searchHeight);

        _favoriteProfileRoot.anchorMin = new Vector2(1f, 1f);
        _favoriteProfileRoot.anchorMax = new Vector2(1f, 1f);
        _favoriteProfileRoot.pivot = new Vector2(1f, 1f);
        _favoriteProfileRoot.anchoredPosition = new Vector2(-gap, -gap);
        _favoriteProfileRoot.sizeDelta = new Vector2(categoryWidth, searchHeight);

        _commandScroll.anchorMin = new Vector2(0f, 0f);
        _commandScroll.anchorMax = new Vector2(1f, 1f);
        _commandScroll.pivot = new Vector2(0f, 0.5f);
        _commandScroll.offsetMin = new Vector2(gap, gap);
        _commandScroll.offsetMax = new Vector2(-(categoryWidth + gap * 2f), -(searchHeight + gap * 2f));

        _categoryScroll.anchorMin = new Vector2(1f, 0f);
        _categoryScroll.anchorMax = new Vector2(1f, 1f);
        _categoryScroll.pivot = new Vector2(1f, 0.5f);
        _categoryScroll.offsetMin = new Vector2(-(categoryWidth + gap), gap);
        _categoryScroll.offsetMax = new Vector2(-gap, -(searchHeight + gap * 2f));

        ApplyScrollSettings(_commandScrollRect);
        ApplyScrollSettings(_categoryScrollRect);
        _commandScroll.SetAsLastSibling();
        _headerRoot.SetAsLastSibling();
        _searchInputRoot.SetAsLastSibling();
        _favoriteProfileRoot.SetAsLastSibling();
        _categoryScroll.SetAsLastSibling();
        _tooltipRoot?.SetAsLastSibling();
    }

    private void ApplyVanillaConsoleLayout()
    {
        RectTransform? rect = FindVanillaConsoleRect();
        if (rect == null)
        {
            return;
        }

        if (_vanillaConsoleRect != rect)
        {
            RestoreVanillaConsoleLayout();
            _vanillaConsoleRect = rect;
            _originalVanillaConsoleRectState = RectTransformState.Capture(rect);
            ConsolePanelModule.Diagnostic(
                $"captured vanilla console rect '{GetTransformPath(rect)}': " +
                $"anchorMin={rect.anchorMin}, anchorMax={rect.anchorMax}, offsetMin={rect.offsetMin}, offsetMax={rect.offsetMax}");
        }

        RectTransformState original = _originalVanillaConsoleRectState ?? RectTransformState.Capture(rect);
        float bottomOffset = GetVanillaConsoleBottomOffset();
        rect.anchorMin = original.AnchorMin;
        rect.anchorMax = original.AnchorMax;
        rect.pivot = original.Pivot;
        rect.anchoredPosition = original.AnchoredPosition;
        rect.sizeDelta = original.SizeDelta;
        rect.offsetMax = original.OffsetMax;
        rect.offsetMin = new Vector2(original.OffsetMin.x, bottomOffset);
        _vanillaConsoleBottomFromPanelCanvas = TryGetRectBottomFromPanelCanvas(rect, out float consoleBottom)
            ? consoleBottom
            : -1f;
    }

    private void RestoreVanillaConsoleLayout()
    {
        if (_vanillaConsoleRect != null && _originalVanillaConsoleRectState.HasValue)
        {
            _originalVanillaConsoleRectState.Value.Apply(_vanillaConsoleRect);
        }

        _vanillaConsoleRect = null;
        _originalVanillaConsoleRectState = null;
        _vanillaConsoleBottomFromPanelCanvas = -1f;
    }

    private static float GetVanillaConsoleBottomOffset()
    {
        return Mathf.Max(0f, ConsolePanelModule.VanillaConsoleBottomOffset.Value);
    }

    private float GetPanelTopOffset()
    {
        float consoleBottom = GetConsoleBottomFromPanelCanvas();
        return Mathf.Max(0f, consoleBottom - ConsolePanelLayout.PanelGapBelowConsole);
    }

    private float GetConsoleBottomFromPanelCanvas()
    {
        if (_vanillaConsoleRect != null && TryGetRectBottomFromPanelCanvas(_vanillaConsoleRect, out float consoleBottom))
        {
            _vanillaConsoleBottomFromPanelCanvas = consoleBottom;
            return consoleBottom;
        }

        return _vanillaConsoleBottomFromPanelCanvas >= 0f
            ? _vanillaConsoleBottomFromPanelCanvas
            : GetVanillaConsoleBottomOffset();
    }

    private bool TryGetRectBottomFromPanelCanvas(RectTransform rect, out float bottom)
    {
        bottom = 0f;
        if (_canvas == null || _canvas.transform is not RectTransform canvasRect)
        {
            return false;
        }

        rect.GetWorldCorners(RectWorldCorners);
        Canvas? sourceCanvas = rect.GetComponentInParent<Canvas>();
        Camera? sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera
            : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, RectWorldCorners[0]);
        Camera? panelCamera = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, panelCamera, out Vector2 localPoint))
        {
            return false;
        }

        bottom = Mathf.Max(0f, localPoint.y - canvasRect.rect.yMin);
        return true;
    }

    private static RectTransform? FindVanillaConsoleRect()
    {
        global::Console? console = global::Console.instance;
        if (console == null)
        {
            return null;
        }

        if (console.m_chatWindow != null)
        {
            RectTransform? visualRect = FindConsoleBuddyStyleVisualRect(console.m_chatWindow);
            if (visualRect != null)
            {
                return visualRect;
            }
        }

        RectTransform? transformVisualRect = FindConsoleBuddyStyleVisualRect(console.transform);
        if (transformVisualRect != null)
        {
            return transformVisualRect;
        }

        if (console.m_chatWindow != null)
        {
            return console.m_chatWindow;
        }

        if (console.m_input != null && ((Component)(object)console.m_input).transform is RectTransform inputRect)
        {
            RectTransform? root = inputRect.GetComponentsInParent<RectTransform>(true)
                .Where(rect => rect != null)
                .OrderByDescending(rect => rect.rect.width * rect.rect.height)
                .FirstOrDefault();
            if (root != null)
            {
                return root;
            }
        }

        return null;
    }

    private static RectTransform? FindConsoleBuddyStyleVisualRect(Transform? root)
    {
        if (root == null)
        {
            return null;
        }

        Queue<Transform> queue = new();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                queue.Enqueue(child);
            }

            if (!string.Equals(current.name, "Image", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (current is not RectTransform rect || current.GetComponent<Image>() == null)
            {
                continue;
            }

            Transform? textChild = current.Find("Text");
            if (textChild != null && textChild.GetComponent<TextMeshProUGUI>() != null)
            {
                return rect;
            }
        }

        return null;
    }

    private static string GetTransformPath(Transform transform)
    {
        List<string> parts = new();
        for (Transform? current = transform; current != null; current = current.parent)
        {
            parts.Add(current.name);
        }

        parts.Reverse();
        return string.Join("/", parts);
    }





    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (_canvas != null && _canvas.gameObject.activeSelf != visible)
        {
            _canvas.gameObject.SetActive(visible);
        }
    }

    private void ApplyCursorRelease()
    {
        if (!ConsolePanelModule.ReleaseMouseCursor.Value)
        {
            RestoreCursor();
            return;
        }

        if (!_cursorCaptured)
        {
            _previousCursorVisible = Cursor.visible;
            _previousCursorLockState = Cursor.lockState;
            _cursorCaptured = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursor()
    {
        if (!_cursorCaptured)
        {
            return;
        }

        Cursor.visible = _previousCursorVisible;
        Cursor.lockState = _previousCursorLockState;
        _cursorCaptured = false;
    }



    private const string AllOwner = "All";
    private const string FavoriteOwnerPrefix = "Favorite ";
    private const float VirtualCommandPadding = ConsolePanelLayout.VirtualCommandPadding;
    private const float VirtualCommandSpacing = ConsolePanelLayout.VirtualCommandSpacing;
}
