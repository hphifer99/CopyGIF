using CopyGIF.Presentation.Library;
using CopyGIF.Presentation.Main;
using CopyGIF.Presentation.Onboarding;
using CopyGIF.Presentation.Search;
using CopyGIF.Presentation.Settings;
using CopyGIF.Presentation.Updates;
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
            SearchViewModel>();

        services.AddTransient<
            FavoritesViewModel>();

        services.AddTransient<
            RecentsViewModel>();

        services.AddTransient<
            OnboardingViewModel>();

        services.AddTransient<
            ApiSettingsViewModel>();

        services.AddTransient<
            AppearanceSettingsViewModel>();

        services.AddTransient<
            GeneralSettingsViewModel>();

        services.AddTransient<
            LibrarySettingsViewModel>();

        services.AddTransient<
            SearchSettingsViewModel>();

        services.AddTransient<
            UpdateSettingsViewModel>();

        services.AddTransient<
            SettingsViewModel>();

        services.AddTransient<
            UpdateViewModel>();

        services.AddTransient<
            MainViewModel>();

        return services;
    }
}
