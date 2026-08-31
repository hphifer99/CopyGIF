using System;
using CopyGIF.Application;
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

    private static IServiceProvider
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

    protected override void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        _window =
            Services.GetRequiredService<
                MainWindow>();

        _window.Activate();
    }
}