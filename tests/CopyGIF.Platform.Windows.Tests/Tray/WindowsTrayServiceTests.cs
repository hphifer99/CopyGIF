using CopyGIF.Platform.Windows.Tray;

namespace CopyGIF.Platform.Windows.Tests.Tray;

[TestClass]
public sealed class WindowsTrayServiceTests
{
    [TestMethod]
    public async Task InitializeAsync_CalledTwice_InitializesHostOnce()
    {
        FakeTrayHost host =
            new();

        await using WindowsTrayService service =
            new(host);

        await service.InitializeAsync();
        await service.InitializeAsync();

        Assert.AreEqual(
            1,
            host.InitializeCallCount);
    }

    [TestMethod]
    public async Task HostEvents_AreForwardedByService()
    {
        FakeTrayHost host =
            new();

        await using WindowsTrayService service =
            new(host);

        int openCount = 0;
        int settingsCount = 0;
        int exitCount = 0;

        service.OpenRequested +=
            (_, _) => openCount++;

        service.SettingsRequested +=
            (_, _) => settingsCount++;

        service.ExitRequested +=
            (_, _) => exitCount++;

        host.RaiseOpenRequested();
        host.RaiseSettingsRequested();
        host.RaiseExitRequested();

        Assert.AreEqual(
            1,
            openCount);

        Assert.AreEqual(
            1,
            settingsCount);

        Assert.AreEqual(
            1,
            exitCount);
    }

    [TestMethod]
    public async Task ShowNotificationAsync_InitializesAndTruncatesContent()
    {
        FakeTrayHost host =
            new();

        await using WindowsTrayService service =
            new(host);

        await service.ShowNotificationAsync(
            new string('T', 100),
            new string('M', 300));

        Assert.AreEqual(
            1,
            host.InitializeCallCount);

        Assert.AreEqual(
            63,
            host.LastTitle?.Length);

        Assert.AreEqual(
            255,
            host.LastMessage?.Length);
    }

    [TestMethod]
    public async Task ShowNotificationAsync_BlankMessage_IsRejected()
    {
        FakeTrayHost host =
            new();

        await using WindowsTrayService service =
            new(host);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () =>
                service.ShowNotificationAsync(
                    "CopyGIF",
                    "   "));

        Assert.AreEqual(
            0,
            host.InitializeCallCount);
    }

    private sealed class FakeTrayHost :
        ITrayHost
    {
        public event EventHandler? OpenRequested;

        public event EventHandler? SettingsRequested;

        public event EventHandler? ExitRequested;

        public int InitializeCallCount { get; private set; }

        public string? LastTitle { get; private set; }

        public string? LastMessage { get; private set; }

        public Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            InitializeCallCount++;

            return Task.CompletedTask;
        }

        public Task ShowNotificationAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastTitle = title;
            LastMessage = message;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void RaiseOpenRequested()
        {
            OpenRequested?.Invoke(
                this,
                EventArgs.Empty);
        }

        public void RaiseSettingsRequested()
        {
            SettingsRequested?.Invoke(
                this,
                EventArgs.Empty);
        }

        public void RaiseExitRequested()
        {
            ExitRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}
