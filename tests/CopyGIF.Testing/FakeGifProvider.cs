using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Testing;

public sealed class FakeGifProvider :
    IGifProvider
{
    private readonly object _syncRoot = new();

    private readonly List<GifSearchRequest>
        _searchRequests = [];

    private readonly List<FakeShareRegistration>
        _shareRegistrations = [];

    public FakeGifProvider(
        string id = "klipy",
        string displayName = "KLIPY")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            id);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            displayName);

        Id = id.Trim();
        DisplayName = displayName.Trim();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public Func<
        GifSearchRequest,
        CancellationToken,
        Task<GifSearchPage>>? SearchHandler
    { get; set; }

    public Func<
        string,
        CancellationToken,
        Task<CredentialValidationResult>>?
        CredentialValidationHandler
    { get; set; }

    public Func<
        string,
        string?,
        CancellationToken,
        Task>? ShareRegistrationHandler
    { get; set; }

    public IReadOnlyList<GifSearchRequest>
        SearchRequests
    {
        get
        {
            lock (_syncRoot)
            {
                return _searchRequests.ToArray();
            }
        }
    }

    public IReadOnlyList<FakeShareRegistration>
        ShareRegistrations
    {
        get
        {
            lock (_syncRoot)
            {
                return _shareRegistrations.ToArray();
            }
        }
    }

    public Task<GifSearchPage> SearchAsync(
        GifSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        lock (_syncRoot)
        {
            _searchRequests.Add(
                request);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return SearchHandler is null
            ? Task.FromResult(
                GifSearchPage.Empty())
            : SearchHandler(
                request,
                cancellationToken);
    }

    public Task<CredentialValidationResult>
        ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            credential);

        cancellationToken.ThrowIfCancellationRequested();

        return CredentialValidationHandler is null
            ? Task.FromResult(
                CredentialValidationResult.Valid())
            : CredentialValidationHandler(
                credential,
                cancellationToken);
    }

    public Task RegisterShareAsync(
        string itemId,
        string? searchQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            itemId);

        lock (_syncRoot)
        {
            _shareRegistrations.Add(
                new FakeShareRegistration(
                    itemId,
                    searchQuery));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ShareRegistrationHandler is null
            ? Task.CompletedTask
            : ShareRegistrationHandler(
                itemId,
                searchQuery,
                cancellationToken);
    }
}

public sealed record FakeShareRegistration(
    string ItemId,
    string? SearchQuery);

public sealed class FakeGifProviderCredentialManager :
    IGifProviderCredentialManager
{
    private readonly List<string>
        _validationAttempts = [];

    private readonly List<string>
        _saveAttempts = [];

    public FakeGifProviderCredentialManager(
        string providerId = "klipy",
        string displayName = "KLIPY")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            displayName);

        ProviderId = providerId.Trim();
        DisplayName = displayName.Trim();
    }

    public string ProviderId { get; }

    public string DisplayName { get; }

    public string? StoredCredential { get; set; }

    public int DeleteCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<bool>>? HasCredentialHandler
    { get; set; }

    public Func<
        string,
        CancellationToken,
        Task<CredentialValidationResult>>?
        ValidationHandler
    { get; set; }

    public Func<
        string,
        CancellationToken,
        Task>? SaveHandler
    { get; set; }

    public Func<
        CancellationToken,
        Task>? DeleteHandler
    { get; set; }

    public IReadOnlyList<string> ValidationAttempts =>
        _validationAttempts.ToArray();

    public IReadOnlyList<string> SaveAttempts =>
        _saveAttempts.ToArray();

    public Task<bool> HasCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return HasCredentialHandler is null
            ? Task.FromResult(
                !string.IsNullOrWhiteSpace(
                    StoredCredential))
            : HasCredentialHandler(
                cancellationToken);
    }

    public Task<CredentialValidationResult>
        ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            credential);

        _validationAttempts.Add(
            credential);

        cancellationToken.ThrowIfCancellationRequested();

        return ValidationHandler is null
            ? Task.FromResult(
                CredentialValidationResult.Valid())
            : ValidationHandler(
                credential,
                cancellationToken);
    }

    public async Task SaveCredentialAsync(
        string credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            credential);

        _saveAttempts.Add(
            credential);

        cancellationToken.ThrowIfCancellationRequested();

        if (SaveHandler is not null)
        {
            await SaveHandler(
                    credential,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        StoredCredential = credential;
    }

    public async Task DeleteCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        if (DeleteHandler is not null)
        {
            await DeleteHandler(
                    cancellationToken)
                .ConfigureAwait(false);
        }

        StoredCredential = null;
    }
}
