using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Infrastructure.Klipy;
using CopyGIF.Infrastructure.Giphy;
using CopyGIF.Infrastructure.Media;
using CopyGIF.Infrastructure.Migration;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Infrastructure.Time;
using CopyGIF.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection
        AddCopyGifInfrastructure(
            this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        services.AddSingleton<
            IApplicationPaths,
            ApplicationPaths>();

        services.AddSingleton<
            AtomicFileWriter>();

        services.AddSingleton<
            OwnedPathGuard>();

        services.AddSingleton(
            PreviewCacheLimits.Default);

        services.AddSingleton<PreviewCache>();

        services.AddSingleton<IPreviewCache, SecurePreviewCache>();

        services.AddHttpClient(nameof(SecurePreviewCache), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip |
                DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            MaxConnectionsPerServer = MediaPolicy.MaximumConcurrentMediaRequests,
            MaxResponseHeadersLength = 32
        });

        services.AddSingleton<
            IClock,
            SystemClock>();

        services.AddSingleton<
            CorruptFileRecovery>();

        services.AddSingleton<
            VersionedJsonSerializer>();

        services.AddSingleton<
            ISettingsStore,
            JsonSettingsStore>();

        services.AddSingleton<
            ILibraryStore,
            JsonLibraryStore>();

        services.AddSingleton<
            ILibraryStorageMover,
            LibraryStorageMover>();

        services.AddSingleton<
            ISearchHistoryStore,
            JsonSearchHistoryStore>();

        services.AddSingleton<
            IMigrationStateStore,
            JsonMigrationStateStore>();

        services.AddSingleton<
            IUpdateStateStore,
            JsonUpdateStateStore>();

        services.AddSingleton<
            UpdateManifestParser>();

        services.AddSingleton<
            V1SettingsReader>();

        services.AddSingleton<
            V1LibraryReader>();

        services.AddSingleton<
            IMigrationCoordinator,
            V1MigrationCoordinator>();

        // Every provider is registered with a descriptor that carries all of its settings. To add
        // a provider, register its descriptor here (or beside its own code) together with its
        // IGifProvider and IGifProviderCredentialManager. Nothing else needs to change.
        services.AddSingleton(
            BuiltInProviders.Klipy);

        services.AddSingleton<
            IHostAddressResolver,
            SystemHostAddressResolver>();

        services.AddSingleton(
            serviceProvider =>
                new MediaHostPolicy(
                    serviceProvider
                        .GetRequiredService<
                            IHostAddressResolver>(),
                    serviceProvider
                        .GetServices<
                            ProviderDescriptor>()));

        services
            .AddHttpClient<KlipyGifProvider>(
                httpClient =>
                {
                    httpClient.BaseAddress =
                        new Uri(
                            "https://api.klipy.com/");

                    httpClient.Timeout =
                        TimeSpan.FromSeconds(
                            20);
                })
            // KLIPY puts the API key in the request path, so keep request URLs out of any
            // logging provider that might be registered later (same hardening as GIPHY).
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(
                () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect =
                            false,

                        AutomaticDecompression =
                            DecompressionMethods.GZip |
                            DecompressionMethods.Deflate |
                            DecompressionMethods.Brotli,

                        ConnectTimeout =
                            TimeSpan.FromSeconds(
                                10)
                    });

        services.AddTransient<IGifProvider>(
            serviceProvider =>
                serviceProvider
                    .GetRequiredService<
                        KlipyGifProvider>());

        services.AddTransient<
            IGifProviderCredentialManager,
            KlipyCredentialManager>();

        services
            .AddHttpClient<
                SecureGifDownloader>(
                httpClient =>
                {
                    httpClient.Timeout =
                        TimeSpan.FromSeconds(
                            30);
                })
            .ConfigurePrimaryHttpMessageHandler(
                () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect =
                            false,

                        AutomaticDecompression =
                            DecompressionMethods.GZip |
                            DecompressionMethods.Deflate |
                            DecompressionMethods.Brotli,

                        ConnectTimeout =
                            TimeSpan.FromSeconds(
                                10),

                        MaxConnectionsPerServer =
                            MediaPolicy
                                .MaximumConcurrentMediaRequests,

                        MaxResponseHeadersLength =
                            32
                    });

        services.AddTransient<IGifDownloader>(
            serviceProvider =>
                serviceProvider
                    .GetRequiredService<
                        SecureGifDownloader>());

        services
            .AddHttpClient<GitHubUpdateFeed>(
                httpClient =>
                {
                    httpClient.Timeout =
                        TimeSpan.FromSeconds(
                            30);
                })
            .ConfigurePrimaryHttpMessageHandler(
                () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect =
                            false,

                        AutomaticDecompression =
                            DecompressionMethods.GZip |
                            DecompressionMethods.Deflate |
                            DecompressionMethods.Brotli,

                        ConnectTimeout =
                            TimeSpan.FromSeconds(
                                10),

                        MaxResponseHeadersLength =
                            32
                    });

        services.AddTransient<IUpdateFeed>(
            serviceProvider =>
                serviceProvider
                    .GetRequiredService<
                        GitHubUpdateFeed>());

        services
            .AddHttpClient<
                HttpUpdatePackageService>(
                httpClient =>
                {
                    httpClient.Timeout =
                        TimeSpan.FromMinutes(
                            10);
                })
            .ConfigurePrimaryHttpMessageHandler(
                () =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect =
                            false,

                        AutomaticDecompression =
                            DecompressionMethods.None,

                        ConnectTimeout =
                            TimeSpan.FromSeconds(
                                10),

                        MaxConnectionsPerServer =
                            2,

                        MaxResponseHeadersLength =
                            32
                    });

        services.AddTransient<IUpdatePackageService>(
            serviceProvider =>
                serviceProvider
                    .GetRequiredService<
                        HttpUpdatePackageService>());

        services.AddSingleton(
            BuiltInProviders.Giphy);
        services.AddHttpClient<GiphyGifProvider>(client => client.Timeout = TimeSpan.FromSeconds(25))
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                MaxResponseHeadersLength = 32
            });
        services.AddTransient<IGifProvider>(provider => provider.GetRequiredService<GiphyGifProvider>());
        services.AddTransient<IGifProviderCredentialManager, GiphyCredentialManager>();
        return services;
    }
}
