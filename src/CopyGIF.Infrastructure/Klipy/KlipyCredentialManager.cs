using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Klipy;

public sealed class KlipyCredentialManager :
    IGifProviderCredentialManager
{
    private readonly KlipyGifProvider
        _provider;

    private readonly ISecretStore
        _secretStore;

    public KlipyCredentialManager(
        KlipyGifProvider provider,
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
        KlipyGifProvider.ProviderId;

    public string DisplayName =>
        "KLIPY";

    public async Task<bool>
        HasCredentialAsync(
            CancellationToken cancellationToken =
                default)
    {
        string? credential =
            await _secretStore.GetAsync(
                SecretNames.KlipyApiKey,
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
            SecretNames.KlipyApiKey,
            credential.Trim(),
            cancellationToken);
    }

    public Task DeleteCredentialAsync(
        CancellationToken cancellationToken =
            default)
    {
        return _secretStore.DeleteAsync(
            SecretNames.KlipyApiKey,
            cancellationToken);
    }
}