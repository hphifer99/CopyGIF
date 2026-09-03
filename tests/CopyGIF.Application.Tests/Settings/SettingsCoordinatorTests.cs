using CopyGIF.Application.Settings;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Settings;

[TestClass]
public sealed class SettingsCoordinatorTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(
            2026,
            9,
            3,
            12,
            0,
            0,
            TimeSpan.Zero);

    [TestMethod]
    public async Task LoadAsync_NormalizesStoredSettings()
    {
        Harness harness =
            new(
                new AppSettings
                {
                    Hotkey = "  ",
                    Search =
                        new SearchSettings
                        {
                            ResultsPerSearch = 0
                        }
                });

        AppSettings result =
            await harness.Coordinator.LoadAsync();

        Assert.AreEqual(
            AppSettings.DefaultHotkey,
            result.Hotkey);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);
    }

    [TestMethod]
    public async Task SaveAsync_ValidSettings_AppliesAndPersistsChanges()
    {
        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+G",
                startWithWindows: true);

        Harness harness =
            new(
                current);

        await harness.HotkeyService
            .TryRegisterAsync(
                current.Hotkey);

        harness.StartupService.IsEnabled = true;

        AppSettings proposed =
            current with
            {
                Hotkey = "Ctrl+H",
                Startup =
                    new StartupSettings
                    {
                        StartWithWindows = false
                    }
            };

        SettingsSaveResult result =
            await harness.Coordinator.SaveAsync(
                proposed);

        Assert.IsTrue(
            result.Succeeded);

        Assert.AreEqual(
            "Ctrl+H",
            result.EffectiveSettings.Hotkey);

        Assert.AreEqual(
            "Ctrl+H",
            harness.HotkeyService.RegisteredGesture);

        CollectionAssert.AreEqual(
            new[]
            {
                false
            },
            harness.StartupService.RequestedStates.ToArray());

        Assert.HasCount(
            1,
            harness.SettingsStore.SavedSettings);

        Assert.AreEqual(
            proposed,
            harness.SettingsStore.Value);
    }

    [TestMethod]
    public async Task SaveAsync_InvalidSettings_RejectsBeforeSideEffects()
    {
        Harness harness =
            new(
                new AppSettings());

        AppSettings invalid =
            new AppSettings
            {
                Library =
                    new LibrarySettings
                    {
                        RecentLimit = 0
                    }
            };

        ArgumentException exception =
            await Assert.ThrowsExactlyAsync<
                ArgumentException>(
                () => harness.Coordinator.SaveAsync(
                    invalid));

        StringAssert.Contains(
            exception.Message,
            "Library.RecentLimit");

        Assert.AreEqual(
            0,
            harness.SettingsStore.LoadCallCount);

        Assert.HasCount(
            0,
            harness.SettingsStore.SavedSettings);

        Assert.AreEqual(
            0,
            harness.StartupService.IsEnabledCallCount);

        Assert.HasCount(
            0,
            harness.HotkeyService.RegistrationAttempts);
    }

    [TestMethod]
    public async Task SaveAsync_HotkeyConflict_PreservesPreviousSettingsAndRuntimeState()
    {
        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+G");

        Harness harness =
            new(
                current);

        await harness.HotkeyService
            .TryRegisterAsync(
                current.Hotkey);

        harness.HotkeyService.RegistrationHandler =
            static (gesture, _) =>
                Task.FromResult(
                    gesture == "Ctrl+H"
                        ? HotkeyRegistrationResult.Failed(
                            HotkeyRegistrationFailure.Conflict,
                            "That hotkey is already in use.")
                        : HotkeyRegistrationResult.Success());

        SettingsSaveResult result =
            await harness.Coordinator.SaveAsync(
                current with
                {
                    Hotkey = "Ctrl+H"
                });

        Assert.IsFalse(
            result.Succeeded);

        Assert.AreEqual(
            HotkeyRegistrationFailure.Conflict,
            result.HotkeyFailure);

        Assert.AreEqual(
            current,
            result.EffectiveSettings);

        Assert.AreEqual(
            "Ctrl+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.HasCount(
            0,
            harness.SettingsStore.SavedSettings);

        Assert.HasCount(
            0,
            harness.StartupService.RequestedStates);
    }

    [TestMethod]
    public async Task SaveAsync_StartupChangeFails_RestoresPreviousHotkey()
    {
        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+G",
                startWithWindows: true);

        Harness harness =
            new(
                current);

        await harness.HotkeyService
            .TryRegisterAsync(
                current.Hotkey);

        harness.StartupService.IsEnabled = true;
        harness.StartupService.SetEnabledHandler =
            static (enabled, _) =>
                enabled
                    ? Task.CompletedTask
                    : throw new IOException(
                        "Startup registration failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Hotkey = "Ctrl+H",
                    Startup =
                        new StartupSettings
                        {
                            StartWithWindows = false
                        }
                }));

        Assert.AreEqual(
            "Ctrl+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.HasCount(
            0,
            harness.SettingsStore.SavedSettings);
    }

    [TestMethod]
    public async Task SaveAsync_SettingsWriteFails_RollsBackRuntimeChanges()
    {
        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+G",
                startWithWindows: true);

        Harness harness =
            new(
                current);

        await harness.HotkeyService
            .TryRegisterAsync(
                current.Hotkey);

        harness.StartupService.IsEnabled = true;
        harness.SettingsStore.SaveHandler =
            static (_, _) =>
                throw new IOException(
                    "Settings write failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Hotkey = "Ctrl+H",
                    Startup =
                        new StartupSettings
                        {
                            StartWithWindows = false
                        }
                }));

        Assert.AreEqual(
            "Ctrl+G",
            harness.HotkeyService.RegisteredGesture);

        CollectionAssert.AreEqual(
            new[]
            {
                false,
                true
            },
            harness.StartupService.RequestedStates.ToArray());

        Assert.AreEqual(
            current,
            harness.SettingsStore.Value);
    }

    [TestMethod]
    public async Task SaveAsync_CancelledDuringWrite_RollsBackWithoutCancelledToken()
    {
        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+G",
                startWithWindows: true);

        Harness harness =
            new(
                current);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        using CancellationTokenSource cancellation =
            new();

        harness.SettingsStore.SaveHandler =
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();

                return Task.CompletedTask;
            };

        await Assert.ThrowsExactlyAsync<
            OperationCanceledException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Hotkey = "Ctrl+H",
                    Startup =
                        new StartupSettings
                        {
                            StartWithWindows = false
                        }
                },
                cancellation.Token));

        Assert.AreEqual(
            "Ctrl+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.IsTrue(
            harness.StartupService.IsEnabled);

        CollectionAssert.AreEqual(
            new[]
            {
                false,
                true
            },
            harness.StartupService.RequestedStates.ToArray());
    }

    [TestMethod]
    public async Task SaveAsync_ConcurrentCalls_AreSerialized()
    {
        AppSettings current =
            CreateSettings();

        Harness harness =
            new(
                current);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        TaskCompletionSource firstSaveEntered =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        TaskCompletionSource releaseFirstSave =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int saveCallCount = 0;

        harness.SettingsStore.SaveHandler =
            async (_, _) =>
            {
                saveCallCount++;

                if (saveCallCount == 1)
                {
                    firstSaveEntered.SetResult();

                    await releaseFirstSave.Task;
                }
            };

        AppSettings firstSettings =
            current with
            {
                Search =
                    current.Search with
                    {
                        ResultsPerSearch = 12
                    }
            };

        AppSettings secondSettings =
            current with
            {
                Search =
                    current.Search with
                    {
                        ResultsPerSearch = 18
                    }
            };

        Task<SettingsSaveResult> firstSave =
            harness.Coordinator.SaveAsync(
                firstSettings);

        await firstSaveEntered.Task;

        Task<SettingsSaveResult> secondSave =
            harness.Coordinator.SaveAsync(
                secondSettings);

        Assert.AreEqual(
            1,
            harness.SettingsStore.LoadCallCount);

        releaseFirstSave.SetResult();

        await Task.WhenAll(
            firstSave,
            secondSave);

        Assert.AreEqual(
            2,
            saveCallCount);

        Assert.AreEqual(
            18,
            harness.SettingsStore.Value
                .Search
                .ResultsPerSearch);
    }

    [TestMethod]
    public async Task SaveAsync_StorageRootChanges_MovesFilesAndUpdatesMetadata()
    {
        FakeApplicationPaths paths =
            new();

        string favoritePath =
            Path.Combine(
                paths.GetFavoritesDirectory(
                    customStorageRoot: null),
                "favorite.gif");

        string recentPath =
            Path.Combine(
                paths.GetRecentsDirectory(
                    customStorageRoot: null),
                "recent.gif");

        LibrarySnapshot library =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        "favorite",
                        favoritePath)
                ],
                Recents =
                [
                    CreateEntry(
                        "recent",
                        recentPath)
                ]
            };

        AppSettings current =
            CreateSettings();

        Harness harness =
            new(
                current,
                library,
                paths);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        string selectedRoot =
            Path.Combine(
                paths.RootDirectory,
                "CustomLibrary");

        SettingsSaveResult result =
            await harness.Coordinator.SaveAsync(
                current with
                {
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                selectedRoot
                        }
                });

        Assert.IsTrue(
            result.Succeeded);

        Assert.HasCount(
            1,
            harness.StorageMover.MoveRequests);

        Assert.HasCount(
            1,
            harness.LibraryStore.SavedSnapshots);

        string destinationRoot =
            paths.GetLibraryRoot(
                selectedRoot);

        Assert.AreEqual(
            Path.Combine(
                destinationRoot,
                Path.GetRelativePath(
                    paths.RootDirectory,
                    favoritePath)),
            harness.LibraryStore.Value
                .Favorites[0]
                .LocalFilePath);

        Assert.AreEqual(
            Path.Combine(
                destinationRoot,
                Path.GetRelativePath(
                    paths.RootDirectory,
                    recentPath)),
            harness.LibraryStore.Value
                .Recents[0]
                .LocalFilePath);

        CollectionAssert.AreEqual(
            new string?[]
            {
                selectedRoot
            },
            paths.EnsuredLibraryRoots.ToArray());
    }

    [TestMethod]
    public async Task SaveAsync_StorageMoveFails_RollsBackRuntimeChanges()
    {
        FakeApplicationPaths paths =
            new();

        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+G",
                startWithWindows: true);

        LibrarySnapshot library =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        "favorite",
                        Path.Combine(
                            paths.FavoritesDirectory,
                            "favorite.gif"))
                ]
            };

        Harness harness =
            new(
                current,
                library,
                paths);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        harness.StorageMover.MoveHandler =
            static (_, _, _, _) =>
                throw new IOException(
                    "Move failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Hotkey = "Ctrl+H",
                    Startup =
                        new StartupSettings
                        {
                            StartWithWindows = false
                        },
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                Path.Combine(
                                    paths.RootDirectory,
                                    "CustomLibrary")
                        }
                }));

        Assert.AreEqual(
            "Ctrl+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.IsTrue(
            harness.StartupService.IsEnabled);

        Assert.AreEqual(
            library,
            harness.LibraryStore.Value);

        Assert.AreEqual(
            current,
            harness.SettingsStore.Value);
    }

    [TestMethod]
    public async Task SaveAsync_LibraryMetadataWriteFails_MovesFilesBack()
    {
        FakeApplicationPaths paths =
            new();

        AppSettings current =
            CreateSettings();

        LibrarySnapshot library =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        "favorite",
                        Path.Combine(
                            paths.FavoritesDirectory,
                            "favorite.gif"))
                ]
            };

        Harness harness =
            new(
                current,
                library,
                paths);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        harness.LibraryStore.SaveHandler =
            static (_, _) =>
                throw new IOException(
                    "Library metadata write failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                Path.Combine(
                                    paths.RootDirectory,
                                    "CustomLibrary")
                        }
                }));

        Assert.HasCount(
            2,
            harness.StorageMover.MoveRequests);

        Assert.AreEqual(
            current,
            harness.SettingsStore.Value);

        Assert.AreEqual(
            library,
            harness.LibraryStore.Value);
    }

    [TestMethod]
    public async Task SaveAsync_SettingsWriteFails_RestoresMovedLibrary()
    {
        FakeApplicationPaths paths =
            new();

        AppSettings current =
            CreateSettings();

        LibrarySnapshot library =
            new()
            {
                Recents =
                [
                    CreateEntry(
                        "recent",
                        Path.Combine(
                            paths.RecentsDirectory,
                            "recent.gif"))
                ]
            };

        Harness harness =
            new(
                current,
                library,
                paths);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        harness.SettingsStore.SaveHandler =
            static (_, _) =>
                throw new IOException(
                    "Settings write failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                Path.Combine(
                                    paths.RootDirectory,
                                    "CustomLibrary")
                        }
                }));

        Assert.HasCount(
            2,
            harness.StorageMover.MoveRequests);

        Assert.HasCount(
            2,
            harness.LibraryStore.SavedSnapshots);

        Assert.AreEqual(
            library,
            harness.LibraryStore.Value);

        Assert.AreEqual(
            current,
            harness.SettingsStore.Value);
    }

    [TestMethod]
    public async Task SaveAsync_RollbackWhenSourceRemained_DeletesDestinationCopy()
    {
        FakeApplicationPaths paths =
            new();

        AppSettings current =
            CreateSettings();

        string sourcePath =
            Path.Combine(
                paths.FavoritesDirectory,
                "favorite.gif");

        LibrarySnapshot library =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        "favorite",
                        sourcePath)
                ]
            };

        Harness harness =
            new(
                current,
                library,
                paths);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        string selectedRoot =
            Path.Combine(
                paths.RootDirectory,
                "CustomLibrary");

        string destinationPath =
            Path.Combine(
                paths.GetLibraryRoot(
                    selectedRoot),
                Path.GetRelativePath(
                    paths.RootDirectory,
                    sourcePath));

        harness.StorageMover.MoveHandler =
            (_, _, _, _) =>
                Task.FromResult(
                    new LibraryStorageMoveResult
                    {
                        MovedPaths =
                            new Dictionary<string, string>
                            {
                                [sourcePath] =
                                    destinationPath
                            },
                        SourceFilesNotDeleted =
                        [
                            sourcePath
                        ]
                    });

        harness.SettingsStore.SaveHandler =
            static (_, _) =>
                throw new IOException(
                    "Settings write failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.SaveAsync(
                current with
                {
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                selectedRoot
                        }
                }));

        Assert.HasCount(
            1,
            harness.StorageMover.MoveRequests);

        Assert.HasCount(
            1,
            harness.StorageMover.DeleteRequests);

        Assert.AreEqual(
            destinationPath,
            harness.StorageMover
                .DeleteRequests[0]
                .FilePaths
                .Single());
    }

    [TestMethod]
    public async Task RestoreDefaultsAsync_ResetsSettingsWithoutDeletingLibraryData()
    {
        FakeApplicationPaths paths =
            new();

        AppSettings current =
            CreateSettings(
                hotkey: "Ctrl+Shift+G",
                startWithWindows: false,
                customStorageRoot:
                    Path.Combine(
                        paths.RootDirectory,
                        "CustomLibrary"));

        LibrarySnapshot library =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        "remote-only",
                        localFilePath: null)
                ]
            };

        Harness harness =
            new(
                current,
                library,
                paths);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        SettingsSaveResult result =
            await harness.Coordinator
                .RestoreDefaultsAsync();

        Assert.IsTrue(
            result.Succeeded);

        Assert.AreEqual(
            new AppSettings(),
            result.EffectiveSettings);

        Assert.AreEqual(
            library,
            harness.LibraryStore.Value);

        Assert.HasCount(
            0,
            harness.StorageMover.DeleteRequests);

        Assert.HasCount(
            0,
            harness.LibraryStore.SavedSnapshots);
    }

    [TestMethod]
    public async Task ChooseLibraryStorageRootAsync_CancelledPicker_DoesNotSave()
    {
        AppSettings current =
            CreateSettings(
                customStorageRoot:
                    Path.Combine(
                        Path.GetTempPath(),
                        "ExistingLibrary"));

        Harness harness =
            new(
                current);

        harness.FolderPickerService.SelectedFolder = null;

        SettingsSaveResult? result =
            await harness.Coordinator
                .ChooseLibraryStorageRootAsync();

        Assert.IsNull(
            result);

        Assert.AreEqual(
            current.Library.CustomStorageRoot,
            harness.FolderPickerService
                .InitialDirectories
                .Single());

        Assert.HasCount(
            0,
            harness.SettingsStore.SavedSettings);

        Assert.AreEqual(
            0,
            harness.StartupService.IsEnabledCallCount);
    }

    [TestMethod]
    public async Task ChooseLibraryStorageRootAsync_SelectedFolder_SavesNewRoot()
    {
        AppSettings current =
            CreateSettings();

        Harness harness =
            new(
                current);

        await RegisterCurrentRuntimeStateAsync(
            harness,
            current);

        string selectedRoot =
            Path.Combine(
                harness.Paths.RootDirectory,
                "SelectedLibrary");

        harness.FolderPickerService.SelectedFolder =
            selectedRoot;

        SettingsSaveResult? result =
            await harness.Coordinator
                .ChooseLibraryStorageRootAsync();

        Assert.IsNotNull(
            result);

        Assert.IsTrue(
            result.Succeeded);

        Assert.AreEqual(
            selectedRoot,
            result.EffectiveSettings
                .Library
                .CustomStorageRoot);

        Assert.AreEqual(
            selectedRoot,
            harness.SettingsStore.Value
                .Library
                .CustomStorageRoot);

        Assert.AreEqual(
            2,
            harness.SettingsStore.LoadCallCount);
    }

    private static async Task
        RegisterCurrentRuntimeStateAsync(
            Harness harness,
            AppSettings settings)
    {
        await harness.HotkeyService
            .TryRegisterAsync(
                settings.Hotkey);

        harness.StartupService.IsEnabled =
            settings.Startup.StartWithWindows;
    }

    private static AppSettings CreateSettings(
        string hotkey = AppSettings.DefaultHotkey,
        bool startWithWindows = true,
        string? customStorageRoot = null)
    {
        return new AppSettings
        {
            Hotkey = hotkey,
            Startup =
                new StartupSettings
                {
                    StartWithWindows =
                        startWithWindows
                },
            Library =
                new LibrarySettings
                {
                    CustomStorageRoot =
                        customStorageRoot
                }
        };
    }

    private static LibraryEntry CreateEntry(
        string id,
        string? localFilePath)
    {
        return new LibraryEntry
        {
            Identity =
                new GifIdentity(
                    "klipy",
                    id),
            Title = $"GIF {id}",
            GifUri =
                new Uri(
                    $"https://static.klipy.com/{id}.gif"),
            ThumbnailUri =
                new Uri(
                    $"https://static.klipy.com/{id}-thumb.gif"),
            LocalFilePath = localFilePath,
            AddedAtUtc = ReferenceTime
        };
    }

    private sealed class Harness
    {
        public Harness(
            AppSettings settings,
            LibrarySnapshot? library = null,
            FakeApplicationPaths? paths = null)
        {
            SettingsStore.Value = settings;
            LibraryStore.Value =
                library ?? new LibrarySnapshot();

            Paths =
                paths ?? new FakeApplicationPaths();

            Coordinator =
                new SettingsCoordinator(
                    SettingsStore,
                    LibraryStore,
                    StorageMover,
                    Paths,
                    HotkeyService,
                    StartupService,
                    FolderPickerService);
        }

        public FakeSettingsStore SettingsStore { get; } =
            new();

        public FakeLibraryStore LibraryStore { get; } =
            new();

        public FakeLibraryStorageMover StorageMover { get; } =
            new();

        public FakeHotkeyService HotkeyService { get; } =
            new();

        public FakeStartupService StartupService { get; } =
            new();

        public FakeFolderPickerService FolderPickerService { get; } =
            new();

        public FakeApplicationPaths Paths { get; }

        public SettingsCoordinator Coordinator { get; }
    }
}
