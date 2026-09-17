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

        if (HasMsiMarker(
                RegistryHive.LocalMachine))
        {
            return Task.FromResult(
                new InstallationContext
                {
                    Channel =
                        InstallChannel.Msi,
                    Scope =
                        InstallScope.AllUsers
                });
        }

        if (HasMsiMarker(
                RegistryHive.CurrentUser))
        {
            return Task.FromResult(
                new InstallationContext
                {
                    Channel =
                        InstallChannel.Msi,
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

    private bool HasMsiMarker(
        RegistryHive hive)
    {
        object? value =
            _registryValueReader.ReadValue(
                hive,
                CopyGifRegistry.ProductSubKey,
                CopyGifRegistry.InstallChannelValueName);

        if (!string.Equals(value as string, CopyGifRegistry.MsiInstallChannelValue, StringComparison.OrdinalIgnoreCase)) return false;
        // A machine marker must not classify an unrelated unpackaged debug/portable build as installed.
        string? installedDirectory = _registryValueReader.ReadValue(hive,
            CopyGifRegistry.ProductSubKey, "InstallDirectory") as string;
        if (string.IsNullOrWhiteSpace(installedDirectory) || string.IsNullOrWhiteSpace(Environment.ProcessPath)) return false;
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(installedDirectory)),
                Path.GetDirectoryName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
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
