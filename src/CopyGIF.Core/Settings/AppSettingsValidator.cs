namespace CopyGIF.Core.Settings;

public sealed record SettingsValidationIssue(
    string Path,
    string Message);

public static class AppSettingsValidator
{
    public const int MinimumResultsPerSearch = 6;

    public const int MaximumResultsPerSearch = 50;

    public const int MinimumDebounceMilliseconds = 150;

    public const int MaximumDebounceMilliseconds = 2000;

    public const int MinimumSearchHistoryLimit = 1;

    public const int MaximumSearchHistoryLimit = 500;

    public const int MinimumRecentLimit = 1;

    public const int MaximumRecentLimit = 100;

    public const int MinimumFavoriteLimit = 1;

    public const int MaximumFavoriteLimit = 500;

    public const double MinimumWindowWidth = 520;

    public const double MaximumWindowWidth = 1800;

    public const double MinimumWindowHeight = 400;

    public const double MaximumWindowHeight = 1400;

    public const double MaximumAbsoluteCoordinate =
        10_000_000;

    public const int MaximumHotkeyLength = 100;

    public const int MaximumProviderIdLength = 128;

    public const int MaximumMonitorIdLength = 512;

    public const int MaximumStorageRootLength = 32_767;

    public static IReadOnlyList<SettingsValidationIssue>
        Validate(AppSettings? settings)
    {
        List<SettingsValidationIssue> issues = [];

        if (settings is null)
        {
            AddIssue(
                issues,
                "$",
                "Settings are required.");

            return issues;
        }

        ValidateSchemaVersion(
            settings.SchemaVersion,
            issues);

        ValidateRequiredText(
            settings.Hotkey,
            MaximumHotkeyLength,
            "Hotkey",
            issues);

        ValidateSearch(
            settings.Search,
            issues);

        ValidateLibrary(
            settings.Library,
            issues);

        ValidateWindow(
            settings.Window,
            issues);

        ValidateAppearance(
            settings.Appearance,
            issues);

        ValidateUpdates(
            settings.Updates,
            issues);

        ValidateProviders(
            settings.Providers,
            issues);

        if (settings.Behavior is null)
        {
            AddIssue(
                issues,
                "Behavior",
                "Behavior settings are required.");
        }

        if (settings.Startup is null)
        {
            AddIssue(
                issues,
                "Startup",
                "Startup settings are required.");
        }

        return issues;
    }

    public static bool IsValid(
        AppSettings? settings)
    {
        return Validate(settings).Count == 0;
    }

    private static void ValidateSchemaVersion(
        int schemaVersion,
        ICollection<SettingsValidationIssue> issues)
    {
        if (schemaVersion !=
            AppSettings.CurrentSchemaVersion)
        {
            AddIssue(
                issues,
                "SchemaVersion",
                $"SchemaVersion must be " +
                $"{AppSettings.CurrentSchemaVersion}.");
        }
    }

    private static void ValidateSearch(
        SearchSettings? search,
        ICollection<SettingsValidationIssue> issues)
    {
        if (search is null)
        {
            AddIssue(
                issues,
                "Search",
                "Search settings are required.");

            return;
        }

        ValidateRange(
            search.ResultsPerSearch,
            MinimumResultsPerSearch,
            MaximumResultsPerSearch,
            "Search.ResultsPerSearch",
            issues);

        ValidateRange(
            search.DebounceMilliseconds,
            MinimumDebounceMilliseconds,
            MaximumDebounceMilliseconds,
            "Search.DebounceMilliseconds",
            issues);

        ValidateRange(
            search.SearchHistoryLimit,
            MinimumSearchHistoryLimit,
            MaximumSearchHistoryLimit,
            "Search.SearchHistoryLimit",
            issues);
    }

    private static void ValidateLibrary(
        LibrarySettings? library,
        ICollection<SettingsValidationIssue> issues)
    {
        if (library is null)
        {
            AddIssue(
                issues,
                "Library",
                "Library settings are required.");

            return;
        }

        ValidateRange(
            library.RecentLimit,
            MinimumRecentLimit,
            MaximumRecentLimit,
            "Library.RecentLimit",
            issues);

        ValidateRange(
            library.FavoriteLimit,
            MinimumFavoriteLimit,
            MaximumFavoriteLimit,
            "Library.FavoriteLimit",
            issues);

        ValidateOptionalText(
            library.CustomStorageRoot,
            MaximumStorageRootLength,
            "Library.CustomStorageRoot",
            issues);
    }

