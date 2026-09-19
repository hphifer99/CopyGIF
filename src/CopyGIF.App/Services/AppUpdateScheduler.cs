using CopyGIF.Application.Settings;
using CopyGIF.Application.Updates;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Updates;

namespace CopyGIF.App.Services;

public sealed class AppUpdateScheduler : IAsyncDisposable
{
    private readonly IUpdateCoordinator _updates;
    private readonly EffectiveSettings _effective;
    private readonly ITrayService _tray;
    private readonly WinUiDispatcher _dispatcher;
    private readonly UpdateViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _sync = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private CancellationTokenSource? _current;
    private Task? _loop;
    private DateTimeOffset _retryAfter;

    public AppUpdateScheduler(IUpdateCoordinator updates, EffectiveSettings effective, ITrayService tray,
        WinUiDispatcher dispatcher, UpdateViewModel viewModel)
    {
        _updates = updates; _effective = effective; _tray = tray;
        _dispatcher = dispatcher; _viewModel = viewModel;
        effective.Changed += SettingsChanged;
    }

    public void Start(string version)
    {
        if (_loop is not null) return;
        _viewModel.Initialize(version);
        _loop = Task.Run(() => RunAsync(version));
    }

    private void SettingsChanged(object? sender, EventArgs args)
    {
        lock (_sync)
        {
            _current?.Cancel();
            if (_wake.CurrentCount == 0) _wake.Release();
        }
    }

    private async Task RunAsync(string version)
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                TimeSpan nextCheck = TimeSpan.FromMinutes(15);
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                lock (_sync) _current = operation;
                try
                {
                    if (!_effective.SuppressAutomaticUpdates && !_viewModel.IsBusy && DateTimeOffset.UtcNow >= _retryAfter)
                    {
                        AutomaticUpdateResult result = await _updates.RunAutomaticAsync(version, cancellationToken: operation.Token);
                        if (result.Check.Status is UpdateCheckStatus.FeedUnavailable or UpdateCheckStatus.UnsupportedInstallation)
                            _retryAfter = DateTimeOffset.UtcNow.AddHours(1);
                        if (result.Check.Status is UpdateCheckStatus.ManagedByStore or
                            UpdateCheckStatus.UnsupportedInstallation or UpdateCheckStatus.Disabled)
                            nextCheck = TimeSpan.FromDays(1);
                        else if (result.Check.Status == UpdateCheckStatus.FeedUnavailable)
                            nextCheck = TimeSpan.FromHours(1);
                        if (result.Check.Status != UpdateCheckStatus.NotDue)
                            await _dispatcher.InvokeAsync(() => _viewModel.AcceptAutomaticResult(result));
                        if (result.Action != AutomaticUpdateAction.None)
                            await _tray.ShowNotificationAsync("CopyGIF update",
                                result.Action == AutomaticUpdateAction.Installed ? "The verified update installer has started." :
                                result.Action == AutomaticUpdateAction.VerificationFailed ? "Update verification failed. Open Settings for details." :
                                "An update is available. Open Settings > Updates for details.", _lifetime.Token);
                    }
                }
                catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    _retryAfter = DateTimeOffset.UtcNow.AddHours(1);
                    nextCheck = TimeSpan.FromHours(1);
                    RepairDiagnostics.Record("automatic-update", "github", exception.GetType().Name);
                }
                finally { lock (_sync) if (ReferenceEquals(_current, operation)) _current = null; }
                await _wake.WaitAsync(nextCheck, _lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _effective.Changed -= SettingsChanged;
        await _lifetime.CancelAsync();
        if (_loop is not null) await _loop;
        _wake.Dispose();
        _lifetime.Dispose();
    }
}
