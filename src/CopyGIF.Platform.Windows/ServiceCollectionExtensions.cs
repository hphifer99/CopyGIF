using CopyGIF.Core.Contracts;
using CopyGIF.Platform.Windows.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Platform.Windows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection
        AddCopyGifWindowsPlatform(
            this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        services.AddSingleton<ISecretStore>(
            serviceProvider =>
            {
                IApplicationPaths paths =
                    serviceProvider
                        .GetRequiredService<
                            IApplicationPaths>();

                paths.EnsureDirectoriesExist();

                return new DpapiSecretStore(
                    paths.SecretsDirectory);
            });

        services.AddSingleton<
            ILegacyCredentialDecoder,
            DpapiLegacyCredentialDecoder>();

        return services;
    }
}