    private static void ValidateWindow(
        WindowSettings? window,
        ICollection<SettingsValidationIssue> issues)
    {
        if (window is null)
        {
            AddIssue(
                issues,
                "Window",
                "Window settings are required.");

            return;
        }

        ValidateEnum(
            window.PlacementMode,
            "Window.PlacementMode",
            issues);

        ValidateRange(
            window.Width,
            MinimumWindowWidth,
            MaximumWindowWidth,
            "Window.Width",
            issues);

        ValidateRange(
            window.Height,
            MinimumWindowHeight,
            MaximumWindowHeight,
            "Window.Height",
            issues);

        ValidateCoordinate(
            window.Left,
            "Window.Left",
            issues);

        ValidateCoordinate(
            window.Top,
            "Window.Top",
            issues);

        ValidateOptionalText(
            window.LastMonitorId,
            MaximumMonitorIdLength,
            "Window.LastMonitorId",
            issues);
    }

    private static void ValidateAppearance(
        AppearanceSettings? appearance,
        ICollection<SettingsValidationIssue> issues)
    {
        if (appearance is null)
        {
            AddIssue(
                issues,
                "Appearance",
                "Appearance settings are required.");

            return;
        }

        ValidateEnum(
            appearance.Theme,
            "Appearance.Theme",
            issues);
    }

    private static void ValidateUpdates(
        UpdateSettings? updates,
        ICollection<SettingsValidationIssue> issues)
    {
        if (updates is null)
        {
            AddIssue(
                issues,
                "Updates",
                "Update settings are required.");

            return;
        }

        ValidateEnum(
            updates.CheckFrequency,
            "Updates.CheckFrequency",
            issues);

        ValidateEnum(
            updates.Mode,
            "Updates.Mode",
            issues);
    }

    private static void ValidateProviders(
        ProviderSettings? providers,
        ICollection<SettingsValidationIssue> issues)
    {
        if (providers is null)
        {
            AddIssue(
                issues,
                "Providers",
                "Provider settings are required.");

            return;
        }

        ValidateRequiredText(
            providers.ActiveProviderId,
            MaximumProviderIdLength,
            "Providers.ActiveProviderId",
            issues);

        ValidateEnum(
            providers.DisplayMode,
            "Providers.DisplayMode",
            issues);
    }

    private static void ValidateRange(
        int value,
        int minimum,
        int maximum,
        string path,
        ICollection<SettingsValidationIssue> issues)
    {
        if (value < minimum || value > maximum)
        {
            AddIssue(
                issues,
                path,
                $"Value must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateRange(
        double value,
        double minimum,
        double maximum,
        string path,
        ICollection<SettingsValidationIssue> issues)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value) ||
            value < minimum ||
            value > maximum)
        {
            AddIssue(
                issues,
                path,
                $"Value must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateCoordinate(
        double? value,
        string path,
        ICollection<SettingsValidationIssue> issues)
    {
        if (!value.HasValue)
        {
            return;
        }

        double coordinate = value.Value;

        if (double.IsNaN(coordinate) ||
            double.IsInfinity(coordinate) ||
            Math.Abs(coordinate) >
            MaximumAbsoluteCoordinate)
        {
            AddIssue(
                issues,
                path,
                "Coordinate is outside the supported range.");
        }
    }

    private static void ValidateRequiredText(
        string? value,
        int maximumLength,
        string path,
        ICollection<SettingsValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddIssue(
                issues,
                path,
                "Value is required.");

            return;
        }

        ValidateTextContent(
            value,
            maximumLength,
            path,
            issues);
    }

    private static void ValidateOptionalText(
        string? value,
        int maximumLength,
        string path,
        ICollection<SettingsValidationIssue> issues)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            AddIssue(
                issues,
                path,
                "Use null instead of an empty value.");

            return;
        }

        ValidateTextContent(
            value,
            maximumLength,
            path,
            issues);
    }

    private static void ValidateTextContent(
        string value,
        int maximumLength,
        string path,
        ICollection<SettingsValidationIssue> issues)
    {
        if (value.Length > maximumLength)
        {
            AddIssue(
                issues,
                path,
                $"Value cannot exceed {maximumLength} characters.");
        }

        if (value.Any(char.IsControl))
        {
            AddIssue(
                issues,
                path,
                "Value cannot contain control characters.");
        }
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        string path,
        ICollection<SettingsValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            AddIssue(
                issues,
                path,
                "Value is not supported.");
        }
    }

    private static void AddIssue(
        ICollection<SettingsValidationIssue> issues,
        string path,
        string message)
    {
        issues.Add(
            new SettingsValidationIssue(
                path,
                message));
    }
}
