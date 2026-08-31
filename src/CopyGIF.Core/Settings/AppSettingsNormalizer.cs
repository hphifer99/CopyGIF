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

        StartupSettings startup =
            settings.Startup ?? new StartupSettings();

        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,

            Hotkey = string.IsNullOrWhiteSpace(settings.Hotkey)
                ? "Alt+G"
                : settings.Hotkey.Trim(),

            Search = search with
            {
                ResultsPerSearch = Clamp(
                    search.ResultsPerSearch,
                    6,
                    50,
                    24),

                DebounceMilliseconds = Clamp(
                    search.DebounceMilliseconds,
                    150,
                    2000,
                    300)
            },

            Library = library with
            {
                RecentLimit = Clamp(
                    library.RecentLimit,
                    1,
                    100,
                    30),

                FavoriteLimit = Clamp(
                    library.FavoriteLimit,
                    1,
                    500,
                    100)
            },

            Window = window with
            {
                Width = Clamp(
                    window.Width,
                    520,
                    1800,
                    760),

                Height = Clamp(
                    window.Height,
                    400,
                    1400,
                    560),

                Left = NormalizeCoordinate(window.Left),
                Top = NormalizeCoordinate(window.Top)
            },

            Behavior = behavior,
            Startup = startup
        };
    }

    private static int Clamp(
        int value,
        int minimum,
        int maximum,
        int fallback)
    {
        return value < minimum || value > maximum
            ? fallback
            : value;
    }

    private static double Clamp(
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

    private static double? NormalizeCoordinate(double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        double coordinate = value.Value;

        if (double.IsNaN(coordinate) ||
            double.IsInfinity(coordinate) ||
            Math.Abs(coordinate) > 10_000_000)
        {
            return null;
        }

        return coordinate;
    }
}