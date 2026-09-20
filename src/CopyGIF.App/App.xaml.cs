using CopyGIF.Application.Startup;
using CopyGIF.App.Composition;
using CopyGIF.Core.Models;
using Microsoft.UI.Xaml;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace CopyGIF.App;

public partial class App :
    XamlApplication
{
    private CopyGifHost? _host;

    private Window? _startupFailureWindow;

    public App()
    {
        InitializeComponent();

        RegisterGlobalExceptionLogging();
    }

    /// <summary>
    /// Leaves a trace in the local repair log for failures that would otherwise vanish or
    /// crash the process silently. None of these handlers mark the exception as handled, so
    /// the default behavior (including a crash for a truly unhandled exception) is unchanged.
    /// Only the exception type and throwing method are written, never the message.
    /// </summary>
    private void RegisterGlobalExceptionLogging()
    {
        // The event argument types are deliberately not named because Microsoft.UI.Xaml and
        // System both define an UnhandledExceptionEventArgs and the lambdas avoid the ambiguity.
        UnhandledException +=
            (_, arguments) =>
                RecordGlobalException(
                    "unhandled-ui-exception",
                    arguments.Exception);

        AppDomain.CurrentDomain.UnhandledException +=
            (_, arguments) =>
                RecordGlobalException(
                    arguments.IsTerminating
                        ? "unhandled-fatal-exception"
                        : "unhandled-exception",
                    arguments.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException +=
            (_, arguments) =>
                RecordGlobalException(
                    "unobserved-task-exception",
                    arguments.Exception);
    }

    private static void RecordGlobalException(
        string stage,
        Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            RepairDiagnostics.RecordException(
                stage,
                exception);
        }
        catch
        {
            // Diagnostics must never turn one failure into a second one.
        }
    }

    public IServiceProvider Services =>
        _host?.Services ??
        throw new InvalidOperationException(
            "CopyGIF services are not available before application launch.");

    protected override async void OnLaunched(
        LaunchActivatedEventArgs args)
    {
        _ = args;

        try
        {
            _host ??=
                CopyGifHost.Create();

            _host.WindowManager.ShutdownAsync = async () =>
            {
                await DisposeHostAsync();
                Exit();
            };

            string[] commandLine =
                Environment.GetCommandLineArgs();

            IReadOnlyList<string> arguments =
                commandLine.Length > 1
                    ? commandLine[1..]
                    : [];

            ApplicationStartupResult result =
                await _host
                    .StartAsync(
                        arguments)
                    .ConfigureAwait(true);

            switch (result.Status)
            {
                case ApplicationStartupStatus.Ready:
                    return;

                case ApplicationStartupStatus.RedirectedToPrimary:
                case ApplicationStartupStatus.UpdateInstallStarted:
                    // The second case: a deferred update was handed to the installer and
                    // CopyGIF must close at once so the installer can replace its files. The
                    // installer starts the updated CopyGIF when it finishes.
                    await DisposeHostAsync()
                        .ConfigureAwait(true);

                    Exit();

                    return;

                case ApplicationStartupStatus.MigrationFailed:
                    ShowStartupFailure(
                        result.Message ??
                        "CopyGIF could not safely migrate its saved data.");

                    return;

                case ApplicationStartupStatus.HotkeyRejected:
                    ShowStartupFailure(
                        result.Message ??
                        "CopyGIF could not register its configured global hotkey.");

                    return;

                default:
                    ShowStartupFailure(
                        "CopyGIF startup returned an unsupported status.");

                    return;
            }
        }
        catch (Exception exception)
        {
            RepairDiagnostics.Record("startup", "local", exception.GetType().Name);
            ShowStartupFailure(exception switch
            {
                IOException or UnauthorizedAccessException =>
                    "CopyGIF could not read its saved data. Check access to the app data folder and try again.",
                InvalidDataException =>
                    "CopyGIF found saved data it could not safely load. Check the local repair log before changing files.",
                _ => "CopyGIF could not finish startup. Check the local repair log for the failing stage."
            });
        }
    }

    private void ShowStartupFailure(
        string message)
    {
        if (_startupFailureWindow is not null)
        {
            return;
        }

        StartupFailureWindow window =
            new(
                string.IsNullOrWhiteSpace(
                    message)
                    ? "CopyGIF could not start safely."
                    : message);

        _startupFailureWindow =
            window;

        window.Closed +=
            HandleStartupFailureClosed;

        window.Activate();
    }

    private async void HandleStartupFailureClosed(
        object sender,
        WindowEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        if (_startupFailureWindow is not null)
        {
            _startupFailureWindow.Closed -=
                HandleStartupFailureClosed;

            _startupFailureWindow =
                null;
        }

        await DisposeHostAsync()
            .ConfigureAwait(true);

        Exit();
    }

    private async ValueTask DisposeHostAsync()
    {
        CopyGifHost? host =
            _host;

        if (host is null)
        {
            return;
        }

        _host =
            null;

        await host
            .DisposeAsync()
            .ConfigureAwait(true);
    }
}
