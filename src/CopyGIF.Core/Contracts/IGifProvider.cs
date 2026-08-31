using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IGifProvider
{
    string Id { get; }

    string DisplayName { get; }

    Task<GifSearchPage> SearchAsync(
        GifSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<CredentialValidationResult> ValidateCredentialAsync(
        string credential,
        CancellationToken cancellationToken = default);

    Task RegisterShareAsync(
        string itemId,
        string? searchQuery,
        CancellationToken cancellationToken = default);
}