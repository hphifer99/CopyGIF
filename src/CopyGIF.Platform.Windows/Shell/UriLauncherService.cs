using System.ComponentModel;
using System.Diagnostics;
using CopyGIF.Core.Contracts;

namespace CopyGIF.Platform.Windows.Shell;

internal interface IUriLaunchNativeApi
{
    bool TryLaunch(Uri uri);
}

internal sealed class ShellUriLaunchNativeApi :
    IUriLaunchNativeApi
{
    public static ShellUriLaunchNativeApi Instance { get; } =
        new();

    private ShellUriLaunchNativeApi()
    {
    }

    public bool TryLaunch(
        Uri uri)
    {
        ProcessStartInfo startInfo =
            new()
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };

        using Process? process =
            Process.Start(startInfo);

        return true;
    }
}

public sealed class UriLauncherService :
    IUriLauncherService
{
    private readonly IUriLaunchNativeApi
        _nativeApi;

    public UriLauncherService()
        : this(
            ShellUriLaunchNativeApi.Instance)
    {
    }

    internal UriLauncherService(
        IUriLaunchNativeApi nativeApi)
    {
        _nativeApi =
            nativeApi ??
            throw new ArgumentNullException(
                nameof(nativeApi));
    }

    public Task<bool> TryLaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();

        if (!uri.IsAbsoluteUri ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return Task.FromResult(false);
        }

        try
        {
            return Task.FromResult(
                _nativeApi.TryLaunch(uri));
        }
        catch (Win32Exception)
        {
            return Task.FromResult(false);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult(false);
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }
}
