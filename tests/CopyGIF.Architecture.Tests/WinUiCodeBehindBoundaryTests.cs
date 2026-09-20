using CopyGIF.Testing;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class WinUiCodeBehindBoundaryTests
{
    private static readonly string[] ForbiddenLayerDependencies =
    [
        "using CopyGIF.Application",
        "using CopyGIF.Core",
        "using CopyGIF.Infrastructure",
        "using CopyGIF.Platform.Windows",
        "using CopyGIF.Presentation",
        "using Microsoft.Extensions.DependencyInjection"
    ];

    private static readonly string[] ForbiddenBusinessOperations =
    [
        "IServiceProvider",
        "GetRequiredService",
        "HttpClient",
        "HttpRequestMessage",
        "HttpResponseMessage",
        "WebRequest",
        "JsonSerializer",
        "Registry.",
        "File.",
        "Directory.",
        "FileStream",
        "Process.Start",
        ".SaveAsync(",
        ".LoadAsync(",
        ".SearchAsync(",
        ".CopyAsync(",
        ".DownloadAsync(",
        ".InstallAsync(",
        ".Migrate",
        "Coordinator",
        "Repository"
    ];

    [TestMethod]
    public void PageAndControlCodeBehind_DoesNotReferenceBusinessLayers()
    {
        foreach (string codeBehindFile
                 in GetViewCodeBehindFiles())
        {
            string source =
                File.ReadAllText(
                    codeBehindFile);

            foreach (string forbiddenDependency
                     in ForbiddenLayerDependencies)
            {
                Assert.IsFalse(
                    source.Contains(
                        forbiddenDependency,
                        StringComparison.Ordinal),
                    $"WinUI code-behind references a forbidden business layer " +
                    $"through '{forbiddenDependency}' in {codeBehindFile}.");
            }
        }
    }

    [TestMethod]
    public void PageAndControlCodeBehind_DoesNotPerformBusinessOperations()
    {
        foreach (string codeBehindFile
                 in GetViewCodeBehindFiles())
        {
            string source =
                File.ReadAllText(
                    codeBehindFile);

            foreach (string forbiddenOperation
                     in ForbiddenBusinessOperations)
            {
                Assert.IsFalse(
                    source.Contains(
                        forbiddenOperation,
                        StringComparison.Ordinal),
                    $"WinUI code-behind contains forbidden business-operation " +
                    $"text '{forbiddenOperation}' in {codeBehindFile}.");
            }
        }
    }

    [TestMethod]
    public void PageAndControlCodeBehind_DoesNotConstructViewModels()
    {
        foreach (string codeBehindFile
                 in GetViewCodeBehindFiles())
        {
            string source =
                File.ReadAllText(
                    codeBehindFile);

            Assert.IsFalse(
                source.Contains(
                    "ViewModel(",
                    StringComparison.Ordinal),
                $"WinUI code-behind must receive bindable state rather than construct a view model: {codeBehindFile}");

            Assert.IsFalse(
                source.Contains(
                    "new ViewModel",
                    StringComparison.Ordinal),
                $"WinUI code-behind must not construct view models: {codeBehindFile}");
        }
    }

    [TestMethod]
    public void EveryViewCodeBehindFile_HasAMatchingXamlFile()
    {
        foreach (string codeBehindFile
                 in GetViewCodeBehindFiles())
        {
            string xamlFile =
                codeBehindFile[..^3];

            Assert.IsTrue(
                File.Exists(
                    xamlFile),
                $"The WinUI code-behind file has no matching XAML file: {codeBehindFile}");
        }
    }

    [TestMethod]
    public void BusinessComposition_RemainsInTheDedicatedAppCompositionRoot()
    {
        string appDirectory =
            GetAppDirectory();

        string hostPath =
            Path.Combine(
                appDirectory,
                "Composition",
                "CopyGifHost.cs");

        string hostSource =
            File.ReadAllText(
                hostPath);

        string[] requiredCompositionText =
        [
            "AddCopyGifInfrastructure()",
            "AddCopyGifWindowsPlatform()",
            "AddCopyGifApplication()",
            "AddCopyGifPresentation()",
            "IApplicationStartupCoordinator",
            "ISettingsCoordinator",
            "IGifCopyCoordinator"
        ];

        foreach (string requiredText
                 in requiredCompositionText)
        {
            StringAssert.Contains(
                hostSource,
                requiredText,
                $"The dedicated composition root is missing required text: {requiredText}");
        }
    }

    private static string[] GetViewCodeBehindFiles()
    {
        string appDirectory =
            GetAppDirectory();

        string[] viewDirectories =
        [
            Path.Combine(
                appDirectory,
                "Controls"),
            Path.Combine(
                appDirectory,
                "Views")
        ];

        string[] codeBehindFiles =
            viewDirectories
                .SelectMany(
                    directory =>
                        Directory.EnumerateFiles(
                            directory,
                            "*.xaml.cs",
                            SearchOption.AllDirectories))
                .Append(
                    Path.Combine(
                        appDirectory,
                        "StartupFailureWindow.xaml.cs"))
                .OrderBy(
                    path =>
                        path,
                    StringComparer.Ordinal)
                .ToArray();

        Assert.AreEqual(
            10,
            codeBehindFiles.Length,
            "The final shell must contain exactly ten view code-behind files.");

        return codeBehindFiles;
    }

    private static string GetAppDirectory()
    {
        return Path.Combine(
            RepositoryRootLocator.Find(),
            "src",
            "CopyGIF.App");
    }
}
