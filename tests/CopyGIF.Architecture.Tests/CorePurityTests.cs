using CopyGIF.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class CorePurityTests
{
    [TestMethod]
    public void CoreSource_ContainsNoPlatformOrImplementationApis()
    {
        string repositoryRoot =
            RepositoryRootLocator.Find();

        string coreDirectory =
            Path.Combine(
                repositoryRoot,
                "src",
                "CopyGIF.Core");

        string[] sourceFiles =
            Directory.EnumerateFiles(
                    coreDirectory,
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(
                    path =>
                        !IsBuildArtifactPath(
                            path))
                .ToArray();

        string[] forbiddenPatterns =
        [
            "using Microsoft.UI",
            "using Windows.",
            "using Microsoft.Extensions",
            "using System.Net.Http",
            "HttpClient",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "Registry.",
            "DllImport",
            "Marshal.",
            "File.",
            "Directory.",
            "FileStream",
            "Path."
        ];

        foreach (string sourceFile
                 in sourceFiles)
        {
            string contents =
                File.ReadAllText(
                    sourceFile);

            foreach (string forbiddenPattern
                     in forbiddenPatterns)
            {
                Assert.IsFalse(
                    contents.Contains(
                        forbiddenPattern,
                        StringComparison.Ordinal),
                    $"Core source contains forbidden implementation API " +
                    $"'{forbiddenPattern}' in {sourceFile}.");
            }
        }
    }

    [TestMethod]
    public void AppSettings_ContainsNoSecretsOrRuntimeState()
    {
        string repositoryRoot =
            RepositoryRootLocator.Find();

        string settingsPath =
            Path.Combine(
                repositoryRoot,
                "src",
                "CopyGIF.Core",
                "Settings",
                "AppSettings.cs");

        string contents =
            File.ReadAllText(
                settingsPath);

        string[] forbiddenNames =
        [
            "ApiKey",
            "Password",
            "Credential",
            "Secret",
            "LastUpdateCheck",
            "MigrationCompleted",
            "DownloadedFile",
            "LibraryEntry",
            "SearchHistoryEntry"
        ];

        foreach (string forbiddenName
                 in forbiddenNames)
        {
            Assert.IsFalse(
                contents.Contains(
                    forbiddenName,
                    StringComparison.OrdinalIgnoreCase),
                $"AppSettings contains forbidden state: {forbiddenName}.");
        }
    }

    private static bool IsBuildArtifactPath(
        string path)
    {
        string[] segments =
            path.Split(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        return segments.Any(
            segment =>
                string.Equals(
                    segment,
                    "bin",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    segment,
                    "obj",
                    StringComparison.OrdinalIgnoreCase));
    }
}
