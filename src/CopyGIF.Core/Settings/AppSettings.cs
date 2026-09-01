namespace CopyGIF.Core.Settings;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public const string DefaultHotkey = "Alt+G";

    public const string DefaultProviderId = "klipy";

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public string Hotkey { get; init; } =
        DefaultHotkey;

    public SearchSettings Search { get; init; } =
        new();

    public LibrarySettings Library { get; init; } =
        new();

    public WindowSettings Window { get; init; } =
        new();

    public BehaviorSettings Behavior { get; init; } =
        new();

    public AppearanceSettings Appearance { get; init; } =
        new();

    public StartupSettings Startup { get; init; } =
        new();

    public UpdateSettings Updates { get; init; } =
        new();

    public ProviderSettings Providers { get; init; } =
        new();
}

public sealed record SearchSettings
{
    public int ResultsPerSearch { get; init; } = 24;

    public int DebounceMilliseconds { get; init; } = 300;

    public bool AnimatePreviews { get; init; } = true;

    public bool AutoLoadMoreResults { get; init; }

    public bool ShowTrendingWhenEmpty { get; init; } = true;

    public bool SaveSearchHistory { get; init; } = true;

    public bool UseHistorySuggestions { get; init; } = true;

    public int SearchHistoryLimit { get; init; } = 50;
}

public sealed record LibrarySettings
{
    public int RecentLimit { get; init; } = 30;

    public int FavoriteLimit { get; init; } = 100;

    public bool StoreFavoritesLocally { get; init; } = true;

    public bool StoreRecentsLocally { get; init; } = true;

    public string? CustomStorageRoot { get; init; }
}

public sealed record WindowSettings
{
    public WindowPlacementMode PlacementMode { get; init; } =
        WindowPlacementMode.Mouse;

    public bool RememberWindowSize { get; init; } = true;

    public double Width { get; init; } = 760;

    public double Height { get; init; } = 560;

    public double? Left { get; init; }

    public double? Top { get; init; }

    public string? LastMonitorId { get; init; }
}

public sealed record BehaviorSettings
{
    public bool CloseWhenFocusLost { get; init; } = true;

    public bool HideAfterCopy { get; init; } = true;
}

public sealed record AppearanceSettings
{
    public AppTheme Theme { get; init; } =
        AppTheme.System;
}

public sealed record StartupSettings
{
    public bool StartWithWindows { get; init; } = true;
}

public sealed record UpdateSettings
{
    public bool CheckForUpdates { get; init; } = true;

    public UpdateCheckFrequency CheckFrequency { get; init; } =
        UpdateCheckFrequency.Daily;

    public UpdateMode Mode { get; init; } =
        UpdateMode.Recommended;
}

public sealed record ProviderSettings
{
    public string ActiveProviderId { get; init; } =
        AppSettings.DefaultProviderId;

    public ProviderDisplayMode DisplayMode { get; init; } =
        ProviderDisplayMode.Single;
}
