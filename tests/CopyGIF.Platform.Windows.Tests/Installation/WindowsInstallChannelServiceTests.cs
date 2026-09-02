using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Installation;
using Microsoft.Win32;

namespace CopyGIF.Platform.Windows.Tests.Installation;

[TestClass]
public sealed class WindowsInstallChannelServiceTests
{
    [TestMethod]
    public async Task GetCurrentAsync_PackageIdentity_ReturnsMicrosoftStore()
    {
        FakeRegistryValueReader registry =
            new();

        registry.SetMsiMarker(
            RegistryHive.LocalMachine);

        WindowsInstallChannelService service =
            new(
                new FakePackageIdentityDetector(
                    hasIdentity: true),
                registry);

        InstallationContext result =
            await service.GetCurrentAsync();

        Assert.AreEqual(
            InstallChannel.MicrosoftStore,
            result.Channel);

        Assert.AreEqual(
            InstallScope.CurrentUser,
            result.Scope);
    }

    [TestMethod]
    public async Task GetCurrentAsync_MachineMsiMarker_ReturnsAllUsersMsi()
    {
        FakeRegistryValueReader registry =
            new();

        registry.SetMsiMarker(
            RegistryHive.LocalMachine);

        WindowsInstallChannelService service =
            CreateUnpackagedService(registry);

        InstallationContext result =
            await service.GetCurrentAsync();

        Assert.AreEqual(
            InstallChannel.Msi,
            result.Channel);

        Assert.AreEqual(
            InstallScope.AllUsers,
            result.Scope);
    }

    [TestMethod]
    public async Task GetCurrentAsync_UserMsiMarker_ReturnsCurrentUserMsi()
    {
        FakeRegistryValueReader registry =
            new();

        registry.SetMsiMarker(
            RegistryHive.CurrentUser);

        WindowsInstallChannelService service =
            CreateUnpackagedService(registry);

        InstallationContext result =
            await service.GetCurrentAsync();

        Assert.AreEqual(
            InstallChannel.Msi,
            result.Channel);

        Assert.AreEqual(
            InstallScope.CurrentUser,
            result.Scope);
    }

    [TestMethod]
    public async Task GetCurrentAsync_NoIdentityOrMarker_ReturnsNone()
    {
        WindowsInstallChannelService service =
            CreateUnpackagedService(
                new FakeRegistryValueReader());

        InstallationContext result =
            await service.GetCurrentAsync();

        Assert.AreEqual(
            InstallChannel.None,
            result.Channel);

        Assert.AreEqual(
            InstallScope.None,
            result.Scope);
    }

    [TestMethod]
    public async Task GetCurrentAsync_UnknownRegistryValue_IsIgnored()
    {
        FakeRegistryValueReader registry =
            new();

        registry.SetValue(
            RegistryHive.LocalMachine,
            "Unknown");

        WindowsInstallChannelService service =
            CreateUnpackagedService(registry);

        InstallationContext result =
            await service.GetCurrentAsync();

        Assert.AreEqual(
            InstallChannel.None,
            result.Channel);
    }

    private static WindowsInstallChannelService
        CreateUnpackagedService(
            FakeRegistryValueReader registry)
    {
        return new WindowsInstallChannelService(
            new FakePackageIdentityDetector(
                hasIdentity: false),
            registry);
    }

    private sealed class FakePackageIdentityDetector :
        IPackageIdentityDetector
    {
        private readonly bool _hasIdentity;

        public FakePackageIdentityDetector(
            bool hasIdentity)
        {
            _hasIdentity = hasIdentity;
        }

        public bool HasPackageIdentity()
        {
            return _hasIdentity;
        }
    }

    private sealed class FakeRegistryValueReader :
        IRegistryValueReader
    {
        private readonly Dictionary<RegistryHive, object?>
            _values =
                new();

        public object? ReadValue(
            RegistryHive hive,
            string subKey,
            string valueName)
        {
            _values.TryGetValue(
                hive,
                out object? value);

            return value;
        }

        public void SetMsiMarker(
            RegistryHive hive)
        {
            SetValue(
                hive,
                CopyGifRegistry.MsiInstallChannelValue);
        }

        public void SetValue(
            RegistryHive hive,
            object? value)
        {
            _values[hive] = value;
        }
    }
}
