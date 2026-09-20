using CopyGIF.Core.Contracts;
using CopyGIF.Platform.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Platform.Windows.Tests.Composition;

[TestClass]
public sealed class WindowsPlatformCompositionTests
{
    [TestMethod]
    public void AddCopyGifWindowsPlatform_RegistersEveryPlatformContract()
    {
        ServiceCollection services = new();

        services.AddCopyGifWindowsPlatform();

        Type[] expectedServiceTypes =
        [
            typeof(ISecretStore),
            typeof(ILegacyCredentialDecoder),
            typeof(IWindowHandleProvider),
            typeof(IClipboardService),
            typeof(IFolderPickerService),
            typeof(IUriLauncherService),
            typeof(IHotkeyService),
            typeof(ISingleInstanceService),
            typeof(ITrayService),
            typeof(IInstallChannelService),
            typeof(IStartupService),
            typeof(IWindowPlacementService),
            typeof(IUpdateInstaller)
        ];

        foreach (Type serviceType in
                 expectedServiceTypes)
        {
            ServiceDescriptor? descriptor =
                services.LastOrDefault(
                    candidate =>
                        candidate.ServiceType ==
                        serviceType);

            Assert.IsNotNull(
                descriptor,
                $"{serviceType.Name} was not registered.");

            Assert.AreEqual(
                ServiceLifetime.Singleton,
                descriptor.Lifetime,
                $"{serviceType.Name} must be a singleton.");
        }
    }
}
