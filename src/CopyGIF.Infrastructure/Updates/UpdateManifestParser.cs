using System.Globalization;
using System.Text.Json;
using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Updates;

public sealed class UpdateManifestParser
{
    public const long MaximumPackageBytes =
        1024L * 1024L * 1024L;

    private static readonly JsonSerializerOptions
        SerializerOptions =
            new(JsonSerializerDefaults.Web)
            {
                AllowTrailingCommas = false,
                MaxDepth = 16,
                ReadCommentHandling =
                    JsonCommentHandling.Disallow,
                PropertyNameCaseInsensitive = false
            };

    public UpdateManifest Parse(
        ReadOnlySpan<byte> utf8Json,
        string expectedChannel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            expectedChannel);

        try
        {
            byte[] manifestBytes =
                utf8Json.ToArray();

            using JsonDocument document =
                JsonDocument.Parse(
                    manifestBytes,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling =
                            JsonCommentHandling.Disallow,
                        MaxDepth = 16
                    });

            EnsureNoDuplicateProperties(
                document.RootElement);

            UpdateManifest manifest =
                JsonSerializer.Deserialize<UpdateManifest>(
                    manifestBytes,
                    SerializerOptions) ??
                throw new InvalidDataException(
                    "The update manifest is empty.");

            Validate(
                manifest,
                expectedChannel);

            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The update manifest is not valid JSON.",
                exception);
        }
    }

    public static void Validate(
        UpdateManifest manifest,
        string expectedChannel)
    {
        ArgumentNullException.ThrowIfNull(
            manifest);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            expectedChannel);

        if (manifest.SchemaVersion !=
            UpdateManifest.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                "The update manifest schema is not supported.");
        }

        if (!IsSemanticVersion(
                manifest.Version) ||
            !IsSemanticVersion(
                manifest.MinimumSupportedVersion))
        {
            throw new InvalidDataException(
                "The update manifest contains an invalid semantic version.");
        }

        if (!string.Equals(
                manifest.Channel,
                expectedChannel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The update manifest channel does not match the requested channel.");
        }

        if (string.IsNullOrWhiteSpace(
                manifest.AssetName) ||
            manifest.AssetName.Length > 255 ||
            !string.Equals(
                manifest.AssetName,
                Path.GetFileName(
                    manifest.AssetName),
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(
                    manifest.AssetName),
                ".msi",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The update manifest contains an invalid MSI asset name.");
        }

        ValidateRepositoryAssetUri(
            manifest.AssetUri,
            manifest.AssetName);

        ValidateReleaseNotesUri(
            manifest.ReleaseNotesUri);

        if (manifest.SizeBytes <= 0 ||
            manifest.SizeBytes > MaximumPackageBytes)
        {
            throw new InvalidDataException(
                "The update package size is outside the supported range.");
        }

        if (!TryParseSha256(
                manifest.Sha256,
                out _))
        {
            throw new InvalidDataException(
                "The update manifest contains an invalid SHA-256 value.");
        }

        if (manifest.PublishedAtUtc == default)
        {
            throw new InvalidDataException(
                "The update manifest is missing its publication time.");
        }
    }

    internal static void EnsureAllowedTransportUri(
        Uri uri)
    {
        ArgumentNullException.ThrowIfNull(
            uri);

        if (!uri.IsAbsoluteUri ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(
                uri.UserInfo) ||
            !uri.IsDefaultPort ||
            uri.IsLoopback ||
            !AllowedTransportHosts.Contains(
                uri.IdnHost))
        {
            throw new InvalidDataException(
                "The update address is not an approved GitHub HTTPS endpoint.");
        }
    }

    internal static bool TryParseSha256(
        string? value,
        out byte[] hash)
    {
        hash = [];

        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Length != 64)
        {
            return false;
        }

        try
        {
            hash = Convert.FromHexString(
                value);

            return hash.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static readonly HashSet<string>
        AllowedTransportHosts =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "github.com",
                "objects.githubusercontent.com",
                "release-assets.githubusercontent.com",
                "github-releases.githubusercontent.com"
            };

    private static void ValidateRepositoryAssetUri(
        Uri? uri,
        string assetName)
    {
        if (uri is null)
        {
            throw new InvalidDataException(
                "The update manifest is missing the package address.");
        }

        EnsureAllowedTransportUri(
            uri);

        if (!string.Equals(
                uri.IdnHost,
                "github.com",
                StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(
                "/hphifer99/CopyGIF/releases/download/",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Uri.UnescapeDataString(
                    uri.Segments[^1]),
                assetName,
                StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(
                uri.Query) ||
            !string.IsNullOrEmpty(
                uri.Fragment))
        {
            throw new InvalidDataException(
                "The update package address is outside the official CopyGIF repository.");
        }
    }

    private static void ValidateReleaseNotesUri(
        Uri? uri)
    {
        if (uri is null)
        {
            throw new InvalidDataException(
                "The update manifest is missing the release-notes address.");
        }

        EnsureAllowedTransportUri(
            uri);

        if (!string.Equals(
                uri.IdnHost,
                "github.com",
                StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(
                "/hphifer99/CopyGIF/releases/",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(
                uri.Query) ||
            !string.IsNullOrEmpty(
                uri.Fragment))
        {
            throw new InvalidDataException(
                "The release-notes address is outside the official CopyGIF repository.");
        }
    }

    private static bool IsSemanticVersion(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Length > 128)
        {
            return false;
        }

        string withoutBuildMetadata =
            value.Split(
                '+',
                count: 2,
                StringSplitOptions.None)[0];

        string[] versionAndPrerelease =
            withoutBuildMetadata.Split(
                '-',
                count: 2,
                StringSplitOptions.None);

        string[] numberParts =
            versionAndPrerelease[0].Split(
                '.',
                StringSplitOptions.None);

        if (numberParts.Length != 3 ||
            numberParts.Any(
                part =>
                    !int.TryParse(
                        part,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out _)))
        {
            return false;
        }

        if (versionAndPrerelease.Length == 1)
        {
            return true;
        }

        string[] identifiers =
            versionAndPrerelease[1].Split(
                '.',
                StringSplitOptions.None);

        return identifiers.Length > 0 &&
               identifiers.All(
                   identifier =>
                       identifier.Length > 0 &&
                       identifier.All(
                           character =>
                               char.IsAsciiLetterOrDigit(
                                   character) ||
                               character == '-'));
    }

    private static void EnsureNoDuplicateProperties(
        JsonElement root)
    {
        if (root.ValueKind !=
            JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "The update manifest root must be a JSON object.");
        }

        HashSet<string> names =
            new(StringComparer.Ordinal);

        foreach (JsonProperty property
                 in root.EnumerateObject())
        {
            if (!names.Add(
                    property.Name))
            {
                throw new InvalidDataException(
                    $"The update manifest contains the duplicate property '{property.Name}'.");
            }
        }
    }
}
