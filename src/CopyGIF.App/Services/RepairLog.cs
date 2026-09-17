using CopyGIF.Core.Models;

namespace CopyGIF.App.Services;

internal static class RepairLog
{
    private static readonly object Gate = new();
    public static void Configure(string directory)
    {
        RepairDiagnostics.Sink = message =>
        {
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "repair.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 1024 * 1024)
                    File.Move(path, Path.Combine(directory, "repair.previous.log"), overwrite: true);
                File.AppendAllText(path, message + Environment.NewLine);
            }
        };
    }
}
