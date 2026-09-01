using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Infrastructure.Klipy;
using CopyGIF.Infrastructure.Migration;
using CopyGIF.Infrastructure.Storage;
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
            ISearchHistoryStore,
            JsonSearchHistoryStore>();

        services.AddSingleton<
            IMigrationStateStore,
            JsonMigrationStateStore>();

        services.AddSingleton<
            V1SettingsReader>();

        services.AddSingleton<
            V1LibraryReader>();

        services.AddSingleton<
            IMigrationCoordinator,
            V1MigrationCoordinator>();

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

        return services;
    }
}
