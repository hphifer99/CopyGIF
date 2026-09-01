using CopyGIF.Application;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure;
using CopyGIF.Platform.Windows;
using CopyGIF.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace CopyGIF.App;

public partial class App : XamlApplication
{
    private Window? _window;

    public IServiceProvider Services { get; }

    public App()
    {
        InitializeComponent();

        Services =
            ConfigureServices();
    }

    private static ServiceProvider
        ConfigureServices()
    {
        ServiceCollection services =
            new();

        services
            .AddCopyGifInfrastructure();

        services
            .AddCopyGifWindowsPlatform();

        services
            .AddCopyGifApplication();

        services
            .AddCopyGifPresentation();

        services.AddTransient<
            MainWindow>();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    protected override async void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        try
        {
            IMigrationCoordinator migrationCoordinator =
                Services.GetRequiredService<
                    IMigrationCoordinator>();

            MigrationResult migrationResult =
                await migrationCoordinator
                    .MigrateIfNeededAsync();

            if (!migrationResult.Succeeded)
            {
                ShowStartupFailure(
                    migrationResult.Message);

                return;
            }

            _window =
                Services.GetRequiredService<
                    MainWindow>();

            _window.Activate();
        }
        catch (Exception)
        {
            ShowStartupFailure(
                "CopyGIF could not verify or migrate its saved data.");
        }
    }

    private void ShowStartupFailure(
        string? message)
    {
        _window =
            new StartupFailureWindow(
                string.IsNullOrWhiteSpace(message)
                    ? "CopyGIF could not start safely."
                    : message);

        _window.Activate();
    }
}
