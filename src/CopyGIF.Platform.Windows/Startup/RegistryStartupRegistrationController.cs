using System.Security;
using CopyGIF.Platform.Windows.Installation;
using Microsoft.Win32;

namespace CopyGIF.Platform.Windows.Startup;

internal interface IRegistryStartupStore
{
    string? ReadCommand();

    void WriteCommand(string command);

    void DeleteCommand();
}

internal sealed class WindowsRegistryStartupStore :
    IRegistryStartupStore
{
    public string? ReadCommand()
    {
        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.CurrentUser,
                    RegistryView.Registry64);

            using RegistryKey? key =
                baseKey.OpenSubKey(
                    CopyGifRegistry.RunSubKey,
                    writable: false);

            return key?.GetValue(
                    CopyGifRegistry.StartupValueName,
                    defaultValue: null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames)
                as string;
        }
        catch (Exception exception)
            when (exception is
                UnauthorizedAccessException or
                SecurityException or
                IOException)
        {
            return null;
        }
    }

    public void WriteCommand(
        string command)
    {
        using RegistryKey baseKey =
            RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Registry64);

        using RegistryKey key =
            baseKey.CreateSubKey(
                CopyGifRegistry.RunSubKey,
                writable: true);

        key.SetValue(
            CopyGifRegistry.StartupValueName,
            command,
            RegistryValueKind.String);
    }

    public void DeleteCommand()
    {
        using RegistryKey baseKey =
            RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Registry64);

        using RegistryKey? key =
            baseKey.OpenSubKey(
                CopyGifRegistry.RunSubKey,
                writable: true);

        key?.DeleteValue(
            CopyGifRegistry.StartupValueName,
            throwOnMissingValue: false);
    }
}

internal sealed class RegistryStartupRegistrationController :
    IStartupRegistrationController
{
    private readonly IRegistryStartupStore _store;

    private readonly Func<string?>
        _processPathAccessor;

    public RegistryStartupRegistrationController()
        : this(
            new WindowsRegistryStartupStore(),
            static () => Environment.ProcessPath)
    {
    }

    internal RegistryStartupRegistrationController(
        IRegistryStartupStore store,
        Func<string?> processPathAccessor)
    {
        _store =
            store ??
            throw new ArgumentNullException(
                nameof(store));

        _processPathAccessor =
            processPathAccessor ??
            throw new ArgumentNullException(
                nameof(processPathAccessor));
    }

    public Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string expectedCommand =
            CreateStartupCommand();

        string? actualCommand =
            _store.ReadCommand();

        return Task.FromResult(
            string.Equals(
                expectedCommand,
                actualCommand,
                StringComparison.OrdinalIgnoreCase));
    }

    public Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (enabled)
        {
            _store.WriteCommand(
                CreateStartupCommand());
        }
        else
        {
            _store.DeleteCommand();
        }

        return Task.CompletedTask;
    }

    internal string CreateStartupCommand()
    {
        string? processPath =
            _processPathAccessor();

        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException(
                "Windows did not provide the CopyGIF executable path.");
        }

        string fullPath =
            Path.GetFullPath(processPath);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The CopyGIF startup target must be a Windows executable.");
        }

        if (fullPath.Contains('"'))
        {
            throw new InvalidOperationException(
                "The CopyGIF executable path contains an unsupported character.");
        }

        return
            $"\"{fullPath}\" --startup";
    }
}
