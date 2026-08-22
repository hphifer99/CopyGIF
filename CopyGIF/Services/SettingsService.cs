using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace CopyGIF.Services
{
    public sealed class SettingsService
    {
        public string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "CopyGIF");

        public string SettingsPath =>
            Path.Combine(SettingsDirectory, "settings.json");

        public string LibraryPath =>
            Path.Combine(SettingsDirectory, "library.json");

        public string FavoritesDirectory =>
            Path.Combine(SettingsDirectory, "Favorites");

        public string RecentsDirectory =>
            Path.Combine(SettingsDirectory, "Recents");

        public AppSettings LoadOrCreate()
        {
            Directory.CreateDirectory(SettingsDirectory);

            if (!File.Exists(SettingsPath))
            {
                var newSettings = AppSettings.CreateDefault();
                Save(newSettings);
                return newSettings;
            }

            var serializer =
                new DataContractJsonSerializer(typeof(AppSettings));

            using (var stream = File.OpenRead(SettingsPath))
            {
                var settings =
                    serializer.ReadObject(stream) as AppSettings
                    ?? AppSettings.CreateDefault();

                settings.Normalize();
                return settings;
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Normalize();
            Directory.CreateDirectory(SettingsDirectory);

            string temporaryPath = SettingsPath + ".tmp";

            var serializer =
                new DataContractJsonSerializer(typeof(AppSettings));

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    serializer.WriteObject(stream, settings);
                    stream.Flush(true);
                }

                if (File.Exists(SettingsPath))
                {
                    string backupPath = SettingsPath + ".bak";

                    File.Replace(
                        temporaryPath,
                        SettingsPath,
                        backupPath,
                        true);

                    TryDelete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, SettingsPath);
                }
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [DataContract]
    public sealed class AppSettings
    {
        private const string MousePlacement = "Mouse";
        private const string RememberPlacement = "Remember";
        private const string CenterPlacement = "Center";

        [IgnoreDataMember]
        public string ApiKey { get; set; } = string.Empty;

        [DataMember(Name = "ApiKeyProtected", EmitDefaultValue = false)]
        private string ApiKeyProtected { get; set; }

        [DataMember(Name = "ApiKey", EmitDefaultValue = false)]
        private string LegacyApiKey { get; set; }

        [DataMember(Name = "Hotkey")]
        public string Hotkey { get; set; } = "Alt+G";

        [DataMember(Name = "ResultsPerSearch")]
        public int ResultsPerSearch { get; set; } = 24;

        [DataMember(Name = "SearchDebounceMilliseconds")]
        public int SearchDebounceMilliseconds { get; set; } = 300;

        [DataMember(Name = "RecentLimit")]
        public int RecentLimit { get; set; } = 30;

        [DataMember(Name = "FavoriteLimit")]
        public int FavoriteLimit { get; set; } = 100;

        [DataMember(Name = "WindowPlacementMode")]
        public string WindowPlacementMode { get; set; } = MousePlacement;

        [DataMember(Name = "CloseWhenFocusLost")]
        public bool CloseWhenFocusLost { get; set; } = true;

        [DataMember(Name = "HideAfterCopy")]
        public bool HideAfterCopy { get; set; } = true;

        [DataMember(Name = "StoreFavoritesLocally")]
        public bool StoreFavoritesLocally { get; set; } = true;

        [DataMember(Name = "StoreRecentsLocally")]
        public bool StoreRecentsLocally { get; set; } = true;

        [DataMember(Name = "RememberWindowSize")]
        public bool RememberWindowSize { get; set; } = true;

        [DataMember(Name = "AnimatePreviews")]
        public bool AnimatePreviews { get; set; } = true;

        [DataMember(Name = "StartWithWindows")]
        public bool StartWithWindows { get; set; } = true;

        [DataMember(Name = "AutoLoadMoreResults")]
        public bool AutoLoadMoreResults { get; set; }

        [DataMember(Name = "WindowLeft")]
        public double WindowLeft { get; set; }

        [DataMember(Name = "WindowTop")]
        public double WindowTop { get; set; }

        [DataMember(Name = "WindowWidth")]
        public double WindowWidth { get; set; } = 760;

        [DataMember(Name = "WindowHeight")]
        public double WindowHeight { get; set; } = 560;

        [DataMember(Name = "HasSavedWindowPlacement")]
        public bool HasSavedWindowPlacement { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                ApiKey = ApiKey,
                Hotkey = Hotkey,
                ResultsPerSearch = ResultsPerSearch,
                SearchDebounceMilliseconds = SearchDebounceMilliseconds,
                RecentLimit = RecentLimit,
                FavoriteLimit = FavoriteLimit,
                WindowPlacementMode = WindowPlacementMode,
                CloseWhenFocusLost = CloseWhenFocusLost,
                HideAfterCopy = HideAfterCopy,
                StoreFavoritesLocally = StoreFavoritesLocally,
                StoreRecentsLocally = StoreRecentsLocally,
                RememberWindowSize = RememberWindowSize,
                AnimatePreviews = AnimatePreviews,
                StartWithWindows = StartWithWindows,
                AutoLoadMoreResults = AutoLoadMoreResults,
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                HasSavedWindowPlacement = HasSavedWindowPlacement
            };
        }

        public void Normalize()
        {
            ApiKey = (ApiKey ?? string.Empty).Trim();
            Hotkey = string.IsNullOrWhiteSpace(Hotkey)
                ? "Alt+G"
                : Hotkey.Trim();

            ResultsPerSearch = Clamp(ResultsPerSearch, 6, 50, 24);
            SearchDebounceMilliseconds =
                Clamp(SearchDebounceMilliseconds, 150, 2000, 300);
            RecentLimit = Clamp(RecentLimit, 1, 100, 30);
            FavoriteLimit = Clamp(FavoriteLimit, 1, 500, 100);

            if (!string.Equals(WindowPlacementMode, MousePlacement,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(WindowPlacementMode, RememberPlacement,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(WindowPlacementMode, CenterPlacement,
                    StringComparison.OrdinalIgnoreCase))
            {
                WindowPlacementMode = MousePlacement;
            }

            WindowWidth = Clamp(WindowWidth, 520, 1800, 760);
            WindowHeight = Clamp(WindowHeight, 400, 1400, 560);

            if (double.IsNaN(WindowLeft) ||
                double.IsInfinity(WindowLeft) ||
                double.IsNaN(WindowTop) ||
                double.IsInfinity(WindowTop) ||
                Math.Abs(WindowLeft) > 10000000 ||
                Math.Abs(WindowTop) > 10000000)
            {
                WindowLeft = 0;
                WindowTop = 0;
                HasSavedWindowPlacement = false;
            }
        }

        public string ValidateForSave()
        {
            if (ResultsPerSearch < 6 || ResultsPerSearch > 50)
            {
                return "Results per search must be between 6 and 50.";
            }

            if (SearchDebounceMilliseconds < 150 ||
                SearchDebounceMilliseconds > 2000)
            {
                return "Search delay must be between 150 and 2000 milliseconds.";
            }

            if (RecentLimit < 1 || RecentLimit > 100)
            {
                return "Recent GIF limit must be between 1 and 100.";
            }

            if (FavoriteLimit < 1 || FavoriteLimit > 500)
            {
                return "Favorite GIF limit must be between 1 and 500.";
            }

            return null;
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            ApiKey = string.Empty;
            Hotkey = "Alt+G";
            ResultsPerSearch = 24;
            SearchDebounceMilliseconds = 300;
            RecentLimit = 30;
            FavoriteLimit = 100;
            WindowPlacementMode = MousePlacement;
            CloseWhenFocusLost = true;
            HideAfterCopy = true;
            StoreFavoritesLocally = true;
            StoreRecentsLocally = true;
            RememberWindowSize = true;
            AnimatePreviews = true;
            StartWithWindows = true;
            AutoLoadMoreResults = false;
            WindowWidth = 760;
            WindowHeight = 560;
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            LegacyApiKey = null;

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                ApiKeyProtected = null;
                return;
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(ApiKey);
            byte[] protectedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser);

            ApiKeyProtected = Convert.ToBase64String(protectedBytes);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (!string.IsNullOrWhiteSpace(ApiKeyProtected))
            {
                byte[] protectedBytes =
                    Convert.FromBase64String(ApiKeyProtected);

                byte[] plainBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                ApiKey = Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                ApiKey = LegacyApiKey ?? string.Empty;
            }

            LegacyApiKey = null;
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
    }
}
