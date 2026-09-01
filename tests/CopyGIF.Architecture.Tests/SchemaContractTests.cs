using System.Text.Json;
using CopyGIF.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class SchemaContractTests
{
    private static readonly string[]
        ExpectedSchemaFileNames =
        [
            "settings.schema.json",
            "library.schema.json",
            "search-history.schema.json",
            "update-manifest.schema.json"
        ];

    [TestMethod]
    public void RequiredSchemas_ExistAndContainValidJson()
    {
        string schemaDirectory =
            GetSchemaDirectory();

        foreach (string fileName
                 in ExpectedSchemaFileNames)
        {
            string schemaPath =
                Path.Combine(
                    schemaDirectory,
                    fileName);

            Assert.IsTrue(
                File.Exists(
                    schemaPath),
                $"Required schema is missing: {fileName}");

            using JsonDocument document =
                JsonDocument.Parse(
                    File.ReadAllText(
                        schemaPath));

            Assert.AreEqual(
                JsonValueKind.Object,
                document.RootElement.ValueKind,
                $"Schema root must be an object: {fileName}");
        }
    }

    [TestMethod]
    public void RequiredSchemas_UseDraft202012AndVersionOne()
    {
        string schemaDirectory =
            GetSchemaDirectory();

        foreach (string fileName
                 in ExpectedSchemaFileNames)
        {
            using JsonDocument document =
                LoadSchema(
                    schemaDirectory,
                    fileName);

            JsonElement root =
                document.RootElement;

            Assert.AreEqual(
                "https://json-schema.org/draft/2020-12/schema",
                root.GetProperty(
                        "$schema")
                    .GetString(),
                $"Unexpected JSON Schema draft in {fileName}.");

            int schemaVersion =
                root.GetProperty(
                        "properties")
                    .GetProperty(
                        "schemaVersion")
                    .GetProperty(
                        "const")
                    .GetInt32();

            Assert.AreEqual(
                1,
                schemaVersion,
                $"Unexpected schema version in {fileName}.");
        }
    }

    [TestMethod]
    public void SettingsSchema_RequiresEveryFrozenGroup()
    {
        string schemaDirectory =
            GetSchemaDirectory();

        using JsonDocument document =
            LoadSchema(
                schemaDirectory,
                "settings.schema.json");

        string[] requiredProperties =
            document.RootElement
                .GetProperty(
                    "required")
                .EnumerateArray()
                .Select(
                    element =>
                        element.GetString()!)
                .ToArray();

        string[] expectedProperties =
        [
            "schemaVersion",
            "hotkey",
            "search",
            "library",
            "window",
            "behavior",
            "appearance",
            "startup",
            "updates",
            "providers"
        ];

        CollectionAssert.AreEquivalent(
            expectedProperties,
            requiredProperties);
    }

    [TestMethod]
    public void SchemaObjectNodes_RejectUnknownProperties()
    {
        string schemaDirectory =
            GetSchemaDirectory();

        foreach (string fileName
                 in ExpectedSchemaFileNames)
        {
            using JsonDocument document =
                LoadSchema(
                    schemaDirectory,
                    fileName);

            JsonElement[] objectSchemas =
                EnumerateSchemaObjects(
                        document.RootElement)
                    .Where(
                        IsObjectSchema)
                    .ToArray();

            Assert.IsGreaterThan(
                0,
                objectSchemas.Length,
                $"No object schemas were found in {fileName}.");

            foreach (JsonElement objectSchema
                     in objectSchemas)
            {
                Assert.IsTrue(
                    objectSchema.TryGetProperty(
                        "additionalProperties",
                        out JsonElement additionalProperties),
                    $"Object schema does not declare " +
                    $"'additionalProperties' in {fileName}.");

                Assert.AreEqual(
                    JsonValueKind.False,
                    additionalProperties.ValueKind,
                    $"Object schema permits unknown properties " +
                    $"in {fileName}.");
            }
        }
    }

    [TestMethod]
    public void SettingsSchema_DeclaresNoSecretOrRuntimeProperties()
    {
        string schemaDirectory =
            GetSchemaDirectory();

        using JsonDocument document =
            LoadSchema(
                schemaDirectory,
                "settings.schema.json");

        string[] propertyNames =
            EnumerateDeclaredPropertyNames(
                    document.RootElement)
                .ToArray();

        string[] forbiddenNames =
        [
            "apiKey",
            "password",
            "credential",
            "secret",
            "token",
            "lastUpdateCheck",
            "migrationCompleted",
            "downloadedFile",
            "libraryEntry",
            "searchHistoryEntry"
        ];

        foreach (string forbiddenName
                 in forbiddenNames)
        {
            Assert.IsFalse(
                propertyNames.Contains(
                    forbiddenName,
                    StringComparer.OrdinalIgnoreCase),
                $"Settings schema contains forbidden state: " +
                $"{forbiddenName}.");
        }
    }

    [TestMethod]
    public void DataSchemas_PreserveFrozenSecurityLimits()
    {
        string schemaDirectory =
            GetSchemaDirectory();

        using JsonDocument libraryDocument =
            LoadSchema(
                schemaDirectory,
                "library.schema.json");

        JsonElement libraryEntryProperties =
            libraryDocument.RootElement
                .GetProperty(
                    "$defs")
                .GetProperty(
                    "libraryEntry")
                .GetProperty(
                    "properties");

        long maximumGifBytes =
            libraryEntryProperties
                .GetProperty(
                    "sizeBytes")
                .GetProperty(
                    "maximum")
                .GetInt64();

        Assert.AreEqual(
            50L * 1024L * 1024L,
            maximumGifBytes);

        using JsonDocument updateDocument =
            LoadSchema(
                schemaDirectory,
                "update-manifest.schema.json");

        JsonElement updateProperties =
            updateDocument.RootElement
                .GetProperty(
                    "properties");

        Assert.AreEqual(
            "^https://",
            updateProperties
                .GetProperty(
                    "assetUri")
                .GetProperty(
                    "pattern")
                .GetString());

        Assert.AreEqual(
            "^https://",
            updateProperties
                .GetProperty(
                    "releaseNotesUri")
                .GetProperty(
                    "pattern")
                .GetString());

        Assert.AreEqual(
            "^[A-Fa-f0-9]{64}$",
            updateProperties
                .GetProperty(
                    "sha256")
                .GetProperty(
                    "pattern")
                .GetString());

        Assert.AreEqual(
            1024L * 1024L * 1024L,
            updateProperties
                .GetProperty(
                    "sizeBytes")
                .GetProperty(
                    "maximum")
                .GetInt64());
    }

    private static string GetSchemaDirectory()
    {
        return Path.Combine(
            RepositoryRootLocator.Find(),
            "schemas");
    }

    private static JsonDocument LoadSchema(
        string schemaDirectory,
        string fileName)
    {
        string path =
            Path.Combine(
                schemaDirectory,
                fileName);

        return JsonDocument.Parse(
            File.ReadAllText(
                path));
    }

    private static bool IsObjectSchema(
        JsonElement element)
    {
        return element.TryGetProperty(
                   "type",
                   out JsonElement type) &&
               type.ValueKind ==
                   JsonValueKind.String &&
               string.Equals(
                   type.GetString(),
                   "object",
                   StringComparison.Ordinal);
    }

    private static IEnumerable<JsonElement>
        EnumerateSchemaObjects(
            JsonElement element)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            yield return element;

            foreach (JsonProperty property
                     in element.EnumerateObject())
            {
                foreach (JsonElement child
                         in EnumerateSchemaObjects(
                             property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind ==
                 JsonValueKind.Array)
        {
            foreach (JsonElement item
                     in element.EnumerateArray())
            {
                foreach (JsonElement child
                         in EnumerateSchemaObjects(
                             item))
                {
                    yield return child;
                }
            }
        }
    }

    private static IEnumerable<string>
        EnumerateDeclaredPropertyNames(
            JsonElement element)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            if (element.TryGetProperty(
                    "properties",
                    out JsonElement properties) &&
                properties.ValueKind ==
                    JsonValueKind.Object)
            {
                foreach (JsonProperty property
                         in properties.EnumerateObject())
                {
                    yield return property.Name;
                }
            }

            foreach (JsonProperty property
                     in element.EnumerateObject())
            {
                foreach (string propertyName
                         in EnumerateDeclaredPropertyNames(
                             property.Value))
                {
                    yield return propertyName;
                }
            }
        }
        else if (element.ValueKind ==
                 JsonValueKind.Array)
        {
            foreach (JsonElement item
                     in element.EnumerateArray())
            {
                foreach (string propertyName
                         in EnumerateDeclaredPropertyNames(
                             item))
                {
                    yield return propertyName;
                }
            }
        }
    }
}
