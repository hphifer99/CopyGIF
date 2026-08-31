using System;
using System.Net;
using System.Net.Http;
using CopyGIF.Core.Contracts;
using CopyGIF.Infrastructure.Klipy;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Platform.Windows.Secrets;
using CopyGIF.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace CopyGIF.App;

public partial class App : Application
{
    private Window? _window;

    public IServiceProvider Services { get; }

    public new static App Current =>
        (App)Application.Current;

    public App()
    {
        Services = ConfigureServices();

        InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        //
        // Application storage
        //

        services.AddSingleton<ApplicationPaths>();

        services.AddSingleton<
            ISettingsStore,
            JsonSettingsStore>();

        //
        // Secure credential storage
        //

        services.AddSingleton<ISecretStore>(
            serviceProvider =>
            {
                ApplicationPaths paths =
                    serviceProvider
                        .GetRequiredService<ApplicationPaths>();

                paths.EnsureDirectoriesExist();

                return new DpapiSecretStore(
                    paths.SecretsDirectory);
            });

        //
        // KLIPY HTTP client
        //

        services
            .AddHttpClient<KlipyGifProvider>(
                httpClient =>
                {
                    httpClient.BaseAddress =
                        new Uri(
                            "https://api.klipy.com/");

                    httpClient.Timeout =
                        TimeSpan.FromSeconds(20);
                })
            .ConfigurePrimaryHttpMessageHandler(
                () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,

                        AutomaticDecompression =
                            DecompressionMethods.GZip |
                            DecompressionMethods.Deflate |
                            DecompressionMethods.Brotli,

                        ConnectTimeout =
                            TimeSpan.FromSeconds(10)
                    });

        //
        // GIF provider abstraction
        //

        services.AddTransient<IGifProvider>(
            serviceProvider =>
                serviceProvider
                    .GetRequiredService<KlipyGifProvider>());

        //
        // Presentation layer
        //

        services.AddSingleton<MainViewModel>();

        //
        // WinUI shell
        //

        services.AddSingleton<MainWindow>();

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
            Services.GetRequiredService<MainWindow>();

        _window.Activate();
    }
}