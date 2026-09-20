using CopyGIF.Core.Contracts;
using CopyGIF.Platform.Windows.Clipboard;
using CopyGIF.Platform.Windows.Display;
using CopyGIF.Platform.Windows.Hotkeys;
using CopyGIF.Platform.Windows.Installation;
using CopyGIF.Platform.Windows.Secrets;
using CopyGIF.Platform.Windows.Shell;
using CopyGIF.Platform.Windows.SingleInstance;
using CopyGIF.Platform.Windows.Startup;
using CopyGIF.Platform.Windows.Tray;
using CopyGIF.Platform.Windows.Updates;
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

        services.AddSingleton<
            IWindowHandleProvider,
            ProcessWindowHandleProvider>();

        services.AddSingleton<
            IClipboardService,
            GifClipboardService>();

        services.AddSingleton<
            IFolderPickerService,
            FolderPickerService>();

        services.AddSingleton<
            IUriLauncherService,
            UriLauncherService>();

        services.AddSingleton<
            IHotkeyService,
            WindowsHotkeyService>();

        services.AddSingleton<
            ISingleInstanceService,
            WindowsSingleInstanceService>();

        services.AddSingleton<
            ITrayService,
            WindowsTrayService>();

        services.AddSingleton<
            IInstallChannelService,
            WindowsInstallChannelService>();

        services.AddSingleton<
            IStartupService,
            WindowsStartupService>();

        services.AddSingleton<
            IWindowPlacementService,
            WindowsWindowPlacementService>();

        services.AddSingleton<
            IUpdateInstaller,
            WindowsUpdateInstaller>();

        return services;
    }
}
