using CopyGIF.Platform.Windows.Startup;

namespace CopyGIF.Platform.Windows.Tests.Startup;

[TestClass]
public sealed class RegistryStartupRegistrationControllerTests
{
    private const string ExecutablePath =
        @"C:\Program Files\CopyGIF\CopyGIF.exe";

    [TestMethod]
    public async Task SetEnabledAsync_True_WritesQuotedCommand()
    {
        FakeRegistryStartupStore store =
            new();

        RegistryStartupRegistrationController controller =
            CreateController(store);

        await controller.SetEnabledAsync(
            enabled: true);

        Assert.AreEqual(
            "\"C:\\Program Files\\CopyGIF\\CopyGIF.exe\" --startup",
            store.Command);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ExactCommand_ReturnsTrue()
    {
        FakeRegistryStartupStore store =
            new()
            {
                Command =
                    "\"C:\\Program Files\\CopyGIF\\CopyGIF.exe\" --startup"
            };

        RegistryStartupRegistrationController controller =
            CreateController(store);

        bool result =
            await controller.IsEnabledAsync();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ChangedCommand_ReturnsFalse()
    {
        FakeRegistryStartupStore store =
            new()
            {
                Command =
                    "\"C:\\Other\\Other.exe\" --startup"
            };

        RegistryStartupRegistrationController controller =
            CreateController(store);

        bool result =
            await controller.IsEnabledAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SetEnabledAsync_False_DeletesCommand()
    {
        FakeRegistryStartupStore store =
            new()
            {
                Command = "existing"
            };

        RegistryStartupRegistrationController controller =
            CreateController(store);

        await controller.SetEnabledAsync(
            enabled: false);

        Assert.IsNull(
            store.Command);

        Assert.AreEqual(
            1,
            store.DeleteCallCount);
    }

    private static RegistryStartupRegistrationController
        CreateController(
            FakeRegistryStartupStore store)
    {
        return new RegistryStartupRegistrationController(
            store,
            static () => ExecutablePath);
    }

    private sealed class FakeRegistryStartupStore :
        IRegistryStartupStore
    {
        public string? Command { get; set; }

        public int DeleteCallCount { get; private set; }

        public string? ReadCommand()
        {
            return Command;
        }

        public void WriteCommand(
            string command)
        {
            Command = command;
        }

        public void DeleteCommand()
        {
            Command = null;
            DeleteCallCount++;
        }
    }
}
