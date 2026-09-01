using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Migration;

public sealed class V1MigrationCoordinator :
    IMigrationCoordinator
{
    private const string V1SettingsBackupFileName =
        "settings.v1.json";

    private const string V1LibraryBackupFileName =
        "library.v1.json";

    private const int MaximumCredentialLength =
        16 * 1024;

    private readonly IApplicationPaths _paths;
    private readonly V1SettingsReader _settingsReader;
    private readonly V1LibraryReader _libraryReader;
    private readonly ISettingsStore _settingsStore;
    private readonly ILibraryStore _libraryStore;
    private readonly ISecretStore _secretStore;
    private readonly ILegacyCredentialDecoder
        _credentialDecoder;
    private readonly IMigrationStateStore
        _migrationStateStore;

    public V1MigrationCoordinator(
        IApplicationPaths paths,
        V1SettingsReader settingsReader,
        V1LibraryReader libraryReader,
        ISettingsStore settingsStore,
        ILibraryStore libraryStore,
        ISecretStore secretStore,
        ILegacyCredentialDecoder credentialDecoder,
        IMigrationStateStore migrationStateStore)
    {
        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _settingsReader =
            settingsReader ??
            throw new ArgumentNullException(
                nameof(settingsReader));

        _libraryReader =
            libraryReader ??
            throw new ArgumentNullException(
                nameof(libraryReader));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _libraryStore =
            libraryStore ??
            throw new ArgumentNullException(
                nameof(libraryStore));

        _secretStore =
            secretStore ??
            throw new ArgumentNullException(
                nameof(secretStore));

        _credentialDecoder =
            credentialDecoder ??
            throw new ArgumentNullException(
                nameof(credentialDecoder));

        _migrationStateStore =
            migrationStateStore ??
            throw new ArgumentNullException(
                nameof(migrationStateStore));
    }

    public async Task<MigrationResult>
        MigrateIfNeededAsync(
            CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        MigrationState originalState =
            await _migrationStateStore.LoadAsync(
                cancellationToken);

        if (originalState.IsCompleted)
        {
            return CreateNotRequiredResult(
                "Legacy data migration was already completed.");
        }

        string settingsBackupPath =
            Path.Combine(
                _paths.MigrationDirectory,
                V1SettingsBackupFileName);

        string libraryBackupPath =
            Path.Combine(
                _paths.MigrationDirectory,
                V1LibraryBackupFileName);

        bool settingsMutationAttempted = false;
        bool libraryMutationAttempted = false;
        bool credentialMutationAttempted = false;
        bool stateMutationAttempted = false;
        string? previousCredential = null;

        try
        {
            RestoreInterruptedSource(
                settingsBackupPath,
                _paths.SettingsPath);

            RestoreInterruptedSource(
                libraryBackupPath,
                _paths.LibraryPath);

            V1SettingsSnapshot? settingsSnapshot =
                await _settingsReader.ReadAsync(
                    _paths.SettingsPath,
                    cancellationToken);

            V1LibrarySnapshot? librarySnapshot =
                await _libraryReader.ReadAsync(
                    _paths.LibraryPath,
                    cancellationToken);

            if (settingsSnapshot is null &&
                librarySnapshot is null)
            {
                stateMutationAttempted = true;

                await _migrationStateStore.SaveAsync(
                    CreateCompletedState(
                        sourceVersion: null),
                    cancellationToken);

                return CreateNotRequiredResult(
                    "No legacy CopyGIF data was found.");
            }

            List<string> warnings = [];

            if (settingsSnapshot is not null)
            {
                warnings.AddRange(
                    settingsSnapshot.Warnings);
            }

            if (librarySnapshot is not null)
            {
                warnings.AddRange(
                    librarySnapshot.Warnings);
            }

            string? migratedCredential =
                ResolveCredential(
                    settingsSnapshot?.Credential,
                    warnings);

            if (migratedCredential is not null)
            {
                previousCredential =
                    await _secretStore.GetAsync(
                        SecretNames.KlipyApiKey,
                        cancellationToken);
            }

            if (settingsSnapshot is not null)
            {
                ArchiveLegacySource(
                    _paths.SettingsPath,
                    settingsBackupPath);

                settingsMutationAttempted = true;

                await _settingsStore.SaveAsync(
                    settingsSnapshot.Settings,
                    cancellationToken);
            }

            if (librarySnapshot is not null)
            {
                ArchiveLegacySource(
                    _paths.LibraryPath,
                    libraryBackupPath);

                libraryMutationAttempted = true;

                await _libraryStore.SaveAsync(
                    librarySnapshot.Library,
                    cancellationToken);
            }

            if (migratedCredential is not null)
            {
                credentialMutationAttempted = true;

                await _secretStore.SetAsync(
                    SecretNames.KlipyApiKey,
                    migratedCredential,
                    cancellationToken);
            }

            stateMutationAttempted = true;

            await _migrationStateStore.SaveAsync(
                CreateCompletedState(
                    sourceVersion: "1"),
                cancellationToken);

            return new MigrationResult
            {
                Status = MigrationStatus.Completed,
                MigratedFavorites =
                    librarySnapshot?
                        .Library.Favorites.Count ??
                    0,
                MigratedRecents =
                    librarySnapshot?
                        .Library.Recents.Count ??
                    0,
                MigratedSettings =
                    settingsSnapshot is not null,
                MigratedCredential =
                    migratedCredential is not null,
                Warnings = warnings,
                Message =
                    "Legacy CopyGIF data was migrated successfully."
            };
        }
        catch (OperationCanceledException)
        {
            await TryRollbackAsync(
                settingsBackupPath,
                libraryBackupPath,
                settingsMutationAttempted,
                libraryMutationAttempted,
                credentialMutationAttempted,
                stateMutationAttempted,
                previousCredential,
                originalState);

            throw;
        }
        catch (Exception)
        {
            bool mutationAttempted =
                settingsMutationAttempted ||
                libraryMutationAttempted ||
                credentialMutationAttempted ||
                stateMutationAttempted;

            if (!mutationAttempted)
            {
                return new MigrationResult
                {
                    Status = MigrationStatus.Failed,
                    Message =
                        "Legacy CopyGIF data could not be prepared for migration. No destination data was changed."
                };
            }

            bool rolledBack =
                await TryRollbackAsync(
                    settingsBackupPath,
                    libraryBackupPath,
                    settingsMutationAttempted,
                    libraryMutationAttempted,
                    credentialMutationAttempted,
                    stateMutationAttempted,
                    previousCredential,
                    originalState);

            return new MigrationResult
            {
                Status = rolledBack
                    ? MigrationStatus.RolledBack
                    : MigrationStatus.Failed,
                Message = rolledBack
                    ? "Legacy CopyGIF migration failed and the original data was restored."
                    : "Legacy CopyGIF migration failed and automatic rollback could not be completed."
            };
        }
    }

    private string? ResolveCredential(
        V1CredentialPayload? payload,
        List<string> warnings)
    {
        if (payload is null)
        {
            return null;
        }

        string value =
            payload.Kind switch
            {
                V1CredentialKind.Plaintext =>
                    payload.Value,

                V1CredentialKind.DpapiCurrentUser =>
                    _credentialDecoder
                        .DecodeCurrentUserCredential(
                            payload.Value),

                _ =>
                    throw new InvalidDataException(
                        "The legacy credential format is not supported.")
            };

        string normalized =
            value.Trim();

        if (normalized.Length == 0)
        {
            warnings.Add(
                "The legacy API credential was empty and was not migrated.");

            return null;
        }

        if (normalized.Length >
            MaximumCredentialLength)
        {
            throw new InvalidDataException(
                "The legacy API credential exceeds its maximum allowed size.");
        }

        return normalized;
    }

    private static void ArchiveLegacySource(
        string sourcePath,
        string backupPath)
    {
        EnsureRegularFile(
            sourcePath,
            "legacy source");

        if (!File.Exists(backupPath))
        {
            File.Copy(
                sourcePath,
                backupPath,
                overwrite: false);
        }

        File.Delete(sourcePath);
    }

    private static void RestoreInterruptedSource(
        string backupPath,
        string destinationPath)
    {
        if (!File.Exists(backupPath))
        {
            return;
        }

        EnsureRegularFile(
            backupPath,
            "legacy migration backup");

        File.Copy(
            backupPath,
            destinationPath,
            overwrite: true);
    }

    private async Task<bool> TryRollbackAsync(
        string settingsBackupPath,
        string libraryBackupPath,
        bool settingsMutationAttempted,
        bool libraryMutationAttempted,
        bool credentialMutationAttempted,
        bool stateMutationAttempted,
        string? previousCredential,
        MigrationState originalState)
    {
        try
        {
            if (settingsMutationAttempted)
            {
                RestoreInterruptedSource(
                    settingsBackupPath,
                    _paths.SettingsPath);

                File.Delete(
                    settingsBackupPath);
            }

            if (libraryMutationAttempted)
            {
                RestoreInterruptedSource(
                    libraryBackupPath,
                    _paths.LibraryPath);

                File.Delete(
                    libraryBackupPath);
            }

            if (credentialMutationAttempted)
            {
                if (previousCredential is null)
                {
                    await _secretStore.DeleteAsync(
                        SecretNames.KlipyApiKey,
                        CancellationToken.None);
                }
                else
                {
                    await _secretStore.SetAsync(
                        SecretNames.KlipyApiKey,
                        previousCredential,
                        CancellationToken.None);
                }
            }

            if (stateMutationAttempted)
            {
                await _migrationStateStore.SaveAsync(
                    originalState,
                    CancellationToken.None);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void EnsureRegularFile(
        string path,
        string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The {description} file was not found.",
                path);
        }

        FileAttributes attributes =
            File.GetAttributes(path);

        if ((attributes &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"The {description} cannot be a reparse-point file.");
        }
    }

    private static MigrationState CreateCompletedState(
        string? sourceVersion)
    {
        return new MigrationState
        {
            IsCompleted = true,
            CompletedAtUtc =
                DateTimeOffset.UtcNow,
            SourceVersion = sourceVersion
        };
    }

    private static MigrationResult
        CreateNotRequiredResult(
            string message)
    {
        return new MigrationResult
        {
            Status = MigrationStatus.NotRequired,
            Message = message
        };
    }
}
