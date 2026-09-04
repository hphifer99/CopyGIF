using CopyGIF.Application.Credentials;
using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Providers;
using CopyGIF.Application.Search;
using CopyGIF.Application.Settings;
using CopyGIF.Application.Setup;
using CopyGIF.Application.Startup;
using CopyGIF.Application.Updates;
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
            ISearchSuggestionCoordinator,
            SearchSuggestionCoordinator>();

        services.AddTransient<
            IGifSearchCoordinator,
            GifSearchCoordinator>();

        services.AddTransient<
            IPreviewCoordinator,
            PreviewCoordinator>();

        services.AddSingleton<
            IGifLibraryCoordinator,
            GifLibraryCoordinator>();

        services.AddTransient<
            IGifCopyCoordinator,
            GifCopyCoordinator>();

        services.AddSingleton<
            ISettingsCoordinator,
            SettingsCoordinator>();

        services.AddTransient<
            IApiCredentialCoordinator,
            ApiCredentialCoordinator>();

        services.AddTransient<
            IOnboardingCoordinator,
            OnboardingCoordinator>();

        services.AddTransient<
            IProviderSetupCoordinator,
            ProviderSetupCoordinator>();

        services.AddSingleton<
            IUpdateCoordinator,
            UpdateCoordinator>();

        services.AddSingleton<
            IApplicationStartupCoordinator,
            ApplicationStartupCoordinator>();

        return services;
    }
}
