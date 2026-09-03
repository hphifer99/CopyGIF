using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;

namespace CopyGIF.Testing;

public sealed class FakeApplicationPaths :
    IApplicationPaths
{
    private readonly List<string?>
        _ensuredLibraryRoots = [];

    public FakeApplicationPaths(
        string? rootDirectory = null)
    {
        RootDirectory =
            Path.GetFullPath(
                rootDirectory ??
                Path.Combine(
                    Path.GetTempPath(),
                    "CopyGIF.Testing",
                    Guid.NewGuid().ToString("N")));
    }

    public string RootDirectory { get; }

    public string SettingsPath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.SettingsFileName);

    public string SettingsBackupPath =>
        SettingsPath + ".bak";

    public string LibraryPath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.LibraryFileName);

    public string LibraryBackupPath =>
        LibraryPath + ".bak";

    public string SearchHistoryPath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.SearchHistoryFileName);

    public string SearchHistoryBackupPath =>
        SearchHistoryPath + ".bak";

    public string UpdateStatePath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.UpdateStateFileName);

    public string UpdateStateBackupPath =>
        UpdateStatePath + ".bak";

    public string MigrationStatePath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.MigrationStateFileName);

    public string MigrationStateBackupPath =>
        MigrationStatePath + ".bak";

    public string SecretsDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.SecretsDirectoryName);

    public string CacheDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.CacheDirectoryName);

    public string ThumbnailCacheDirectory =>
        Path.Combine(
            CacheDirectory,
            StoragePolicy.ThumbnailCacheDirectoryName);

    public string PreviewCacheDirectory =>
        Path.Combine(
            CacheDirectory,
            StoragePolicy.PreviewCacheDirectoryName);

    public string ClipboardCacheDirectory =>
        Path.Combine(
            CacheDirectory,
            StoragePolicy.ClipboardCacheDirectoryName);

    public string UpdatesDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.UpdatesDirectoryName);

    public string LogsDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.LogsDirectoryName);

    public string MigrationDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.MigrationDirectoryName);

    public string FavoritesDirectory =>
        GetFavoritesDirectory(
            customStorageRoot: null);

    public string RecentsDirectory =>
        GetRecentsDirectory(
            customStorageRoot: null);

    public int EnsureDirectoriesCallCount { get; private set; }

    public IReadOnlyList<string?> EnsuredLibraryRoots =>
        _ensuredLibraryRoots.ToArray();

    public string GetLibraryRoot(
        string? customStorageRoot)
    {
        return customStorageRoot is null
            ? RootDirectory
            : Path.Combine(
                Path.GetFullPath(
                    customStorageRoot),
                StoragePolicy.LibraryRootDirectoryName);
    }

    public string GetFavoritesDirectory(
        string? customStorageRoot)
    {
        return Path.Combine(
            GetLibraryRoot(
                customStorageRoot),
            StoragePolicy.FavoritesDirectoryName);
    }

    public string GetRecentsDirectory(
        string? customStorageRoot)
    {
        return Path.Combine(
            GetLibraryRoot(
                customStorageRoot),
            StoragePolicy.RecentsDirectoryName);
    }

    public void EnsureDirectoriesExist()
    {
        EnsureDirectoriesCallCount++;
    }

    public void EnsureLibraryDirectoriesExist(
        string? customStorageRoot)
    {
        _ensuredLibraryRoots.Add(
            customStorageRoot);
    }
}

