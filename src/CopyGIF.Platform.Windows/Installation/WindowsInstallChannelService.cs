using System.ComponentModel;
using System.Runtime.InteropServices;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using Microsoft.Win32;

namespace CopyGIF.Platform.Windows.Installation;

public sealed class WindowsInstallChannelService :
    IInstallChannelService
{
    private readonly IPackageIdentityDetector
        _packageIdentityDetector;

    private readonly IRegistryValueReader
        _registryValueReader;

    public WindowsInstallChannelService()
        : this(
            new WindowsPackageIdentityDetector(),
            new WindowsRegistryValueReader())
    {
    }

    internal WindowsInstallChannelService(
        IPackageIdentityDetector packageIdentityDetector,
        IRegistryValueReader registryValueReader)
    {
        _packageIdentityDetector =
            packageIdentityDetector ??
            throw new ArgumentNullException(
                nameof(packageIdentityDetector));

        _registryValueReader =
            registryValueReader ??
            throw new ArgumentNullException(
                nameof(registryValueReader));
    }

    public Task<InstallationContext> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_packageIdentityDetector
            .HasPackageIdentity())
        {
            return Task.FromResult(
                new InstallationContext
                {
                    Channel =
                        _packageIdentityDetector.GetPackageChannel(),
                    Scope =
                        InstallScope.CurrentUser
                });
        }

        if (TryGetMsiChannel(
                RegistryHive.LocalMachine,
                out InstallChannel machineChannel))
        {
            return Task.FromResult(
                new InstallationContext
                {
                    Channel =
                        machineChannel,
                    Scope =
                        InstallScope.AllUsers
                });
        }

        if (TryGetMsiChannel(
                RegistryHive.CurrentUser,
                out InstallChannel userChannel))
        {
            return Task.FromResult(
                new InstallationContext
                {
                    Channel =
                        userChannel,
                    Scope =
                        InstallScope.CurrentUser
                });
        }

        return Task.FromResult(
            new InstallationContext
            {
                Channel = InstallChannel.None,
                Scope = InstallScope.None
            });
    }

    private bool TryGetMsiChannel(
        RegistryHive hive,
        out InstallChannel channel)
    {
        object? value =
            _registryValueReader.ReadValue(
                hive,
                CopyGifRegistry.ProductSubKey,
                CopyGifRegistry.InstallChannelValueName);

        channel = (value as string) switch
        {
            string text when string.Equals(
                text,
                CopyGifRegistry.MsiInstallChannelValue,
                StringComparison.OrdinalIgnoreCase) => InstallChannel.Msi,
            string text when string.Equals(
                text,
                CopyGifRegistry.UnsignedMsiInstallChannelValue,
                StringComparison.OrdinalIgnoreCase) => InstallChannel.UnsignedMsi,
            _ => InstallChannel.None
        };

        if (channel == InstallChannel.None)
        {
            return false;
        }

        // A machine marker must not classify an unrelated unpackaged debug/portable build as installed.
        string? installedDirectory = _registryValueReader.ReadValue(hive,
            CopyGifRegistry.ProductSubKey, "InstallDirectory") as string;
        if (string.IsNullOrWhiteSpace(installedDirectory) || string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            channel = InstallChannel.None;
            return false;
        }
        try
        {
            bool matches = string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(installedDirectory)),
                Path.GetDirectoryName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                channel = InstallChannel.None;
            }

            return matches;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            channel = InstallChannel.None;
            return false;
        }
    }
}

internal sealed class WindowsPackageIdentityDetector :
    IPackageIdentityDetector
{
    private const int Success = 0;
    private const int InsufficientBuffer = 122;
    private const int NoPackageIdentity = 15700;

    public InstallChannel GetPackageChannel()
    {
        if (!HasPackageIdentity()) return InstallChannel.None;
        var package = global::Windows.ApplicationModel.Package.Current;
        if (package.IsDevelopmentMode) return InstallChannel.DevelopmentPackage;
        return package.SignatureKind == global::Windows.ApplicationModel.PackageSignatureKind.Store
            ? InstallChannel.MicrosoftStore : InstallChannel.SideloadedPackage;
    }

    public bool HasPackageIdentity()
    {
        uint packageNameLength = 0;

        int result =
            GetCurrentPackageFullName(
                ref packageNameLength,
                nint.Zero);

        return result switch
        {
            Success => true,
            InsufficientBuffer => true,
            NoPackageIdentity => false,
            _ => throw new Win32Exception(
                result,
                "Windows could not determine the CopyGIF package identity.")
        };
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength,
        nint packageFullName);
}
