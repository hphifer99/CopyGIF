using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Giphy;

public sealed class GiphyCredentialManager :
    IGifProviderCredentialManager
{
    private readonly GiphyGifProvider
        _provider;

    private readonly ISecretStore
        _secretStore;

    public GiphyCredentialManager(
        GiphyGifProvider provider,
        ISecretStore secretStore)
    {
        _provider =
            provider ??
            throw new ArgumentNullException(
                nameof(provider));

        _secretStore =
            secretStore ??
            throw new ArgumentNullException(
                nameof(secretStore));
    }

    public string ProviderId =>
        GiphyGifProvider.ProviderId;

    public string DisplayName =>
        "GIPHY";

    public string SecretName =>
        SecretNames.GiphyApiKey;

    public async Task<bool>
        HasCredentialAsync(
            CancellationToken cancellationToken =
                default)
    {
        string? credential =
            await _secretStore.GetAsync(
                SecretNames.GiphyApiKey,
                cancellationToken);

        return !string.IsNullOrWhiteSpace(
            credential);
    }

    public Task<CredentialValidationResult>
        ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken =
                default)
    {
        return _provider
            .ValidateCredentialAsync(
                credential,
                cancellationToken);
    }

    public Task SaveCredentialAsync(
        string credential,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            credential);

        return _secretStore.SetAsync(
            SecretNames.GiphyApiKey,
            credential.Trim(),
            cancellationToken);
    }

    public Task DeleteCredentialAsync(
        CancellationToken cancellationToken =
            default)
    {
        return _secretStore.DeleteAsync(
            SecretNames.GiphyApiKey,
            cancellationToken);
    }
}