public sealed class FakeClipboardService :
    IClipboardService
{
    private readonly List<DownloadedGif>
        _copyAttempts = [];

    private readonly List<DownloadedGif>
        _copiedGifs = [];

    public Func<
        DownloadedGif,
        CancellationToken,
        Task>? CopyHandler
    { get; set; }

    public IReadOnlyList<DownloadedGif> CopyAttempts =>
        _copyAttempts.ToArray();

    public IReadOnlyList<DownloadedGif> CopiedGifs =>
        _copiedGifs.ToArray();

    public async Task CopyGifAsync(
        DownloadedGif gif,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            gif);

        _copyAttempts.Add(
            gif);

        cancellationToken.ThrowIfCancellationRequested();

        if (CopyHandler is not null)
        {
            await CopyHandler(
                    gif,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        _copiedGifs.Add(
            gif);
    }
}

public sealed record FakeGifDownloadRequest(
    GifItem Item,
    GifDownloadPurpose Purpose);

public sealed class FakeGifDownloader :
    IGifDownloader
{
    private readonly List<FakeGifDownloadRequest>
        _requests = [];

    public Func<
        GifItem,
        GifDownloadPurpose,
        CancellationToken,
        Task<DownloadedGif>>? DownloadHandler
    { get; set; }

    public IReadOnlyList<FakeGifDownloadRequest> Requests =>
        _requests.ToArray();

    public Task<DownloadedGif> DownloadAsync(
        GifItem item,
        GifDownloadPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        _requests.Add(
            new FakeGifDownloadRequest(
                item,
                purpose));

        cancellationToken.ThrowIfCancellationRequested();

        if (DownloadHandler is not null)
        {
            return DownloadHandler(
                item,
                purpose,
                cancellationToken);
        }

        return Task.FromResult(
            new DownloadedGif
            {
                Identity =
                    item.StableIdentity,

                SourceUri =
                    item.GifUri,

                FilePath =
                    Path.Combine(
                        Path.GetTempPath(),
                        "CopyGIF.Testing",
                        $"{item.ProviderId}-{item.Id}.gif"),

                SizeBytes =
                    item.SizeBytes ?? 6,

                Sha256 =
                    new string(
                        '0',
                        64),

                DownloadedAtUtc =
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),

                Purpose = purpose
            });
    }
}

