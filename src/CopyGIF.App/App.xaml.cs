using CopyGIF.Application.Startup;
using CopyGIF.App.Composition;
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
        catch (Exception)
        {
            ShowStartupFailure(
                "CopyGIF could not complete its safe startup checks.");
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
