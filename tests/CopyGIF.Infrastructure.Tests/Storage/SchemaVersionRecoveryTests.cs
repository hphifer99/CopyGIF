using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class SchemaVersionRecoveryTests
{
    [TestMethod]
    [DataRow("\"2\"")]
    [DataRow("null")]
    [DataRow("true")]
    [DataRow("{}")]
    [DataRow("[]")]
    public async Task WrongSchemaVersionType_UsesBackupAndPreservesInvalidPrimary(string value)
    {
        string root = Path.Combine(Path.GetTempPath(), "CopyGIF.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            ApplicationPaths paths = new(root);
            JsonSettingsStore store = new(paths);
            await store.SaveAsync(new AppSettings { Hotkey = "Ctrl+Shift+G" });
            await store.SaveAsync(new AppSettings { Hotkey = "Ctrl+Alt+G" });

            string invalidJson = "{\"schemaVersion\":" + value + "}";
            await File.WriteAllTextAsync(paths.SettingsPath, invalidJson);

            AppSettings recovered = await store.LoadAsync();
            Assert.AreEqual("Ctrl+Shift+G", recovered.Hotkey);

            string[] preserved = Directory.GetFiles(root, "*.corrupt*", SearchOption.AllDirectories);
            Assert.AreEqual(1, preserved.Length);
            Assert.AreEqual(invalidJson, await File.ReadAllTextAsync(preserved[0]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
