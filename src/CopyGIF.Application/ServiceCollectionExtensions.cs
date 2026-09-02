using CopyGIF.Application.Providers;
using CopyGIF.Application.Search;
using CopyGIF.Application.Setup;
using CopyGIF.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection
        AddCopyGifApplication(
            this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        services.AddTransient<
            IProviderCatalog,
            ProviderCatalog>();

        services.AddTransient<
            IActiveGifProviderAccessor,
            ActiveGifProviderAccessor>();

        services.AddTransient<
            IGifSearchCoordinator,
            GifSearchCoordinator>();

        services.AddTransient<
            IProviderSetupCoordinator,
            ProviderSetupCoordinator>();

        return services;
    }
}
