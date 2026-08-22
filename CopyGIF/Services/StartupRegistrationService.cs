using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace CopyGIF.Services
{
    public sealed class StartupRegistrationService
    {
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string ValueName = "CopyGIF";

        public void Apply(bool startWithWindows)
        {
            using (RegistryKey runKey =
                Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (runKey == null)
                {
                    throw new InvalidOperationException(
                        "The Windows startup registry key could not be opened.");
                }

                if (!startWithWindows)
                {
                    runKey.DeleteValue(ValueName, false);
                    return;
                }

                string command =
                    "\"" + GetExecutablePath() + "\"";

                string existingCommand =
                    runKey.GetValue(ValueName) as string;

                if (!string.Equals(
                        existingCommand,
                        command,
                        StringComparison.OrdinalIgnoreCase))
                {
                    runKey.SetValue(
                        ValueName,
                        command,
                        RegistryValueKind.String);
                }
            }
        }

        private static string GetExecutablePath()
        {
            Assembly entryAssembly =
                Assembly.GetEntryAssembly();

            string executablePath =
                entryAssembly?.Location;

            if (string.IsNullOrWhiteSpace(executablePath) ||
                !File.Exists(executablePath))
            {
                throw new InvalidOperationException(
                    "The CopyGIF executable path could not be determined.");
            }

            return Path.GetFullPath(executablePath);
        }
    }
}
