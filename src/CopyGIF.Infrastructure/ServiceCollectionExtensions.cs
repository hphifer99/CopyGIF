using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Infrastructure.Klipy;
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

        services.AddSingleton<
            IPreviewCache,
            PreviewCache>();

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

        services.AddSingleton(
            new ProviderDescriptor
            {
                Id =
                    KlipyGifProvider.ProviderId,

                DisplayName =
                    "KLIPY",

                Capabilities =
                    ProviderCapabilities.Search |
                    ProviderCapabilities.Trending |
                    ProviderCapabilities.Pagination |
                    ProviderCapabilities.CredentialValidation |
                    ProviderCapabilities.ShareRegistration,

                RequiresCredential =
                    true,

                AttributionText =
                    "Powered by KLIPY",

                AttributionUri =
                    new Uri(
                        "https://klipy.com/")
            });

        services.AddSingleton<
            IHostAddressResolver,
            SystemHostAddressResolver>();

        services.AddSingleton(
            serviceProvider =>
                new MediaHostPolicy(
                    serviceProvider
                        .GetRequiredService<
                            IHostAddressResolver>(),
                    [
                        "static.klipy.com"
                    ]));

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

        return services;
    }
}
