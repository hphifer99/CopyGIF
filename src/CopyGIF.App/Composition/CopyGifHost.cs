using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application;
using CopyGIF.Application.Media;
using CopyGIF.Application.Settings;
using CopyGIF.Application.Startup;
using CopyGIF.App.Services;
using CopyGIF.App.Views;
using CopyGIF.App.Views.Pages;
using CopyGIF.Core.Models;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Platform.Windows;
using CopyGIF.Platform.Windows.Storage;
using CopyGIF.Presentation;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Main;
using CopyGIF.Presentation.Onboarding;
using CopyGIF.Presentation.Settings;
using CopyGIF.Presentation.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace CopyGIF.App.Composition;

public sealed class CopyGifHost :
    IAsyncDisposable
{
    public const string GifCardTemplateKey =
        "CopyGifGifCardTemplate";

    public const string OnboardingTemplateKey =
        "CopyGifOnboardingTemplate";

    public const string GeneralSettingsTemplateKey =
        "CopyGifGeneralSettingsTemplate";

    public const string SearchSettingsTemplateKey =
        "CopyGifSearchSettingsTemplate";

    public const string AppearanceSettingsTemplateKey =
        "CopyGifAppearanceSettingsTemplate";

    public const string LibrarySettingsTemplateKey =
        "CopyGifLibrarySettingsTemplate";

    public const string ApiSettingsTemplateKey =
        "CopyGifApiSettingsTemplate";

    public const string UpdatesSettingsTemplateKey =
        "CopyGifUpdatesSettingsTemplate";

    private readonly ServiceProvider
        _serviceProvider;

    private readonly WinUiDispatcher
        _dispatcher;

    private readonly IApplicationStartupCoordinator
        _startupCoordinator;

    private readonly WindowManager
        _windowManager;

    private readonly ShellRuntimeState
        _runtimeState;

    private int _startState;

    private int _disposeState;

    private CopyGifHost(
        ServiceProvider serviceProvider)
    {
        _serviceProvider =
            serviceProvider ??
            throw new ArgumentNullException(
                nameof(serviceProvider));

        _dispatcher =
            serviceProvider
                .GetRequiredService<
                    WinUiDispatcher>();

        _startupCoordinator =
            serviceProvider
                .GetRequiredService<
                    IApplicationStartupCoordinator>();

        _windowManager =
            serviceProvider
                .GetRequiredService<
                    WindowManager>();

        _runtimeState =
            serviceProvider
                .GetRequiredService<
                    ShellRuntimeState>();
    }

    public IServiceProvider Services =>
        _serviceProvider;

    public WindowManager WindowManager =>
        _windowManager;

    public static CopyGifHost Create()
    {
        ServiceCollection services =
            new();

        ConfigureServices(
            services);

        ServiceProvider serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        // The provider rules (direct media hosts, library permission, display names) follow the
        // registered provider descriptors, so a provider added later needs no change elsewhere.
        ProviderMediaPolicy.Configure(
            serviceProvider.GetServices<ProviderDescriptor>());

        try
        {
            IApplicationPaths paths =
                serviceProvider
                    .GetRequiredService<
                        IApplicationPaths>();

            RepairLog.Configure(paths.LogsDirectory);

            // The first line of every run records where this build actually reads and
            // writes, so the folders can be found without guessing at the install shape.
            RepairDiagnostics.RecordContext(
                "runtime-context",
                $"{WindowsStagingPaths.Describe()} root={paths.RootDirectory} logs={paths.LogsDirectory}");

            return new CopyGifHost(
                serviceProvider);
        }
        catch
        {
            try
            {
                serviceProvider
                    .DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception disposalException)
            {
                Debug.WriteLine(
                    $"CopyGIF could not completely dispose its failed startup container: {disposalException}");
            }

            throw;
        }
    }

    public static void ConfigureServices(
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        services
            .AddCopyGifInfrastructure();

        services
            .AddCopyGifWindowsPlatform();

        services
            .AddCopyGifApplication();

        services
            .AddCopyGifPresentation();

        // Clipboard files have to live where another application can open them, which
        // the default library root under AppData does not guarantee on a packaged build.
        services.RemoveAll<IApplicationPaths>();
        services.AddSingleton<IApplicationPaths>(
            _ =>
                new ApplicationPaths(
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder
                                .LocalApplicationData),
                        StoragePolicy.LibraryRootDirectoryName),
                    WindowsStagingPaths.ClipboardStagingDirectory));

        services.RemoveAll<IClipboardService>();
        services.AddSingleton<IClipboardService, WinUiClipboardService>();
        ReplaceCopyCoordinator(services);
        services.AddSingleton<IApplicationVersion>(
            new EntryAssemblyVersion());
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<AppUpdateScheduler>();

        services.AddSingleton<
            WinUiDispatcher>();

        services.AddSingleton<
            ThemeManager>();

        services.AddSingleton<
            ShellRuntimeState>();

        services.AddSingleton<
            MainViewModel>();

        services.AddTransient<
            MainWindow>(
            CreateMainWindow);

        services.AddTransient<
            SettingsWindow>(
            CreateSettingsWindow);

        services.AddTransient<
            OnboardingWindow>(
            CreateOnboardingWindow);

        services.AddSingleton<
            WindowManager>(
            CreateWindowManager);
    }

    public async Task<ApplicationStartupResult>
        StartAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            arguments);

        if (Interlocked.CompareExchange(
                ref _startState,
                1,
                0) != 0)
        {
            throw new InvalidOperationException(
                "CopyGIF startup has already been requested.");
        }

        try
        {
            DispatcherQueue dispatcherQueue =
                DispatcherQueue
                    .GetForCurrentThread() ??
                throw new InvalidOperationException(
                    "CopyGIF startup must run on a thread with a WinUI dispatcher queue.");

            _dispatcher.Initialize(
                dispatcherQueue);

            ApplicationStartupResult result =
                await _startupCoordinator
                    .InitializeAsync(
                        arguments,
                        cancellationToken)
                    .ConfigureAwait(true);

            if (!result.IsReady)
            {
                Interlocked.Exchange(
                    ref _startState,
                    2);

                return result;
            }

            AppSettings settings =
                result.Settings ??
                throw new InvalidDataException(
                    "Ready startup did not provide application settings.");

            CopyGIF.Application.Onboarding.OnboardingState
                onboarding =
                    result.Onboarding ??
                    throw new InvalidDataException(
                        "Ready startup did not provide onboarding state.");

            bool startHidden =
                arguments.Any(
                    argument =>
                        string.Equals(
                            argument,
                            "--startup",
                            StringComparison.OrdinalIgnoreCase));

            _runtimeState.ApplySettings(
                settings);

            await _windowManager
                .InitializeAsync(
                    settings,
                    onboarding,
                    startHidden,
                    cancellationToken)
                .ConfigureAwait(true);

            _serviceProvider.GetRequiredService<AppUpdateScheduler>().Start(GetCurrentVersion());

            if (result.HotkeyFailure != HotkeyRegistrationFailure.None &&
                !onboarding.IsRequired &&
                !startHidden)
            {
                await _windowManager
                    .ShowSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(true);

                if (_windowManager.SettingsWindow is
                    SettingsWindow settingsWindow)
                {
                    settingsWindow.StatusMessage =
                        "The hotkey is unavailable. Choose another hotkey in General settings.";

                    settingsWindow.StatusSeverity =
                        InfoBarSeverity.Warning;
                }
            }

            Interlocked.Exchange(
                ref _startState,
                2);

            return result;
        }
        catch
        {
            Interlocked.Exchange(
                ref _startState,
                0);

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeState,
                1) != 0)
        {
            return;
        }

        await _serviceProvider
            .DisposeAsync()
            .ConfigureAwait(false);

        Interlocked.Exchange(
            ref _disposeState,
            2);
    }

    private static void ReplaceCopyCoordinator(
        IServiceCollection services)
    {
        services.RemoveAll<
            IGifCopyCoordinator>();

        services.AddTransient<
            GifCopyCoordinator>();

        services.AddTransient<
            IGifCopyCoordinator,
            WindowAwareGifCopyCoordinator>();
    }

    private static MainWindow CreateMainWindow(
        IServiceProvider services)
    {
        MainViewModel viewModel =
            services.GetRequiredService<
                MainViewModel>();

        ShellRuntimeState runtimeState =
            services.GetRequiredService<
                ShellRuntimeState>();

        MainWindow window =
            new();

        window.RootElement.DataContext =
            viewModel;

        BindSearchPage(
            window.SearchPage,
            viewModel.Search);

        BindFavoritesPage(
            window.FavoritesPage,
            viewModel.Favorites);

        BindRecentsPage(
            window.RecentsPage,
            viewModel.Recents);

        DataTemplate gifCardTemplate =
            GetRequiredTemplate(
                GifCardTemplateKey);

        window.SearchPage.ItemTemplate =
            gifCardTemplate;

        window.FavoritesPage.ItemTemplate =
            gifCardTemplate;

        window.RecentsPage.ItemTemplate =
            gifCardTemplate;

        window.SearchPage.Loaded +=
            (_, _) =>
            {
                viewModel.ShowSearchCommand
                    .Execute(null);

                if (runtimeState.Settings
                        .Search
                        .ShowTrendingWhenEmpty &&
                    string.IsNullOrWhiteSpace(viewModel.Search.Query) &&
                    !viewModel.Search.HasResults &&
                    !viewModel.Search.IsBusy &&
                    viewModel.Search
                        .TrendingCommand
                        .CanExecute(null))
                {
                    viewModel.Search
                        .TrendingCommand
                        .Execute(null);
                }
            };

        window.FavoritesPage.Loaded +=
            (_, _) =>
            {
                if (viewModel.ShowFavoritesCommand
                    .CanExecute(null))
                {
                    viewModel.ShowFavoritesCommand
                        .Execute(null);
                }
            };

        window.RecentsPage.Loaded +=
            (_, _) =>
            {
                if (viewModel.ShowRecentsCommand
                    .CanExecute(null))
                {
                    viewModel.ShowRecentsCommand
                        .Execute(null);
                }
            };

        return window;
    }

    private static SettingsWindow CreateSettingsWindow(
        IServiceProvider services)
    {
        IServiceScope scope =
            services.CreateScope();

        try
        {
            SettingsWindow window =
                CreateSettingsWindowCore(
                    scope.ServiceProvider);

            window.Closed +=
                (_, _) =>
                    scope.Dispose();

            return window;
        }
        catch
        {
            scope.Dispose();

            throw;
        }
    }

    private static SettingsWindow
        CreateSettingsWindowCore(
            IServiceProvider services)
    {
        SettingsViewModel viewModel =
            services.GetRequiredService<
                SettingsViewModel>();

        UpdateViewModel updateViewModel =
            services.GetRequiredService<
                UpdateViewModel>();

        SettingsWindow window =
            new();

        window.GeneralContent =
            viewModel.General;

        window.SearchContent =
            viewModel.Search;

        window.AppearanceContent =
            viewModel.Appearance;

        window.LibraryContent =
            viewModel.Library;

        window.ApiContent =
            viewModel.Api;

        window.UpdatesContent =
            new UpdatesSettingsContent(
                viewModel.Updates,
                updateViewModel);

        window.GeneralContentTemplate =
            GetRequiredTemplate(
                GeneralSettingsTemplateKey);

        window.SearchContentTemplate =
            GetRequiredTemplate(
                SearchSettingsTemplateKey);

        window.AppearanceContentTemplate =
            GetRequiredTemplate(
                AppearanceSettingsTemplateKey);

        window.LibraryContentTemplate =
            GetRequiredTemplate(
                LibrarySettingsTemplateKey);

        window.ApiContentTemplate =
            GetRequiredTemplate(
                ApiSettingsTemplateKey);

        window.UpdatesContentTemplate =
            GetRequiredTemplate(
                UpdatesSettingsTemplateKey);

        ApplySettingsWindowState(
            window,
            viewModel);

        viewModel.PropertyChanged +=
            (_, eventArgs) =>
            {
                if (eventArgs.PropertyName is
                    nameof(SettingsViewModel.IsBusyOverall) or
                    nameof(SettingsViewModel.Message))
                {
                    ApplySettingsWindowState(
                        window,
                        viewModel);
                }
            };

        var controller = new SettingsWindowController(window, viewModel,
            services.GetRequiredService<SettingsEditSession>(),
            services.GetRequiredService<ShellRuntimeState>());
        window.Closed += (_, _) => controller.Dispose();
        // WindowManager owns CancelCommand so that Close runs the same resolve and
        // reopen path as the title bar close button.
        bool loaded = false;
        window.Activated += async (_, _) =>
        {
            if (loaded) return;
            loaded = true;
            await controller.LoadAsync();
            if (!updateViewModel.HasCurrentVersion) updateViewModel.Initialize(GetCurrentVersion());
        };

        return window;
    }

    private static OnboardingWindow
        CreateOnboardingWindow(
            IServiceProvider services)
    {
        IServiceScope scope =
            services.CreateScope();

        try
        {
            OnboardingWindow window =
                CreateOnboardingWindowCore(
                    scope.ServiceProvider);

            window.Closed +=
                (_, _) =>
                    scope.Dispose();

            return window;
        }
        catch
        {
            scope.Dispose();

            throw;
        }
    }

    private static OnboardingWindow
        CreateOnboardingWindowCore(
            IServiceProvider services)
    {
        OnboardingViewModel viewModel =
            services.GetRequiredService<
                OnboardingViewModel>();

        var providerOnboarding = new CopyGIF.Application.Onboarding.ProviderOnboardingCoordinator(
            services.GetRequiredService<ISettingsCoordinator>(), services.GetRequiredService<ISecretStore>(),
            services.GetServices<IGifProviderCredentialManager>(), services.GetRequiredService<IStartupService>(),
            services.GetRequiredService<IApplicationPaths>());
        viewModel.CompleteProvider = providerOnboarding.CompleteAsync;
        viewModel.OpenProviderHelp = services.GetRequiredService<IUriLauncherService>().TryLaunchAsync;

        OnboardingWindow window =
            new()
            {
                StepTitle =
                    "Connect GIF provider",

                StepDescription =
                    viewModel.Providers.Count > 1
                        ? "Choose a GIF provider and add its API key to start searching and copying GIFs."
                        : "Add your GIF provider's API key to start searching and copying GIFs.",

                StepContent =
                    viewModel,

                StepContentTemplate =
                    GetRequiredTemplate(
                        OnboardingTemplateKey)
            };

        ApplyOnboardingWindowState(
            window,
            viewModel);

        viewModel.PropertyChanged +=
            (_, eventArgs) =>
            {
                if (eventArgs.PropertyName is
                    nameof(OnboardingViewModel.IsBusy) or
                    nameof(OnboardingViewModel.IsLoaded) or
                    nameof(OnboardingViewModel.Credential) or
                    nameof(OnboardingViewModel.Message))
                {
                    ApplyOnboardingWindowState(
                        window,
                        viewModel);
                }
            };

        window.FinishCommand =
            new AsyncRelayCommand(
                async cancellationToken =>
                {
                    if (!viewModel.CompleteCommand
                        .CanExecute(null))
                    {
                        return;
                    }

                    try
                    {
                        await viewModel
                            .CompleteCommand
                            .ExecuteAsync(null)
                            .ConfigureAwait(true);

                        if (!viewModel.IsCompleted)
                        {
                            return;
                        }

                        var saved = await services.GetRequiredService<ISettingsCoordinator>().LoadAsync(cancellationToken);
                        services.GetRequiredService<ShellRuntimeState>().ApplySettings(saved);
                        await services
                            .GetRequiredService<
                                WindowManager>()
                            .CompleteOnboardingAsync(
                                cancellationToken)
                            .ConfigureAwait(true);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken
                            .IsCancellationRequested)
                    {
                    }
                    catch (Exception exception)
                    {
                        window.StatusMessage =
                            UserFailureMessages.Describe(
                                "onboarding-finish",
                                exception);

                        window.StatusSeverity =
                            InfoBarSeverity.Error;
                    }
                });

        bool loaded =
            false;

        window.Activated +=
            (_, _) =>
            {
                if (loaded)
                {
                    return;
                }

                loaded =
                    true;

                viewModel.LoadCommand
                    .Execute(null);
            };

        return window;
    }

    private static WindowManager CreateWindowManager(
        IServiceProvider services)
    {
        var manager = new WindowManager(
            () =>
                services.GetRequiredService<
                    MainWindow>(),
            () =>
                services.GetRequiredService<
                    SettingsWindow>(),
            () =>
                services.GetRequiredService<
                    OnboardingWindow>(),
            services.GetRequiredService<
                IApplicationStartupCoordinator>(),
            services.GetRequiredService<
                CopyGIF.Core.Contracts.IWindowPlacementService>(),
            services.GetRequiredService<
                ISettingsCoordinator>(),
            services.GetRequiredService<
                WinUiDispatcher>(),
            services.GetRequiredService<
                ThemeManager>());
        manager.AttachEffectiveSettings(services.GetRequiredService<EffectiveSettings>());
        return manager;
    }

    private static void ApplySettingsWindowState(
        SettingsWindow window,
        SettingsViewModel viewModel)
    {
        window.IsBusy =
            viewModel.IsBusyOverall;

        window.StatusMessage =
            viewModel.Message?.Text ??
            string.Empty;

        window.StatusSeverity =
            ToInfoBarSeverity(
                viewModel.Message?.Severity);
    }

    private static void ApplyOnboardingWindowState(
        OnboardingWindow window,
        OnboardingViewModel viewModel)
    {
        window.IsBusy =
            viewModel.IsBusy;

        window.CanFinish =
            viewModel.CompleteCommand
                .CanExecute(null);

        window.StatusMessage =
            viewModel.Message?.Text ??
            string.Empty;

        window.StatusSeverity =
            ToInfoBarSeverity(
                viewModel.Message?.Severity);
    }

    private static InfoBarSeverity ToInfoBarSeverity(
        UserMessageSeverity? severity)
    {
        return severity switch
        {
            UserMessageSeverity.Success =>
                InfoBarSeverity.Success,

            UserMessageSeverity.Warning =>
                InfoBarSeverity.Warning,

            UserMessageSeverity.Error =>
                InfoBarSeverity.Error,

            _ =>
                InfoBarSeverity.Informational
        };
    }

    private static IAsyncRelayCommand
        GetSettingsSaveCommand(
            CopyGIF.App.Views.SettingsSection section,
            SettingsViewModel viewModel)
    {
        return section switch
        {
            CopyGIF.App.Views.SettingsSection.General =>
                viewModel.General.SaveCommand,

            CopyGIF.App.Views.SettingsSection.Search =>
                viewModel.Search.SaveCommand,

            CopyGIF.App.Views.SettingsSection.Appearance =>
                viewModel.Appearance.SaveCommand,

            CopyGIF.App.Views.SettingsSection.Library =>
                viewModel.Library.SaveCommand,

            CopyGIF.App.Views.SettingsSection.Api =>
                viewModel.Api.SaveCommand,

            CopyGIF.App.Views.SettingsSection.Updates =>
                viewModel.Updates.SaveCommand,

            _ =>
                throw new InvalidOperationException(
                    "The selected settings section is not supported.")
        };
    }

    private static AsyncOperationState
        GetSettingsOperationState(
            CopyGIF.App.Views.SettingsSection section,
            SettingsViewModel viewModel)
    {
        return section switch
        {
            CopyGIF.App.Views.SettingsSection.General =>
                viewModel.General.OperationState,

            CopyGIF.App.Views.SettingsSection.Search =>
                viewModel.Search.OperationState,

            CopyGIF.App.Views.SettingsSection.Appearance =>
                viewModel.Appearance.OperationState,

            CopyGIF.App.Views.SettingsSection.Library =>
                viewModel.Library.OperationState,

            CopyGIF.App.Views.SettingsSection.Api =>
                viewModel.Api.OperationState,

            CopyGIF.App.Views.SettingsSection.Updates =>
                viewModel.Updates.OperationState,

            _ =>
                throw new InvalidOperationException(
                    "The selected settings section is not supported.")
        };
    }

    private static void BindSearchPage(
        SearchPage page,
        CopyGIF.Presentation.Search.SearchViewModel
            viewModel)
    {
        page.DataContext =
            viewModel;

        BindTwoWay(
            page,
            SearchPage.QueryProperty,
            viewModel,
            nameof(viewModel.Query));

        Bind(
            page,
            SearchPage.SuggestionsProperty,
            viewModel,
            nameof(viewModel.Suggestions));

        Bind(
            page,
            SearchPage.ItemsSourceProperty,
            viewModel,
            nameof(viewModel.Results));

        Bind(
            page,
            SearchPage.IsBusyProperty,
            viewModel,
            nameof(viewModel.IsBusy));

        Bind(
            page,
            SearchPage.IsEmptyProperty,
            viewModel,
            nameof(viewModel.HasResults),
            converter:
                BooleanNegationConverter.Instance);

        Bind(
            page,
            SearchPage.CanLoadMoreProperty,
            viewModel,
            nameof(viewModel.HasMoreResults));

        Bind(
            page,
            SearchPage.StatusMessageProperty,
            viewModel,
            "Message.Text");

        Bind(
            page,
            SearchPage.StatusSeverityProperty,
            viewModel,
            "Message.Severity",
            converter:
                UserMessageSeverityConverter.Instance);

        page.SearchCommand =
            viewModel.SearchCommand;

        page.DebouncedSearchCommand =
            viewModel.SearchDebouncedCommand;

        page.RefreshSuggestionsCommand =
            viewModel.RefreshSuggestionsCommand;

        page.ClearQueryCommand =
            viewModel.ClearQueryCommand;

        page.CancelCommand =
            viewModel.CancelCommand;

        page.LoadMoreCommand =
            viewModel.LoadMoreCommand;
        page.ClearHistoryCommand = viewModel.ClearSuggestionHistoryCommand;
    }

    private static void BindFavoritesPage(
        FavoritesPage page,
        CopyGIF.Presentation.Library.FavoritesViewModel
            viewModel)
    {
        page.DataContext =
            viewModel;

        Bind(
            page,
            FavoritesPage.ItemsSourceProperty,
            viewModel,
            nameof(viewModel.Items));

        Bind(
            page,
            FavoritesPage.IsBusyProperty,
            viewModel,
            nameof(viewModel.IsBusy));

        Bind(
            page,
            FavoritesPage.IsEmptyProperty,
            viewModel,
            nameof(viewModel.HasItems),
            converter:
                BooleanNegationConverter.Instance);

        Bind(
            page,
            FavoritesPage.StatusMessageProperty,
            viewModel,
            "Message.Text");

        Bind(
            page,
            FavoritesPage.StatusSeverityProperty,
            viewModel,
            "Message.Severity",
            converter:
                UserMessageSeverityConverter.Instance);

        page.RefreshCommand =
            viewModel.LoadCommand;
    }

    private static void BindRecentsPage(
        RecentsPage page,
        CopyGIF.Presentation.Library.RecentsViewModel
            viewModel)
    {
        page.DataContext =
            viewModel;

        Bind(
            page,
            RecentsPage.ItemsSourceProperty,
            viewModel,
            nameof(viewModel.Items));

        Bind(
            page,
            RecentsPage.IsBusyProperty,
            viewModel,
            nameof(viewModel.IsBusy));

        Bind(
            page,
            RecentsPage.IsEmptyProperty,
            viewModel,
            nameof(viewModel.HasItems),
            converter:
                BooleanNegationConverter.Instance);

        Bind(
            page,
            RecentsPage.StatusMessageProperty,
            viewModel,
            "Message.Text");

        Bind(
            page,
            RecentsPage.StatusSeverityProperty,
            viewModel,
            "Message.Severity",
            converter:
                UserMessageSeverityConverter.Instance);

        page.RefreshCommand =
            viewModel.LoadCommand;

        page.ClearHistoryCommand =
            viewModel.ClearCommand;
    }

    private static void Bind(
        FrameworkElement target,
        DependencyProperty property,
        object source,
        string path,
        IValueConverter? converter = null)
    {
        target.SetBinding(
            property,
            new Binding
            {
                Source =
                    source,

                Path =
                    new PropertyPath(
                        path),

                Mode =
                    BindingMode.OneWay,

                Converter =
                    converter
            });
    }

    private static void BindTwoWay(
        FrameworkElement target,
        DependencyProperty property,
        object source,
        string path)
    {
        target.SetBinding(
            property,
            new Binding
            {
                Source =
                    source,

                Path =
                    new PropertyPath(
                        path),

                Mode =
                    BindingMode.TwoWay,

                UpdateSourceTrigger =
                    UpdateSourceTrigger.PropertyChanged
            });
    }

    private static DataTemplate GetRequiredTemplate(
        string key)
    {
        object resource =
            XamlApplication.Current.Resources[
                key];

        return resource as DataTemplate ??
            throw new InvalidOperationException(
                $"Application resource '{key}' is not a DataTemplate.");
    }

    private static string GetCurrentVersion() =>
        new EntryAssemblyVersion().Current;

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(
                ref _disposeState) != 0,
            this);
    }

    private sealed class WindowAwareGifCopyCoordinator :
        IGifCopyCoordinator
    {
        private readonly GifCopyCoordinator
            _inner;

        private readonly IServiceProvider
            _services;

        public WindowAwareGifCopyCoordinator(
            GifCopyCoordinator inner,
            IServiceProvider services)
        {
            _inner =
                inner ??
                throw new ArgumentNullException(
                    nameof(inner));

            _services =
                services ??
                throw new ArgumentNullException(
                    nameof(services));
        }

        public async Task<DownloadedGif> CopyAsync(
            CopyGIF.Core.Models.GifItem item,
            string? searchQuery,
            CancellationToken cancellationToken = default)
        {
            DownloadedGif result =
                await _inner
                    .CopyAsync(
                        item,
                        searchQuery,
                        async () =>
                        {
                            try
                            {
                                await _services.GetRequiredService<WindowManager>()
                                    .HandleCopyCompletedAsync(CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception exception)
                            {
                                CopyGIF.Core.Models.RepairDiagnostics.Record(
                                    "hide-after-copy", item.ProviderId, exception.GetType().Name);
                            }
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            return result;
        }
    }
}

public sealed class ShellRuntimeState :
    IDisposable
{
    private readonly MainViewModel
        _mainViewModel;

    private readonly ThemeManager
        _themeManager;

    private bool _disposed;

    public ShellRuntimeState(
        MainViewModel mainViewModel,
        ThemeManager themeManager)
    {
        _mainViewModel =
            mainViewModel ??
            throw new ArgumentNullException(
                nameof(mainViewModel));

        _themeManager =
            themeManager ??
            throw new ArgumentNullException(
                nameof(themeManager));

        _themeManager.AnimationsEnabledChanged +=
            HandleAnimationsEnabledChanged;
    }

    public AppSettings Settings
    {
        get;
        private set;
    } =
        new();

    public void ApplySettings(
        AppSettings settings)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        Settings =
            settings ??
            throw new ArgumentNullException(
                nameof(settings));

        _mainViewModel.Search.Configure(settings);
        ApplyMotionPreference();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _themeManager.AnimationsEnabledChanged -=
            HandleAnimationsEnabledChanged;
    }

    private void HandleAnimationsEnabledChanged(
        object? sender,
        EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        ApplyMotionPreference();
    }

    private void ApplyMotionPreference()
    {
        _mainViewModel.Search.DisplayQuality = Settings.Appearance.DisplayQuality;
        _mainViewModel.Favorites.DisplayQuality = Settings.Appearance.DisplayQuality;
        _mainViewModel.Recents.DisplayQuality = Settings.Appearance.DisplayQuality;
        _mainViewModel.ReducedMotion =
            !Settings.Search.AnimatePreviews ||
            !_themeManager.AnimationsEnabled;
    }
}

public sealed record UpdatesSettingsContent(
    UpdateSettingsViewModel Settings,
    UpdateViewModel Update);

public sealed class BooleanNegationConverter :
    IValueConverter
{
    public static BooleanNegationConverter Instance
    { get; } =
        new();

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        _ = targetType;
        _ = parameter;
        _ = language;

        return value is bool flag &&
            !flag;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        _ = value;
        _ = targetType;
        _ = parameter;
        _ = language;

        throw new NotSupportedException();
    }
}

public sealed class UserMessageSeverityConverter :
    IValueConverter
{
    public static UserMessageSeverityConverter Instance
    { get; } =
        new();

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        _ = targetType;
        _ = parameter;
        _ = language;

        return value is UserMessageSeverity severity
            ? severity switch
            {
                UserMessageSeverity.Information =>
                    InfoBarSeverity.Informational,

                UserMessageSeverity.Success =>
                    InfoBarSeverity.Success,

                UserMessageSeverity.Warning =>
                    InfoBarSeverity.Warning,

                UserMessageSeverity.Error =>
                    InfoBarSeverity.Error,

                _ =>
                    InfoBarSeverity.Informational
            }
            : InfoBarSeverity.Informational;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        _ = value;
        _ = targetType;
        _ = parameter;
        _ = language;

        throw new NotSupportedException();
    }
}
