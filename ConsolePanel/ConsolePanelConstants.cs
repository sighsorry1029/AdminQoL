namespace ConsolePanel;

internal static class ConsolePanelConfig
{
    internal const string PanelSection = "3 - Console Panel";
    internal const string FavoritesSection = "4 - Console Panel Favorites";
}

internal static class ConsolePanelLayout
{
    internal const int MaxFavoriteTabs = 5;
    internal const int CanvasSortingOrder = 5000;
    internal const int UiLayer = 5;
    internal const float DefaultPanelWidth = 900f;
    internal const float DefaultPanelHeight = 450f;
    internal const float DefaultVanillaConsoleBottomOffset = 120f;
    internal const float PanelLeft = 5f;
    internal const float RowHeight = 30f;
    internal const float ScrollbarWidth = 2f;
    internal const float ScrollSensitivity = 240f;
    internal const float CommandRefreshSeconds = 3f;
    internal const float PanelGapBelowConsole = 30f;
    internal const float CategoryWidth = 150f;
    internal const float MinPanelWidth = 360f;
    internal const float MinPanelHeight = 180f;
    internal const float MinCategoryWidth = 80f;
    internal const float MaxCategoryWidthRatio = 0.45f;
    internal const float Gutter = 8f;
    internal const float MinCommandHeaderWidth = 160f;
    internal const float SearchReservedWidth = 180f;
    internal const float SearchExtraWidth = 120f;
    internal const float SearchMinWidth = 150f;
    internal const float FavoriteActionSpacing = 3f;
    internal const float FavoriteActionButtonWidth = 20f;
    internal const float CompactButtonMinWidth = 14f;
    internal const float CompactButtonPreferredWidth = 18f;
    internal const float VirtualCommandPadding = 4f;
    internal const float VirtualCommandSpacing = 4f;
    internal const int VirtualCommandLookBehindRows = 2;
    internal const int VirtualCommandLookAheadRows = 6;
    internal const int CommandPreviewCount = 5;
    internal const int ManualClickSuppressionFrames = 3;
}
