using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Startup;

public interface IApplicationStartupCoordinator
{
    event EventHandler<ActivationRequestedEventArgs>?
        ActivationRequested;

    event EventHandler? HotkeyActivated;

    event EventHandler? OpenRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    Task<ApplicationStartupResult> InitializeAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public enum ApplicationStartupStatus
{
    Ready,
    RedirectedToPrimary,
    MigrationFailed,
    HotkeyRejected,

    /// <summary>
    /// A deferred update was handed to the installer. CopyGIF must close at once; the
    /// installer starts the updated CopyGIF when it finishes.
    /// </summary>
    UpdateInstallStarted
}

public sealed record ApplicationStartupResult
{
    public required ApplicationStartupStatus Status { get; init; }

    public required SingleInstanceResult SingleInstance { get; init; }

    public MigrationResult? Migration { get; init; }

    public AppSettings? Settings { get; init; }

    public OnboardingState? Onboarding { get; init; }

    public HotkeyRegistrationFailure HotkeyFailure { get; init; }

    public string? Message { get; init; }

    public bool IsReady =>
        Status == ApplicationStartupStatus.Ready;

    public bool ShouldExit =>
        Status ==
            ApplicationStartupStatus.RedirectedToPrimary ||
        Status ==
            ApplicationStartupStatus.UpdateInstallStarted;
}