public sealed class FakePreviewCache :
    IPreviewCache
{
    private readonly Dictionary<
        PreviewCacheKey,
        PreviewCacheEntry> _entries = new();

    public int CleanupCallCount { get; private set; }

    public Func<
        Uri,
        PreviewCacheKind,
        CancellationToken,
        Task<PreviewCacheEntry?>>? TryGetHandler
    { get; set; }

    public Func<
        Uri,
        PreviewCacheKind,
        Stream,
        CancellationToken,
        Task<PreviewCacheEntry>>? StoreHandler
    { get; set; }

    public Func<
        Uri,
        PreviewCacheKind,
        CancellationToken,
        Task>? RemoveHandler
    { get; set; }

    public Func<
        CancellationToken,
        Task>? CleanupHandler
    { get; set; }

    public Task<PreviewCacheEntry?> TryGetAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            sourceUri);

        cancellationToken.ThrowIfCancellationRequested();

        if (TryGetHandler is not null)
        {
            return TryGetHandler(
                sourceUri,
                kind,
                cancellationToken);
        }

        _entries.TryGetValue(
            new PreviewCacheKey(
                sourceUri,
                kind),
            out PreviewCacheEntry? entry);

        return Task.FromResult(
            entry);
    }

    public async Task<PreviewCacheEntry> StoreAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            sourceUri);

        ArgumentNullException.ThrowIfNull(
            content);

        cancellationToken.ThrowIfCancellationRequested();

        PreviewCacheEntry entry;

        if (StoreHandler is not null)
        {
            entry =
                await StoreHandler(
                        sourceUri,
                        kind,
                        content,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            DateTimeOffset timestamp =
                new(
                    2026,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero);

            entry = new PreviewCacheEntry
            {
                SourceUri = sourceUri,
                Kind = kind,

                FilePath =
                    Path.Combine(
                        Path.GetTempPath(),
                        "CopyGIF.Testing",
                        $"{kind}.cache"),

                SizeBytes =
                    content.CanSeek
                        ? content.Length
                        : 0,

                CreatedAtUtc = timestamp,
                LastAccessedAtUtc = timestamp
            };
        }

        _entries[
            new PreviewCacheKey(
                sourceUri,
                kind)] = entry;

        return entry;
    }

    public async Task RemoveAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            sourceUri);

        cancellationToken.ThrowIfCancellationRequested();

        if (RemoveHandler is not null)
        {
            await RemoveHandler(
                    sourceUri,
                    kind,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        _entries.Remove(
            new PreviewCacheKey(
                sourceUri,
                kind));
    }

    public async Task CleanupAsync(
        CancellationToken cancellationToken = default)
    {
        CleanupCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        if (CleanupHandler is not null)
        {
            await CleanupHandler(
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed record PreviewCacheKey(
        Uri SourceUri,
        PreviewCacheKind Kind);
}

public sealed class FakeHotkeyService :
    IHotkeyService
{
    private readonly List<string>
        _registrationAttempts = [];

    public event EventHandler? Activated;

    public string? RegisteredGesture { get; private set; }

    public int UnregisterCallCount { get; private set; }

    public Func<
        string,
        CancellationToken,
        Task<HotkeyRegistrationResult>>?
        RegistrationHandler
    { get; set; }

    public Func<
        CancellationToken,
        Task>? UnregisterHandler
    { get; set; }

    public IReadOnlyList<string> RegistrationAttempts =>
        _registrationAttempts.ToArray();

    public async Task<HotkeyRegistrationResult>
        TryRegisterAsync(
            string gesture,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            gesture);

        _registrationAttempts.Add(
            gesture);

        cancellationToken.ThrowIfCancellationRequested();

        HotkeyRegistrationResult result =
            RegistrationHandler is null
                ? HotkeyRegistrationResult.Success()
                : await RegistrationHandler(
                        gesture,
                        cancellationToken)
                    .ConfigureAwait(false);

        if (result.Succeeded)
        {
            RegisteredGesture = gesture;
        }

        return result;
    }

    public async Task UnregisterAsync(
        CancellationToken cancellationToken = default)
    {
        UnregisterCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        if (UnregisterHandler is not null)
        {
            await UnregisterHandler(
                    cancellationToken)
                .ConfigureAwait(false);
        }

        RegisteredGesture = null;
    }

    public void RaiseActivated()
    {
        Activated?.Invoke(
            this,
            EventArgs.Empty);
    }
}

public sealed class FakeInstallChannelService :
    IInstallChannelService
{
    public InstallationContext Context { get; set; } =
        new()
        {
            Channel = InstallChannel.Msi,
            Scope = InstallScope.AllUsers
        };

    public int CallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<InstallationContext>>? Handler
    { get; set; }

    public Task<InstallationContext> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return Handler is null
            ? Task.FromResult(
                Context)
            : Handler(
                cancellationToken);
    }
}

public sealed class FakeSingleInstanceService :
    ISingleInstanceService
{
    private readonly List<IReadOnlyList<string>>
        _initializationArguments = [];

    public event EventHandler<ActivationRequestedEventArgs>?
        ActivationRequested;

    public SingleInstanceResult Result { get; set; } =
        new()
        {
            Status = SingleInstanceStatus.PrimaryInstance
        };

    public bool WasDisposed { get; private set; }

    public Func<
        IReadOnlyList<string>,
        CancellationToken,
        Task<SingleInstanceResult>>?
        InitializeHandler
    { get; set; }

    public IReadOnlyList<IReadOnlyList<string>>
        InitializationArguments =>
            _initializationArguments.ToArray();

    public Task<SingleInstanceResult> InitializeAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        string[] capturedArguments =
            arguments.ToArray();

        _initializationArguments.Add(
            capturedArguments);

        cancellationToken.ThrowIfCancellationRequested();

        return InitializeHandler is null
            ? Task.FromResult(
                Result)
            : InitializeHandler(
                capturedArguments,
                cancellationToken);
    }

    public void RaiseActivationRequested(
        params string[] arguments)
    {
        ActivationRequested?.Invoke(
            this,
            new ActivationRequestedEventArgs(
                arguments));
    }

    public ValueTask DisposeAsync()
    {
        WasDisposed = true;

        return ValueTask.CompletedTask;
    }
}

