using CopyGIF.Application.Search;
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
            IGifSearchCoordinator,
            GifSearchCoordinator>();

        return services;
    }
}