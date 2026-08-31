using CopyGIF.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Presentation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection
        AddCopyGifPresentation(
            this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(
            services);

        services.AddTransient<
            MainViewModel>();

        return services;
    }
}