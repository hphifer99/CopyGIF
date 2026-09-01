using System.Text.Json;
using System.Xml.Linq;
using CopyGIF.Testing;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class ArchitectureTests
{
    private static readonly string[] ExpectedProductionProjectPaths =
    [
        "src/CopyGIF.App/CopyGIF.App.csproj",
        "src/CopyGIF.Application/CopyGIF.Application.csproj",
        "src/CopyGIF.Core/CopyGIF.Core.csproj",
        "src/CopyGIF.Infrastructure/CopyGIF.Infrastructure.csproj",
        "src/CopyGIF.Platform.Windows/CopyGIF.Platform.Windows.csproj",
        "src/CopyGIF.Presentation/CopyGIF.Presentation.csproj"
    ];

    private static readonly string[] ExpectedTestProjectPaths =
    [
        "tests/CopyGIF.Application.Tests/CopyGIF.Application.Tests.csproj",
        "tests/CopyGIF.Architecture.Tests/CopyGIF.Architecture.Tests.csproj",
        "tests/CopyGIF.Core.Tests/CopyGIF.Core.Tests.csproj",
        "tests/CopyGIF.Infrastructure.Tests/CopyGIF.Infrastructure.Tests.csproj",
        "tests/CopyGIF.Platform.Windows.Tests/CopyGIF.Platform.Windows.Tests.csproj",
        "tests/CopyGIF.Presentation.Tests/CopyGIF.Presentation.Tests.csproj",
        "tests/CopyGIF.Testing/CopyGIF.Testing.csproj"
    ];

    private static readonly IReadOnlyDictionary<string, string[]>
        AllowedProductionReferences =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["CopyGIF.Core"] =
                [],

                ["CopyGIF.Application"] =
                [
                    "CopyGIF.Core"
                ],

                ["CopyGIF.Infrastructure"] =
                [
                    "CopyGIF.Core"
                ],

                ["CopyGIF.Platform.Windows"] =
                [
                    "CopyGIF.Core"
                ],

                ["CopyGIF.Presentation"] =
                [
                    "CopyGIF.Application",
                    "CopyGIF.Core"
                ],

                ["CopyGIF.App"] =
                [
                    "CopyGIF.Application",
                    "CopyGIF.Core",
                    "CopyGIF.Infrastructure",
                    "CopyGIF.Platform.Windows",
                    "CopyGIF.Presentation"
                ]
            };

    private static readonly IReadOnlyDictionary<string, string>
        ExpectedPackageVersions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CommunityToolkit.Mvvm"] = "8.4.2",
                ["Microsoft.Extensions.DependencyInjection"] = "10.0.11",
                ["Microsoft.Extensions.DependencyInjection.Abstractions"] = "10.0.11",
                ["Microsoft.Extensions.Http"] = "10.0.11",
                ["Microsoft.Windows.SDK.BuildTools"] = "10.0.28000.2705",
                ["Microsoft.WindowsAppSDK"] = "2.4.0",
                ["MSTest"] = "4.3.3",
                ["System.Security.Cryptography.ProtectedData"] = "10.0.11"
            };

    [TestMethod]
    public void Repository_UsesFrozenDirectoryAndProjectCasing()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        string[] rootDirectoryNames = Directory
            .EnumerateDirectories(repositoryRoot)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        CollectionAssert.Contains(
            rootDirectoryNames,
            "tests");

        Assert.IsFalse(
            rootDirectoryNames.Contains(
                "Tests",
                StringComparer.Ordinal),
            "The test root must be named exactly 'tests'.");

        string appDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "CopyGIF.App");

        string[] appFileNames = Directory
            .EnumerateFiles(appDirectory)
            .Select(
                path =>
                    Path.GetFileName(path))
            .OfType<string>()
            .ToArray();

        CollectionAssert.Contains(
            appFileNames,
            "CopyGIF.App.csproj");

        Assert.IsFalse(
            appFileNames.Contains(
                "CopyGIF.app.csproj",
                StringComparer.Ordinal),
            "The app project must be named exactly 'CopyGIF.App.csproj'.");
    }

    [TestMethod]
    public void V1Solution_RemainsV1Only()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        XDocument solution = XDocument.Load(
            Path.Combine(repositoryRoot, "CopyGIF.slnx"));

        string[] projectPaths = solution
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "CopyGIF/CopyGIF.csproj"
            },
            projectPaths);
    }

    [TestMethod]
    public void V2Solution_ContainsOnlyExpectedV2Projects()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        XDocument solution = XDocument.Load(
            Path.Combine(repositoryRoot, "CopyGIF.V2.slnx"));

        string[] productionProjects = GetProjectsInSolutionFolder(
            solution,
            "/src/");

        string[] testProjects = GetProjectsInSolutionFolder(
            solution,
            "/tests/");

        CollectionAssert.AreEquivalent(
            ExpectedProductionProjectPaths,
            productionProjects);

        CollectionAssert.AreEquivalent(
            ExpectedTestProjectPaths,
            testProjects);

        string[] allProjects = solution
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();

        string[] allExpectedProjects = ExpectedProductionProjectPaths
            .Concat(ExpectedTestProjectPaths)
            .ToArray();

        CollectionAssert.AreEquivalent(
            allExpectedProjects,
            allProjects);

        Assert.IsFalse(
            allProjects.Any(
                path => path.StartsWith(
                    "CopyGIF/",
                    StringComparison.OrdinalIgnoreCase)),
            "The V2 solution must not reference the V1 project.");
    }

    [TestMethod]
    public void ProductionProjectReferences_FollowFrozenDependencyRules()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        foreach (string relativeProjectPath in ExpectedProductionProjectPaths)
        {
            string projectPath = ToPhysicalPath(
                repositoryRoot,
                relativeProjectPath);

            string projectName = Path.GetFileNameWithoutExtension(
                projectPath);

            Assert.IsTrue(
                AllowedProductionReferences.TryGetValue(
                    projectName,
                    out string[]? expectedReferences),
                $"No dependency rule exists for {projectName}.");

            string[] actualReferences = ReadProjectReferenceNames(
                projectPath);

            CollectionAssert.AreEquivalent(
                expectedReferences!,
                actualReferences,
                $"Unexpected project dependency detected in {projectName}.");
        }
    }

    [TestMethod]
    public void Core_HasNoPackageDependencies()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        string coreProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "CopyGIF.Core",
            "CopyGIF.Core.csproj");

        XDocument project = XDocument.Load(coreProjectPath);

        XElement[] packageReferences = project
            .Descendants()
            .Where(
                element =>
                    element.Name.LocalName == "PackageReference")
            .ToArray();

        Assert.AreEqual(
            0,
            packageReferences.Length,
            "CopyGIF.Core must remain dependency-free.");
    }

    [TestMethod]
    public void V2Projects_UseCentralPackageVersions()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        string[] projectFiles = Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(repositoryRoot, "tests"),
                    "*.csproj",
                    SearchOption.AllDirectories))
            .ToArray();

        foreach (string projectFile in projectFiles)
        {
            XDocument project = XDocument.Load(projectFile);

            foreach (XElement packageReference in project
                         .Descendants()
                         .Where(
                             element =>
                                 element.Name.LocalName ==
                                 "PackageReference"))
            {
                Assert.IsNull(
                    packageReference.Attribute("Version"),
                    $"Package versions must be centralized. File: {projectFile}");

                Assert.IsFalse(
                    packageReference
                        .Elements()
                        .Any(
                            element =>
                                element.Name.LocalName == "Version"),
                    $"Package versions must be centralized. File: {projectFile}");
            }
        }

        XDocument packageFile = XDocument.Load(
            Path.Combine(
                repositoryRoot,
                "Directory.Packages.props"));

        string? centralManagement = GetPropertyValue(
            packageFile,
            "ManagePackageVersionsCentrally");

        Assert.AreEqual(
            "true",
            centralManagement,
            true,
            "Central package management must be enabled.");

        Dictionary<string, string> actualVersions = packageFile
            .Descendants()
            .Where(
                element =>
                    element.Name.LocalName == "PackageVersion")
            .ToDictionary(
                element =>
                    element.Attribute("Include")?.Value
                    ?? string.Empty,
                element =>
                    element.Attribute("Version")?.Value
                    ?? string.Empty,
                StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> expectedPackage
                 in ExpectedPackageVersions)
        {
            Assert.IsTrue(
                actualVersions.TryGetValue(
                    expectedPackage.Key,
                    out string? actualVersion),
                $"Missing central package version for {expectedPackage.Key}.");

            Assert.AreEqual(
                expectedPackage.Value,
                actualVersion,
                $"Unexpected version for {expectedPackage.Key}.");

            Assert.IsFalse(
                actualVersion!.Contains(
                    '-',
                    StringComparison.Ordinal),
                $"Prerelease dependency detected: {expectedPackage.Key}.");
        }
    }

    [TestMethod]
    public void V2BuildPolicy_IsX64Only()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        XDocument buildProperties = XDocument.Load(
            Path.Combine(
                repositoryRoot,
                "Directory.Build.props"));

        Assert.AreEqual(
            "x64",
            GetPropertyValue(
                buildProperties,
                "Platforms"));

        Assert.AreEqual(
            "x64",
            GetPropertyValue(
                buildProperties,
                "PlatformTarget"));

        Assert.AreEqual(
            "false",
            GetPropertyValue(
                buildProperties,
                "Prefer32Bit"),
            true);

        Assert.AreEqual(
            "win-x64",
            GetPropertyValue(
                buildProperties,
                "RuntimeIdentifiers"));

        string[] projectFiles = Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(repositoryRoot, "tests"),
                    "*.csproj",
                    SearchOption.AllDirectories))
            .ToArray();

        foreach (string projectFile in projectFiles)
        {
            string projectContents = File.ReadAllText(projectFile);

            Assert.IsFalse(
                projectContents.Contains(
                    "x86",
                    StringComparison.OrdinalIgnoreCase),
                $"x86 configuration found in {projectFile}.");

            Assert.IsFalse(
                projectContents.Contains(
                    "ARM64",
                    StringComparison.OrdinalIgnoreCase),
                $"ARM64 configuration found in {projectFile}.");
        }

        XDocument solution = XDocument.Load(
            Path.Combine(repositoryRoot, "CopyGIF.V2.slnx"));

        XElement[] solutionPlatforms = solution
            .Descendants()
            .Where(
                element =>
                    element.Name.LocalName == "Platform" &&
                    element.Parent?.Name.LocalName == "Configurations")
            .ToArray();

        Assert.AreEqual(
            1,
            solutionPlatforms.Length,
            "The V2 solution must define exactly one solution platform.");

        Assert.AreEqual(
            "x64",
            solutionPlatforms[0].Attribute("Name")?.Value,
            "The V2 solution platform must be x64.");

        XElement[] projectPlatformMappings = solution
            .Descendants()
            .Where(
                element =>
                    element.Name.LocalName == "Platform" &&
                    element.Parent?.Name.LocalName == "Project")
            .ToArray();

        Assert.AreEqual(
            ExpectedProductionProjectPaths.Length +
            ExpectedTestProjectPaths.Length,
            projectPlatformMappings.Length,
            "Every V2 solution project must have an explicit platform mapping.");

        foreach (XElement platformMapping in projectPlatformMappings)
        {
            Assert.AreEqual(
                "x64",
                platformMapping.Attribute("Project")?.Value,
                "Every V2 project must map to x64.");
        }
    }

    [TestMethod]
    public void V2Projects_UseFrozenTargetFrameworks()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        HashSet<string> windowsProjects =
            new(StringComparer.Ordinal)
            {
                "CopyGIF.App",
                "CopyGIF.Platform.Windows",
                "CopyGIF.Platform.Windows.Tests"
            };

        string[] projectFiles = Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(repositoryRoot, "tests"),
                    "*.csproj",
                    SearchOption.AllDirectories))
            .ToArray();

        foreach (string projectFile in projectFiles)
        {
            string projectName = Path.GetFileNameWithoutExtension(
                projectFile);

            XDocument project = XDocument.Load(projectFile);

            string? actualTargetFramework = GetPropertyValue(
                project,
                "TargetFramework");

            string expectedTargetFramework =
                windowsProjects.Contains(projectName)
                    ? "$(CopyGifWindowsTargetFramework)"
                    : "$(CopyGifNetTargetFramework)";

            Assert.AreEqual(
                expectedTargetFramework,
                actualTargetFramework,
                $"Unexpected target framework in {projectName}.");
        }

        XDocument buildProperties = XDocument.Load(
            Path.Combine(
                repositoryRoot,
                "Directory.Build.props"));

        Assert.AreEqual(
            "net10.0",
            GetPropertyValue(
                buildProperties,
                "CopyGifNetTargetFramework"));

        Assert.AreEqual(
            "10.0.17763.0",
            GetPropertyValue(
                buildProperties,
                "CopyGifWindowsMinimumVersion"));

        Assert.AreEqual(
            "net10.0-windows$(CopyGifWindowsMinimumVersion)",
            GetPropertyValue(
                buildProperties,
                "CopyGifWindowsTargetFramework"));
    }

    [TestMethod]
    public void PackageManifest_RequestsOnlyApprovedCapabilities()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        string manifestPath = Path.Combine(
            repositoryRoot,
            "src",
            "CopyGIF.App",
            "Package.appxmanifest");

        XDocument manifest = XDocument.Load(manifestPath);

        string[] capabilities = manifest
            .Descendants()
            .Where(
                element =>
                    element.Name.LocalName == "Capability")
            .Select(
                element =>
                    element.Attribute("Name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "runFullTrust"
            },
            capabilities);

        string manifestText = File.ReadAllText(manifestPath);

        Assert.IsFalse(
            manifestText.Contains(
                "systemAIModels",
                StringComparison.OrdinalIgnoreCase),
            "CopyGIF must not request the systemAIModels capability.");

        string[] targetDeviceFamilies = manifest
            .Descendants()
            .Where(
                element =>
                    element.Name.LocalName ==
                    "TargetDeviceFamily")
            .Select(
                element =>
                    element.Attribute("Name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "Windows.Desktop"
            },
            targetDeviceFamilies);

        string minimumVersion = manifest
            .Descendants()
            .Single(
                element =>
                    element.Name.LocalName ==
                    "TargetDeviceFamily")
            .Attribute("MinVersion")?.Value
            ?? string.Empty;

        Assert.AreEqual(
            "10.0.17763.0",
            minimumVersion);
    }

    [TestMethod]
    public void GlobalJson_PinsStableDotNet10()
    {
        string repositoryRoot = RepositoryRootLocator.Find();

        string globalJsonPath = Path.Combine(
            repositoryRoot,
            "global.json");

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(globalJsonPath));

        JsonElement sdk = document
            .RootElement
            .GetProperty("sdk");

        Assert.AreEqual(
            "10.0.400",
            sdk.GetProperty("version").GetString());

        Assert.AreEqual(
            "latestFeature",
            sdk.GetProperty("rollForward").GetString());

        Assert.IsFalse(
            sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    private static string[] GetProjectsInSolutionFolder(
        XDocument solution,
        string folderName)
    {
        XElement? folder = solution
            .Descendants()
            .SingleOrDefault(
                element =>
                    element.Name.LocalName == "Folder" &&
                    string.Equals(
                        element.Attribute("Name")?.Value,
                        folderName,
                        StringComparison.Ordinal));

        if (folder is null)
        {
            Assert.Fail(
                $"Solution folder '{folderName}' was not found.");

            return [];
        }

        return folder
            .Elements()
            .Where(
                element =>
                    element.Name.LocalName == "Project")
            .Select(
                element =>
                    element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }

    private static string[] ReadProjectReferenceNames(
        string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);

        string projectDirectory =
            Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException(
                "Project directory could not be determined.");

        List<string> references = [];

        foreach (XElement projectReference in project
                     .Descendants()
                     .Where(
                         element =>
                             element.Name.LocalName ==
                             "ProjectReference"))
        {
            string? include = projectReference
                .Attribute("Include")
                ?.Value;

            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            string referencedProjectPath = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    include));

            references.Add(
                Path.GetFileNameWithoutExtension(
                    referencedProjectPath));
        }

        return references
            .OrderBy(
                reference =>
                    reference,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static string? GetPropertyValue(
        XDocument document,
        string propertyName)
    {
        return document
            .Descendants()
            .FirstOrDefault(
                element =>
                    element.Name.LocalName == propertyName)
            ?.Value
            .Trim();
    }

    private static string ToPhysicalPath(
        string repositoryRoot,
        string relativePath)
    {
        string[] segments = relativePath.Split('/');

        return Path.Combine(
            new[]
            {
                repositoryRoot
            }
            .Concat(segments)
            .ToArray());
    }
}
