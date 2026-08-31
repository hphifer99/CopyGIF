using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Infrastructure.Klipy;
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
            ISettingsStore,
            JsonSettingsStore>();

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