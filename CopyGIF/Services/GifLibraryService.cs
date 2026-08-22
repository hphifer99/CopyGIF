using CopyGIF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CopyGIF.Services
{
    public sealed class GifLibraryService
    {
        private readonly SettingsService _settingsService;
        private readonly ClipboardService _clipboardService;
        private GifLibraryData _library = new GifLibraryData();

        public GifLibraryService(
            SettingsService settingsService,
            ClipboardService clipboardService)
        {
            _settingsService = settingsService
                ?? throw new ArgumentNullException(nameof(settingsService));

            _clipboardService = clipboardService
                ?? throw new ArgumentNullException(nameof(clipboardService));
        }

        public IReadOnlyList<GifItem> Favorites =>
            _library.Favorites.Select(item => item.Clone()).ToList();

        public IReadOnlyList<GifItem> Recents =>
            _library.Recents.Select(item => item.Clone()).ToList();

        public void Load()
        {
            Directory.CreateDirectory(_settingsService.SettingsDirectory);
            Directory.CreateDirectory(_settingsService.FavoritesDirectory);
            Directory.CreateDirectory(_settingsService.RecentsDirectory);

            if (!File.Exists(_settingsService.LibraryPath))
            {
                _library = new GifLibraryData();
                Save();
                return;
            }

            var serializer =
                new DataContractJsonSerializer(typeof(GifLibraryData));

            using (var stream = File.OpenRead(_settingsService.LibraryPath))
            {
                _library =
                    serializer.ReadObject(stream) as GifLibraryData
                    ?? new GifLibraryData();
            }

            _library.Favorites =
                _library.Favorites ?? new List<GifItem>();

            _library.Recents =
                _library.Recents ?? new List<GifItem>();

            foreach (GifItem favorite in _library.Favorites)
            {
                favorite.IsFavorite = true;
                ClearMissingLocalPath(favorite);
            }

            foreach (GifItem recent in _library.Recents)
            {
                recent.IsFavorite = IsFavorite(recent);
                ClearMissingLocalPath(recent);
            }
        }

        public bool IsFavorite(GifItem gif)
        {
            return gif != null &&
                   _library.Favorites.Any(item => item.HasSameIdentity(gif));
        }

        public void MarkFavoriteState(IEnumerable<GifItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (GifItem item in items)
            {
                item.IsFavorite = IsFavorite(item);
            }
        }

        public async Task<bool> ToggleFavoriteAsync(
            GifItem gif,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (gif == null)
            {
                throw new ArgumentNullException(nameof(gif));
            }

            GifItem existing = _library.Favorites
                .FirstOrDefault(item => item.HasSameIdentity(gif));

            if (existing != null)
            {
                _library.Favorites.Remove(existing);
                TryDeleteOwnedFile(
                    existing.LocalFilePath,
                    _settingsService.FavoritesDirectory);

                UpdateFavoriteFlags(gif, false);
                Save();
                return false;
            }

            GifItem storedItem = gif.Clone();
            storedItem.AddedUtc = DateTime.UtcNow;
            storedItem.IsFavorite = true;

            if (settings.StoreFavoritesLocally)
            {
                storedItem.LocalFilePath =
                    await _clipboardService.CacheGifAsync(
                        gif,
                        _settingsService.FavoritesDirectory,
                        ClipboardService.BuildSafeStem(gif) + ".gif",
                        false,
                        cancellationToken);
            }
            else
            {
                storedItem.LocalFilePath = null;
            }

            _library.Favorites.Insert(0, storedItem);
            TrimList(
                _library.Favorites,
                settings.FavoriteLimit,
                _settingsService.FavoritesDirectory);

            UpdateFavoriteFlags(gif, true);
            Save();
            return true;
        }

        public async Task AddRecentAsync(
            GifItem gif,
            string copiedFilePath,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (gif == null)
            {
                throw new ArgumentNullException(nameof(gif));
            }

            GifItem existing = _library.Recents
                .FirstOrDefault(item => item.HasSameIdentity(gif));

            if (existing != null)
            {
                _library.Recents.Remove(existing);

                if (!settings.StoreRecentsLocally)
                {
                    TryDeleteOwnedFile(
                        existing.LocalFilePath,
                        _settingsService.RecentsDirectory);
                }
            }

            GifItem storedItem = gif.Clone();
            storedItem.AddedUtc = DateTime.UtcNow;
            storedItem.IsFavorite = IsFavorite(gif);

            if (settings.StoreRecentsLocally)
            {
                var cacheSource = gif.Clone();
                cacheSource.LocalFilePath = copiedFilePath;

                storedItem.LocalFilePath =
                    await _clipboardService.CacheGifAsync(
                        cacheSource,
                        _settingsService.RecentsDirectory,
                        ClipboardService.BuildSafeStem(gif) + ".gif",
                        true,
                        cancellationToken);
            }
            else
            {
                storedItem.LocalFilePath = null;
            }

            _library.Recents.Insert(0, storedItem);
            TrimList(
                _library.Recents,
                settings.RecentLimit,
                _settingsService.RecentsDirectory);

            Save();
        }

        public void TrimToLimits(AppSettings settings)
        {
            TrimList(
                _library.Favorites,
                settings.FavoriteLimit,
                _settingsService.FavoritesDirectory);

            TrimList(
                _library.Recents,
                settings.RecentLimit,
                _settingsService.RecentsDirectory);

            Save();
        }

        public void ClearRecents()
        {
            foreach (GifItem item in _library.Recents)
            {
                TryDeleteOwnedFile(
                    item.LocalFilePath,
                    _settingsService.RecentsDirectory);
            }

            _library.Recents.Clear();
            Save();
        }

        private void UpdateFavoriteFlags(GifItem changedItem, bool isFavorite)
        {
            changedItem.IsFavorite = isFavorite;

            foreach (GifItem recent in _library.Recents)
            {
                if (recent.HasSameIdentity(changedItem))
                {
                    recent.IsFavorite = isFavorite;
                }
            }
        }

        private static void ClearMissingLocalPath(GifItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.LocalFilePath) &&
                !ClipboardService.IsValidGifFile(item.LocalFilePath))
            {
                item.LocalFilePath = null;
            }
        }

        private static void TrimList(
            List<GifItem> items,
            int limit,
            string ownedDirectory)
        {
            while (items.Count > limit)
            {
                GifItem removed = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                TryDeleteOwnedFile(removed.LocalFilePath, ownedDirectory);
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(_settingsService.SettingsDirectory);

            string temporaryPath =
                _settingsService.LibraryPath + ".tmp";

            var serializer =
                new DataContractJsonSerializer(typeof(GifLibraryData));

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    serializer.WriteObject(stream, _library);
                    stream.Flush(true);
                }

                if (File.Exists(_settingsService.LibraryPath))
                {
                    string backupPath =
                        _settingsService.LibraryPath + ".bak";

                    File.Replace(
                        temporaryPath,
                        _settingsService.LibraryPath,
                        backupPath,
                        true);

                    TryDeleteFile(backupPath);
                }
                else
                {
                    File.Move(
                        temporaryPath,
                        _settingsService.LibraryPath);
                }
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }
        }

        private static void TryDeleteOwnedFile(
            string filePath,
            string ownedDirectory)
        {
            if (string.IsNullOrWhiteSpace(filePath) ||
                string.IsNullOrWhiteSpace(ownedDirectory))
            {
                return;
            }

            try
            {
                string fullPath = Path.GetFullPath(filePath);
                string fullDirectory =
                    Path.GetFullPath(ownedDirectory)
                        .TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;

                if (!fullPath.StartsWith(
                        fullDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                TryDeleteFile(fullPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) &&
                    File.Exists(filePath))
                {
                    File.Delete(filePath);
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
    internal sealed class GifLibraryData
    {
        [DataMember(Name = "Favorites")]
        public List<GifItem> Favorites { get; set; } =
            new List<GifItem>();

        [DataMember(Name = "Recents")]
        public List<GifItem> Recents { get; set; } =
            new List<GifItem>();

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            Favorites = new List<GifItem>();
            Recents = new List<GifItem>();
        }
    }
}

