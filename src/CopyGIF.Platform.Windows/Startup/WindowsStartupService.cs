using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Platform.Windows.Startup;

public sealed class WindowsStartupService :
    IStartupService
{
    private readonly IInstallChannelService
        _installChannelService;

    private readonly IStartupRegistrationController
        _storeController;

    private readonly IStartupRegistrationController
        _msiController;

    public WindowsStartupService(
        IInstallChannelService installChannelService)
        : this(
            installChannelService,
            new StoreStartupRegistrationController(),
            new RegistryStartupRegistrationController())
    {
    }

    internal WindowsStartupService(
        IInstallChannelService installChannelService,
        IStartupRegistrationController storeController,
        IStartupRegistrationController msiController)
    {
        _installChannelService =
            installChannelService ??
            throw new ArgumentNullException(
                nameof(installChannelService));

        _storeController =
            storeController ??
            throw new ArgumentNullException(
                nameof(storeController));

        _msiController =
            msiController ??
            throw new ArgumentNullException(
                nameof(msiController));
    }

    public async Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        InstallationContext context =
            await _installChannelService
                .GetCurrentAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return context.Channel switch
        {
            InstallChannel.MicrosoftStore or InstallChannel.DevelopmentPackage or InstallChannel.SideloadedPackage =>
                await _storeController
                    .IsEnabledAsync(
                        cancellationToken)
                    .ConfigureAwait(false),

            InstallChannel.Msi =>
                await _msiController
                    .IsEnabledAsync(
                        cancellationToken)
                    .ConfigureAwait(false),

            _ => false
        };
    }

    public async Task<bool?> GetRegistrationStateAsync(
        CancellationToken cancellationToken = default)
    {
        InstallationContext context =
            await _installChannelService
                .GetCurrentAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (context.Channel == InstallChannel.None)
        {
            return null;
        }

        return await IsEnabledAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        InstallationContext context =
            await _installChannelService
                .GetCurrentAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        switch (context.Channel)
        {
            case InstallChannel.MicrosoftStore:
            case InstallChannel.DevelopmentPackage:
            case InstallChannel.SideloadedPackage:
                await _storeController
                    .SetEnabledAsync(
                        enabled,
                        cancellationToken)
                    .ConfigureAwait(false);

                break;

            case InstallChannel.Msi:
                await _msiController
                    .SetEnabledAsync(
                        enabled,
                        cancellationToken)
                    .ConfigureAwait(false);

                break;

            default:
                if (enabled)
                {
                    // Development runs have no registration target. Saving other
                    // settings must remain possible until the app is installed.
                    break;
                }

                await _msiController
                    .SetEnabledAsync(
                        enabled: false,
                        cancellationToken)
                    .ConfigureAwait(false);

                break;
        }
    }
}