public sealed class FakeStartupService :
    IStartupService
{
    private readonly List<bool>
        _requestedStates = [];

    public bool IsEnabled { get; set; }

    public int IsEnabledCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<bool>>? IsEnabledHandler
    { get; set; }

    public Func<
        bool,
        CancellationToken,
        Task>? SetEnabledHandler
    { get; set; }

    public IReadOnlyList<bool> RequestedStates =>
        _requestedStates.ToArray();

    public Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        IsEnabledCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return IsEnabledHandler is null
            ? Task.FromResult(
                IsEnabled)
            : IsEnabledHandler(
                cancellationToken);
    }

    public async Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        _requestedStates.Add(
            enabled);

        cancellationToken.ThrowIfCancellationRequested();

        if (SetEnabledHandler is not null)
        {
            await SetEnabledHandler(
                    enabled,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        IsEnabled = enabled;
    }
}

public sealed record FakeTrayNotification(
    string Title,
    string Message);

public sealed class FakeTrayService :
    ITrayService
{
    private readonly List<FakeTrayNotification>
        _notifications = [];

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public int InitializeCallCount { get; private set; }

    public bool WasDisposed { get; private set; }

    public Func<
        CancellationToken,
        Task>? InitializeHandler
    { get; set; }

    public Func<
        string,
        string,
        CancellationToken,
        Task>? NotificationHandler
    { get; set; }

    public IReadOnlyList<FakeTrayNotification> Notifications =>
        _notifications.ToArray();

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        InitializeCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        if (InitializeHandler is not null)
        {
            await InitializeHandler(
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task ShowNotificationAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            title);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        _notifications.Add(
            new FakeTrayNotification(
                title,
                message));

        cancellationToken.ThrowIfCancellationRequested();

        if (NotificationHandler is not null)
        {
            await NotificationHandler(
                    title,
                    message,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public void RaiseOpenRequested()
    {
        OpenRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void RaiseSettingsRequested()
    {
        SettingsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void RaiseExitRequested()
    {
        ExitRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    public ValueTask DisposeAsync()
    {
        WasDisposed = true;

        return ValueTask.CompletedTask;
    }
}

public sealed class FakeUpdateFeed :
    IUpdateFeed
{
    private readonly List<string>
        _requestedChannels = [];

    public UpdateManifest? LatestManifest { get; set; }

    public Func<
        string,
        CancellationToken,
        Task<UpdateManifest?>>? Handler
    { get; set; }

    public IReadOnlyList<string> RequestedChannels =>
        _requestedChannels.ToArray();

    public Task<UpdateManifest?> GetLatestAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            channel);

        _requestedChannels.Add(
            channel);

        cancellationToken.ThrowIfCancellationRequested();

        return Handler is null
            ? Task.FromResult(
                LatestManifest)
            : Handler(
                channel,
                cancellationToken);
    }
}

public sealed class FakeUpdatePackageService :
    IUpdatePackageService
{
    private readonly List<UpdateManifest>
        _downloadRequests = [];

    private readonly List<DownloadedUpdatePackage>
        _deletedPackages = [];

    public Func<
        UpdateManifest,
        IProgress<UpdateDownloadProgress>?,
        CancellationToken,
        Task<DownloadedUpdatePackage>>?
        DownloadHandler
    { get; set; }

    public Func<
        DownloadedUpdatePackage,
        CancellationToken,
        Task>? DeleteHandler
    { get; set; }

    public IReadOnlyList<UpdateManifest> DownloadRequests =>
        _downloadRequests.ToArray();

    public IReadOnlyList<DownloadedUpdatePackage> DeletedPackages =>
        _deletedPackages.ToArray();

    public Task<DownloadedUpdatePackage> DownloadAsync(
        UpdateManifest manifest,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            manifest);

        _downloadRequests.Add(
            manifest);

        cancellationToken.ThrowIfCancellationRequested();

        if (DownloadHandler is not null)
        {
            return DownloadHandler(
                manifest,
                progress,
                cancellationToken);
        }

        progress?.Report(
            new UpdateDownloadProgress
            {
                BytesReceived = manifest.SizeBytes,
                TotalBytes = manifest.SizeBytes
            });

        return Task.FromResult(
            new DownloadedUpdatePackage
            {
                Manifest = manifest,

                FilePath =
                    Path.Combine(
                        Path.GetTempPath(),
                        "CopyGIF.Testing",
                        manifest.AssetName),

                SizeBytes = manifest.SizeBytes,
                Sha256 = manifest.Sha256,

                DownloadedAtUtc =
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.Zero)
            });
    }

    public async Task DeleteAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        cancellationToken.ThrowIfCancellationRequested();

        if (DeleteHandler is not null)
        {
            await DeleteHandler(
                    package,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        _deletedPackages.Add(
            package);
    }
}

public sealed class FakeUpdateInstaller :
    IUpdateInstaller
{
    private readonly List<DownloadedUpdatePackage>
        _verificationRequests = [];

    private readonly List<DownloadedUpdatePackage>
        _installationRequests = [];

    public Func<
        DownloadedUpdatePackage,
        CancellationToken,
        Task<UpdatePackageVerificationResult>>?
        VerificationHandler
    { get; set; }

    public Func<
        DownloadedUpdatePackage,
        CancellationToken,
        Task>? InstallationHandler
    { get; set; }

    public IReadOnlyList<DownloadedUpdatePackage>
        VerificationRequests =>
            _verificationRequests.ToArray();

    public IReadOnlyList<DownloadedUpdatePackage>
        InstallationRequests =>
            _installationRequests.ToArray();

    public Task<UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        _verificationRequests.Add(
            package);

        cancellationToken.ThrowIfCancellationRequested();

        return VerificationHandler is null
            ? Task.FromResult(
                UpdatePackageVerificationResult.Valid())
            : VerificationHandler(
                package,
                cancellationToken);
    }

    public async Task InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        _installationRequests.Add(
            package);

        cancellationToken.ThrowIfCancellationRequested();

        if (InstallationHandler is not null)
        {
            await InstallationHandler(
                    package,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

public sealed class FakeMigrationCoordinator :
    IMigrationCoordinator
{
    public MigrationResult Result { get; set; } =
        new()
        {
            Status = MigrationStatus.NotRequired
        };

    public int CallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<MigrationResult>>? Handler
    { get; set; }

    public Task<MigrationResult> MigrateIfNeededAsync(
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return Handler is null
            ? Task.FromResult(
                Result)
            : Handler(
                cancellationToken);
    }
}

public sealed record FakeLibraryMoveRequest(
    string SourceOwnedRoot,
    string DestinationOwnedRoot,
    IReadOnlyCollection<string> FilePaths);

public sealed record FakeLibraryDeleteRequest(
    string OwnedRoot,
    IReadOnlyCollection<string> FilePaths);

public sealed class FakeLibraryStorageMover :
    ILibraryStorageMover
{
    private readonly List<FakeLibraryMoveRequest>
        _moveRequests = [];

    private readonly List<FakeLibraryDeleteRequest>
        _deleteRequests = [];

    public Func<
        string,
        string,
        IReadOnlyCollection<string>,
        CancellationToken,
        Task<LibraryStorageMoveResult>>?
        MoveHandler
    { get; set; }

    public Func<
        string,
        IReadOnlyCollection<string>,
        CancellationToken,
        Task>? DeleteHandler
    { get; set; }

    public IReadOnlyList<FakeLibraryMoveRequest> MoveRequests =>
        _moveRequests.ToArray();

    public IReadOnlyList<FakeLibraryDeleteRequest> DeleteRequests =>
        _deleteRequests.ToArray();

    public Task<LibraryStorageMoveResult> MoveAsync(
        string sourceOwnedRoot,
        string destinationOwnedRoot,
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceOwnedRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationOwnedRoot);

        ArgumentNullException.ThrowIfNull(
            filePaths);

        string[] capturedPaths =
            filePaths.ToArray();

        _moveRequests.Add(
            new FakeLibraryMoveRequest(
                sourceOwnedRoot,
                destinationOwnedRoot,
                capturedPaths));

        cancellationToken.ThrowIfCancellationRequested();

        if (MoveHandler is not null)
        {
            return MoveHandler(
                sourceOwnedRoot,
                destinationOwnedRoot,
                capturedPaths,
                cancellationToken);
        }

        Dictionary<string, string> movedPaths =
            capturedPaths.ToDictionary(
                filePath =>
                    Path.GetFullPath(
                        filePath),
                filePath =>
                    Path.Combine(
                        Path.GetFullPath(
                            destinationOwnedRoot),
                        Path.GetRelativePath(
                            Path.GetFullPath(
                                sourceOwnedRoot),
                            Path.GetFullPath(
                                filePath))),
                OperatingSystem.IsWindows()
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);

        return Task.FromResult(
            new LibraryStorageMoveResult
            {
                MovedPaths = movedPaths
            });
    }

    public async Task DeleteAsync(
        string ownedRoot,
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            ownedRoot);

        ArgumentNullException.ThrowIfNull(
            filePaths);

        string[] capturedPaths =
            filePaths.ToArray();

        _deleteRequests.Add(
            new FakeLibraryDeleteRequest(
                ownedRoot,
                capturedPaths));

        cancellationToken.ThrowIfCancellationRequested();

        if (DeleteHandler is not null)
        {
            await DeleteHandler(
                    ownedRoot,
                    capturedPaths,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

public sealed class FakeFolderPickerService :
    IFolderPickerService
{
    private readonly List<string?>
        _initialDirectories = [];

    public string? SelectedFolder { get; set; }

    public Func<
        string?,
        CancellationToken,
        Task<string?>>? Handler
    { get; set; }

    public IReadOnlyList<string?> InitialDirectories =>
        _initialDirectories.ToArray();

    public Task<string?> PickFolderAsync(
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        _initialDirectories.Add(
            initialDirectory);

        cancellationToken.ThrowIfCancellationRequested();

        return Handler is null
            ? Task.FromResult(
                SelectedFolder)
            : Handler(
                initialDirectory,
                cancellationToken);
    }
}

public sealed class FakeUriLauncherService :
    IUriLauncherService
{
    private readonly List<Uri>
        _launchRequests = [];

    public bool Result { get; set; } = true;

    public Func<
        Uri,
        CancellationToken,
        Task<bool>>? Handler
    { get; set; }

    public IReadOnlyList<Uri> LaunchRequests =>
        _launchRequests.ToArray();

    public Task<bool> TryLaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            uri);

        _launchRequests.Add(
            uri);

        cancellationToken.ThrowIfCancellationRequested();

        return Handler is null
            ? Task.FromResult(
                Result)
            : Handler(
                uri,
                cancellationToken);
    }
}

public sealed class FakeWindowPlacementService :
    IWindowPlacementService
{
    private readonly List<WindowSettings>
        _requests = [];

    public WindowPlacementResult Result { get; set; } =
        new()
        {
            Left = 100,
            Top = 100,
            Width = 760,
            Height = 560,
            MonitorId = "FakeMonitor"
        };

    public Func<
        WindowSettings,
        CancellationToken,
        Task<WindowPlacementResult>>? Handler
    { get; set; }

    public IReadOnlyList<WindowSettings> Requests =>
        _requests.ToArray();

    public Task<WindowPlacementResult> CalculateAsync(
        WindowSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        _requests.Add(
            settings);

        cancellationToken.ThrowIfCancellationRequested();

        return Handler is null
            ? Task.FromResult(
                Result)
            : Handler(
                settings,
                cancellationToken);
    }
}
