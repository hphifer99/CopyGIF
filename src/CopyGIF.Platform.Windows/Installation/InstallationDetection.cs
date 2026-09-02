using System.Security;
using Microsoft.Win32;

namespace CopyGIF.Platform.Windows.Installation;

internal static class CopyGifRegistry
{
    public const string ProductSubKey =
        @"Software\CopyGIF";

    public const string InstallChannelValueName =
        "InstallChannel";

    public const string MsiInstallChannelValue =
        "Msi";

    public const string RunSubKey =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string StartupValueName =
        "CopyGIF";
}

internal interface IPackageIdentityDetector
{
    bool HasPackageIdentity();
}

internal interface IRegistryValueReader
{
    object? ReadValue(
        RegistryHive hive,
        string subKey,
        string valueName);
}

internal sealed class WindowsRegistryValueReader :
    IRegistryValueReader
{
    public object? ReadValue(
        RegistryHive hive,
        string subKey,
        string valueName)
    {
        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(
                    hive,
                    RegistryView.Registry64);

            using RegistryKey? key =
                baseKey.OpenSubKey(
                    subKey,
                    writable: false);

            return key?.GetValue(
                valueName,
                defaultValue: null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
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
}
