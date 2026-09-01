namespace CopyGIF.Core.Settings;

public static class AppSettingsNormalizer
{
    public static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();

        SearchSettings search =
            settings.Search ?? new SearchSettings();

        LibrarySettings library =
            settings.Library ?? new LibrarySettings();

        WindowSettings window =
            settings.Window ?? new WindowSettings();

        BehaviorSettings behavior =
            settings.Behavior ?? new BehaviorSettings();

        AppearanceSettings appearance =
            settings.Appearance ?? new AppearanceSettings();

        StartupSettings startup =
            settings.Startup ?? new StartupSettings();

        UpdateSettings updates =
            settings.Updates ?? new UpdateSettings();

        ProviderSettings providers =
            settings.Providers ?? new ProviderSettings();

        return settings with
        {
            SchemaVersion =
                AppSettings.CurrentSchemaVersion,

            Hotkey = NormalizeRequiredText(
                settings.Hotkey,
                AppSettings.DefaultHotkey,
                AppSettingsValidator.MaximumHotkeyLength),

            Search = search with
            {
                ResultsPerSearch = UseValueOrFallback(
                    search.ResultsPerSearch,
                    AppSettingsValidator.MinimumResultsPerSearch,
                    AppSettingsValidator.MaximumResultsPerSearch,
                    24),

                DebounceMilliseconds = UseValueOrFallback(
                    search.DebounceMilliseconds,
                    AppSettingsValidator.MinimumDebounceMilliseconds,
                    AppSettingsValidator.MaximumDebounceMilliseconds,
                    300),

                SearchHistoryLimit = UseValueOrFallback(
                    search.SearchHistoryLimit,
                    AppSettingsValidator.MinimumSearchHistoryLimit,
                    AppSettingsValidator.MaximumSearchHistoryLimit,
                    50)
            },

            Library = library with
            {
                RecentLimit = UseValueOrFallback(
                    library.RecentLimit,
                    AppSettingsValidator.MinimumRecentLimit,
                    AppSettingsValidator.MaximumRecentLimit,
                    30),

                FavoriteLimit = UseValueOrFallback(
                    library.FavoriteLimit,
                    AppSettingsValidator.MinimumFavoriteLimit,
                    AppSettingsValidator.MaximumFavoriteLimit,
                    100),

                CustomStorageRoot = NormalizeOptionalText(
                    library.CustomStorageRoot,
                    AppSettingsValidator.MaximumStorageRootLength)
            },

            Window = window with
            {
                PlacementMode = NormalizeEnum(
                    window.PlacementMode,
                    WindowPlacementMode.Mouse),

                Width = UseValueOrFallback(
                    window.Width,
                    AppSettingsValidator.MinimumWindowWidth,
                    AppSettingsValidator.MaximumWindowWidth,
                    760),

                Height = UseValueOrFallback(
                    window.Height,
                    AppSettingsValidator.MinimumWindowHeight,
                    AppSettingsValidator.MaximumWindowHeight,
                    560),

                Left = NormalizeCoordinate(
                    window.Left),

                Top = NormalizeCoordinate(
                    window.Top),

                LastMonitorId = NormalizeOptionalText(
                    window.LastMonitorId,
                    AppSettingsValidator.MaximumMonitorIdLength)
            },

            Behavior = behavior,

            Appearance = appearance with
            {
                Theme = NormalizeEnum(
                    appearance.Theme,
                    AppTheme.System)
            },

            Startup = startup,

            Updates = updates with
            {
                CheckFrequency = NormalizeEnum(
                    updates.CheckFrequency,
                    UpdateCheckFrequency.Daily),

                Mode = NormalizeEnum(
                    updates.Mode,
                    UpdateMode.Recommended)
            },

            Providers = providers with
            {
                ActiveProviderId = NormalizeRequiredText(
                    providers.ActiveProviderId,
                    AppSettings.DefaultProviderId,
                    AppSettingsValidator.MaximumProviderIdLength)
                    .ToLowerInvariant(),

                DisplayMode = NormalizeEnum(
                    providers.DisplayMode,
                    ProviderDisplayMode.Single)
            }
        };
    }

    private static int UseValueOrFallback(
        int value,
        int minimum,
        int maximum,
        int fallback)
    {
        return value < minimum || value > maximum
            ? fallback
            : value;
    }

    private static double UseValueOrFallback(
        double value,
        double minimum,
        double maximum,
        double fallback)
    {
        return double.IsNaN(value) ||
               double.IsInfinity(value) ||
               value < minimum ||
               value > maximum
            ? fallback
            : value;
    }

    private static double? NormalizeCoordinate(
        double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        double coordinate = value.Value;

        if (double.IsNaN(coordinate) ||
            double.IsInfinity(coordinate) ||
            Math.Abs(coordinate) >
            AppSettingsValidator.MaximumAbsoluteCoordinate)
        {
            return null;
        }

        return coordinate;
    }

    private static string NormalizeRequiredText(
        string? value,
        string fallback,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string normalized = value.Trim();

        return normalized.Length > maximumLength ||
               normalized.Any(char.IsControl)
            ? fallback
            : normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();

        return normalized.Length > maximumLength ||
               normalized.Any(char.IsControl)
            ? null
            : normalized;
    }

    private static TEnum NormalizeEnum<TEnum>(
        TEnum value,
        TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : fallback;
    }
}
