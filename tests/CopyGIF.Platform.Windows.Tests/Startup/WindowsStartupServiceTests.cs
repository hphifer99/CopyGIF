using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Startup;

namespace CopyGIF.Platform.Windows.Tests.Startup;

[TestClass]
public sealed class WindowsStartupServiceTests
{
    [TestMethod]
    public async Task IsEnabledAsync_StoreInstall_UsesStoreController()
    {
        FakeStartupController store =
            new(isEnabled: true);

        FakeStartupController msi =
            new(isEnabled: false);

        WindowsStartupService service =
            CreateService(
                InstallChannel.MicrosoftStore,
                store,
                msi);

        bool result =
            await service.IsEnabledAsync();

        Assert.IsTrue(result);

        Assert.AreEqual(
            1,
            store.ReadCallCount);

        Assert.AreEqual(
            0,
            msi.ReadCallCount);
    }

    [TestMethod]
    public async Task SetEnabledAsync_MsiInstall_UsesMsiController()
    {
        FakeStartupController store =
            new();

        FakeStartupController msi =
            new();

        WindowsStartupService service =
            CreateService(
                InstallChannel.Msi,
                store,
                msi);

        await service.SetEnabledAsync(
            enabled: true);

        Assert.AreEqual(
            0,
            store.WriteCallCount);

        Assert.AreEqual(
            1,
            msi.WriteCallCount);

        Assert.AreEqual(
            true,
            msi.LastEnabledValue);
    }

    [TestMethod]
    public async Task SetEnabledAsync_UninstalledEnable_IsRejected()
    {
        WindowsStartupService service =
            CreateService(
                InstallChannel.None,
                new FakeStartupController(),
                new FakeStartupController());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () =>
                service.SetEnabledAsync(
                    enabled: true));
    }

    [TestMethod]
    public async Task SetEnabledAsync_UninstalledDisable_RemovesStaleMsiEntry()
    {
        FakeStartupController msi =
            new();

        WindowsStartupService service =
            CreateService(
                InstallChannel.None,
                new FakeStartupController(),
                msi);

        await service.SetEnabledAsync(
            enabled: false);

        Assert.AreEqual(
            1,
            msi.WriteCallCount);

        Assert.AreEqual(
            false,
            msi.LastEnabledValue);
    }

    private static WindowsStartupService CreateService(
        InstallChannel channel,
        FakeStartupController store,
        FakeStartupController msi)
    {
        return new WindowsStartupService(
            new FakeInstallChannelService(channel),
            store,
            msi);
    }

    private sealed class FakeInstallChannelService :
        IInstallChannelService
    {
        private readonly InstallChannel _channel;

        public FakeInstallChannelService(
            InstallChannel channel)
        {
            _channel = channel;
        }

        public Task<InstallationContext> GetCurrentAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new InstallationContext
                {
                    Channel = _channel,
                    Scope =
                        _channel == InstallChannel.None
                            ? InstallScope.None
                            : InstallScope.CurrentUser
                });
        }
    }

    private sealed class FakeStartupController :
        IStartupRegistrationController
    {
        private readonly bool _isEnabled;

        public FakeStartupController(
            bool isEnabled = false)
        {
            _isEnabled = isEnabled;
        }

        public int ReadCallCount { get; private set; }

        public int WriteCallCount { get; private set; }

        public bool? LastEnabledValue { get; private set; }

        public Task<bool> IsEnabledAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadCallCount++;

            return Task.FromResult(
                _isEnabled);
        }

        public Task SetEnabledAsync(
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteCallCount++;
            LastEnabledValue = enabled;

            return Task.CompletedTask;
        }
    }
}
