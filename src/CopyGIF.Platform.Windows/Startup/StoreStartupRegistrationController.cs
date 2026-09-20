using Windows.ApplicationModel;

namespace CopyGIF.Platform.Windows.Startup;

internal sealed class StoreStartupRegistrationController :
    IStartupRegistrationController
{
    internal const string StartupTaskId =
        "CopyGIFStartup";

    public async Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StartupTask task =
            await StartupTask.GetAsync(
                StartupTaskId);

        cancellationToken.ThrowIfCancellationRequested();

        return task.State is
            StartupTaskState.Enabled or
            StartupTaskState.EnabledByPolicy;
    }

    public async Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StartupTask task =
            await StartupTask.GetAsync(
                StartupTaskId);

        cancellationToken.ThrowIfCancellationRequested();

        if (!enabled)
        {
            if (task.State ==
                StartupTaskState.EnabledByPolicy)
            {
                throw new InvalidOperationException(
                    "Start with Windows is controlled by organization policy.");
            }

            task.Disable();

            return;
        }

        if (task.State ==
            StartupTaskState.DisabledByPolicy)
        {
            throw new InvalidOperationException(
                "Start with Windows is disabled by organization policy.");
        }

        if (task.State is
            StartupTaskState.Enabled or
            StartupTaskState.EnabledByPolicy)
        {
            return;
        }

        StartupTaskState result =
            await task.RequestEnableAsync();

        cancellationToken.ThrowIfCancellationRequested();

        if (result is not
            StartupTaskState.Enabled and not
            StartupTaskState.EnabledByPolicy)
        {
            throw new InvalidOperationException(
                "Windows did not enable Start with Windows for CopyGIF.");
        }
    }
}
