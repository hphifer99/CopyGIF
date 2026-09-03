using System.Runtime.ExceptionServices;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Settings;

public sealed class SettingsCoordinator :
    ISettingsCoordinator,
    IDisposable
{
    private readonly ISettingsStore _settingsStore;

    private readonly ILibraryStore _libraryStore;

    private readonly ILibraryStorageMover _storageMover;

    private readonly IApplicationPaths _paths;

    private readonly IHotkeyService _hotkeyService;

    private readonly IStartupService _startupService;

    private readonly IFolderPickerService _folderPickerService;

    private readonly SemaphoreSlim _gate =
        new(
            initialCount: 1,
            maxCount: 1);

    private readonly StringComparer _pathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private bool _disposed;

    public SettingsCoordinator(
        ISettingsStore settingsStore,
        ILibraryStore libraryStore,
        ILibraryStorageMover storageMover,
        IApplicationPaths paths,
        IHotkeyService hotkeyService,
        IStartupService startupService,
        IFolderPickerService folderPickerService)
    {
        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _libraryStore =
            libraryStore ??
            throw new ArgumentNullException(
                nameof(libraryStore));

        _storageMover =
            storageMover ??
            throw new ArgumentNullException(
                nameof(storageMover));

        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _hotkeyService =
            hotkeyService ??
            throw new ArgumentNullException(
                nameof(hotkeyService));

        _startupService =
            startupService ??
            throw new ArgumentNullException(
                nameof(startupService));

        _folderPickerService =
            folderPickerService ??
            throw new ArgumentNullException(
                nameof(folderPickerService));
    }

    public async Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await LoadNormalizedAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SettingsSaveResult> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            settings);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await SaveCoreAsync(
                    settings,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SettingsSaveResult>
        RestoreDefaultsAsync(
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await SaveCoreAsync(
                    new AppSettings(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SettingsSaveResult?>
        ChooseLibraryStorageRootAsync(
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings current =
                await LoadNormalizedAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            string? selectedRoot =
                await _folderPickerService
                    .PickFolderAsync(
                        current.Library.CustomStorageRoot,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (selectedRoot is null)
            {
                return null;
            }

            AppSettings proposed =
                current with
                {
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                selectedRoot
                        }
                };

            return await SaveCoreAsync(
                    proposed,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private async Task<SettingsSaveResult>
        SaveCoreAsync(
            AppSettings proposedSettings,
            CancellationToken cancellationToken)
    {
        ValidateForSave(
            proposedSettings);

        AppSettings normalizedSettings =
            AppSettingsNormalizer.Normalize(
                proposedSettings);

        AppSettings previousSettings =
            await LoadNormalizedAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        string? previousGesture =
            _hotkeyService.RegisteredGesture;

        bool previousStartupState =
            await _startupService
                .IsEnabledAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        bool hotkeyChanged = false;
        bool startupChanged = false;
        LibraryMoveTransaction? libraryMove = null;

        if (!string.Equals(
                previousGesture,
                normalizedSettings.Hotkey,
                StringComparison.OrdinalIgnoreCase))
        {
            HotkeyRegistrationResult registrationResult =
                await _hotkeyService
                    .TryRegisterAsync(
                        normalizedSettings.Hotkey,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!registrationResult.Succeeded)
            {
                return SettingsSaveResult.HotkeyRejected(
                    previousSettings,
                    registrationResult);
            }

            hotkeyChanged = true;
        }

        try
        {
            if (previousStartupState !=
                normalizedSettings.Startup.StartWithWindows)
            {
                startupChanged = true;

                await _startupService
                    .SetEnabledAsync(
                        normalizedSettings.Startup.StartWithWindows,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            libraryMove =
                await MoveLibraryIfNeededAsync(
                        previousSettings,
                        normalizedSettings,
                        cancellationToken)
                    .ConfigureAwait(false);

            await _settingsStore
                .SaveAsync(
                    normalizedSettings,
                    cancellationToken)
                .ConfigureAwait(false);

            return SettingsSaveResult.Success(
                normalizedSettings);
        }
        catch (Exception exception)
        {
            List<Exception> rollbackFailures =
                await RollbackAsync(
                        libraryMove,
                        startupChanged,
                        previousStartupState,
                        hotkeyChanged,
                        previousGesture)
                    .ConfigureAwait(false);

            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException(
                    "The settings save failed and one or more rollback operations also failed.",
                    [exception, .. rollbackFailures]);
            }

            ExceptionDispatchInfo
                .Capture(
                    exception)
                .Throw();

            throw;
        }
    }

    private async Task<LibraryMoveTransaction?>
        MoveLibraryIfNeededAsync(
            AppSettings previousSettings,
            AppSettings proposedSettings,
            CancellationToken cancellationToken)
    {
        string sourceRoot =
            CanonicalizeDirectory(
                _paths.GetLibraryRoot(
                    previousSettings.Library.CustomStorageRoot));

        string destinationRoot =
            CanonicalizeDirectory(
                _paths.GetLibraryRoot(
                    proposedSettings.Library.CustomStorageRoot));

        if (_pathComparer.Equals(
                sourceRoot,
                destinationRoot))
        {
            return null;
        }

        _paths.EnsureLibraryDirectoriesExist(
            proposedSettings.Library.CustomStorageRoot);

        LibrarySnapshot previousSnapshot =
            await _libraryStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        string[] localFilePaths =
            previousSnapshot.Favorites
                .Concat(
                    previousSnapshot.Recents)
                .Select(
                    entry => entry.LocalFilePath)
                .Where(
                    filePath =>
                        !string.IsNullOrWhiteSpace(
                            filePath))
                .Select(
                    filePath =>
                        Path.GetFullPath(
                            filePath!))
                .Distinct(
                    _pathComparer)
                .ToArray();

        if (localFilePaths.Length == 0)
        {
            return null;
        }

        LibraryStorageMoveResult moveResult =
            await _storageMover
                .MoveAsync(
                    sourceRoot,
                    destinationRoot,
                    localFilePaths,
                    cancellationToken)
                .ConfigureAwait(false);

        Dictionary<string, string> movedPaths =
            new(
                moveResult.MovedPaths,
                _pathComparer);

        if (movedPaths.Count == 0)
        {
            return null;
        }

        LibrarySnapshot movedSnapshot =
            previousSnapshot with
            {
                Favorites =
                    previousSnapshot.Favorites
                        .Select(
                            entry =>
                                ReplaceLocalPath(
                                    entry,
                                    movedPaths))
                        .ToArray(),

                Recents =
                    previousSnapshot.Recents
                        .Select(
                            entry =>
                                ReplaceLocalPath(
                                    entry,
                                    movedPaths))
                        .ToArray()
            };

        LibraryMoveTransaction transaction =
            new(
                sourceRoot,
                destinationRoot,
                previousSnapshot,
                moveResult);

        try
        {
            await _libraryStore
                .SaveAsync(
                    movedSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);

            transaction.LibrarySnapshotSaved = true;
        }
        catch (Exception exception)
        {
            List<Exception> rollbackFailures = [];

            await RollbackLibraryMoveAsync(
                    transaction,
                    rollbackFailures)
                .ConfigureAwait(false);

            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException(
                    "The library metadata save failed and one or more file rollback operations also failed.",
                    [exception, .. rollbackFailures]);
            }

            ExceptionDispatchInfo
                .Capture(
                    exception)
                .Throw();

            throw;
        }

        return transaction;
    }

    private async Task<List<Exception>>
        RollbackAsync(
            LibraryMoveTransaction? libraryMove,
            bool startupChanged,
            bool previousStartupState,
            bool hotkeyChanged,
            string? previousGesture)
    {
        List<Exception> failures = [];

        if (libraryMove is not null)
        {
            await RollbackLibraryMoveAsync(
                    libraryMove,
                    failures)
                .ConfigureAwait(false);
        }

        if (startupChanged)
        {
            try
            {
                await _startupService
                    .SetEnabledAsync(
                        previousStartupState,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }

        if (hotkeyChanged)
        {
            try
            {
                if (previousGesture is null)
                {
                    await _hotkeyService
                        .UnregisterAsync(
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                else
                {
                    HotkeyRegistrationResult rollbackResult =
                        await _hotkeyService
                            .TryRegisterAsync(
                                previousGesture,
                                CancellationToken.None)
                            .ConfigureAwait(false);

                    if (!rollbackResult.Succeeded)
                    {
                        failures.Add(
                            new InvalidOperationException(
                                rollbackResult.Message ??
                                "The previous hotkey could not be restored."));
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }

        return failures;
    }

    private async Task RollbackLibraryMoveAsync(
        LibraryMoveTransaction transaction,
        List<Exception> failures)
    {
        HashSet<string> sourceFilesNotDeleted =
            transaction.MoveResult
                .SourceFilesNotDeleted
                .Select(
                    Path.GetFullPath)
                .ToHashSet(
                    _pathComparer);

        string[] destinationCopiesToDelete =
            transaction.MoveResult.MovedPaths
                .Where(
                    pair =>
                        sourceFilesNotDeleted.Contains(
                            Path.GetFullPath(
                                pair.Key)))
                .Select(
                    pair => pair.Value)
                .ToArray();

        string[] destinationFilesToMoveBack =
            transaction.MoveResult.MovedPaths
                .Where(
                    pair =>
                        !sourceFilesNotDeleted.Contains(
                            Path.GetFullPath(
                                pair.Key)))
                .Select(
                    pair => pair.Value)
                .ToArray();

        if (destinationFilesToMoveBack.Length > 0)
        {
            try
            {
                await _storageMover
                    .MoveAsync(
                        transaction.DestinationRoot,
                        transaction.SourceRoot,
                        destinationFilesToMoveBack,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }

        if (destinationCopiesToDelete.Length > 0)
        {
            try
            {
                await _storageMover
                    .DeleteAsync(
                        transaction.DestinationRoot,
                        destinationCopiesToDelete,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }

        if (transaction.LibrarySnapshotSaved)
        {
            try
            {
                await _libraryStore
                    .SaveAsync(
                        transaction.PreviousSnapshot,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }
    }

    private async Task<AppSettings> LoadNormalizedAsync(
        CancellationToken cancellationToken)
    {
        AppSettings settings =
            await _settingsStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return AppSettingsNormalizer.Normalize(
            settings);
    }

    private static LibraryEntry ReplaceLocalPath(
        LibraryEntry entry,
        Dictionary<string, string> movedPaths)
    {
        if (string.IsNullOrWhiteSpace(
                entry.LocalFilePath))
        {
            return entry;
        }

        string fullPath =
            Path.GetFullPath(
                entry.LocalFilePath);

        return movedPaths.TryGetValue(
            fullPath,
            out string? movedPath)
                ? entry with
                {
                    LocalFilePath = movedPath
                }
                : entry;
    }

    private static void ValidateForSave(
        AppSettings settings)
    {
        IReadOnlyList<SettingsValidationIssue> issues =
            AppSettingsValidator.Validate(
                settings);

        if (issues.Count == 0)
        {
            return;
        }

        string message =
            string.Join(
                Environment.NewLine,
                issues.Select(
                    issue =>
                        $"{issue.Path}: {issue.Message}"));

        throw new ArgumentException(
            message,
            nameof(settings));
    }

    private static string CanonicalizeDirectory(
        string path)
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                path));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private sealed record LibraryMoveTransaction(
        string SourceRoot,
        string DestinationRoot,
        LibrarySnapshot PreviousSnapshot,
        LibraryStorageMoveResult MoveResult)
    {
        public bool LibrarySnapshotSaved { get; set; }
    }
}
