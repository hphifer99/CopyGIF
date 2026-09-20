using CommunityToolkit.Mvvm.ComponentModel;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Presentation.Settings;

public sealed record ProviderChoice(string Id, string Name);

/// <summary>
/// The API key section of Settings. It has one entry for every registered provider that needs a
/// key, so a newly registered provider gets its own key box, status text and help link without any
/// change to this class or to the page that shows it.
/// </summary>
public sealed class ProviderKeysViewModel : ObservableObject
{
    private readonly SettingsEditSession _session;
    private string _activeProviderId = AppSettings.DefaultProviderId;
    private bool _refreshing;

    public ProviderKeysViewModel(SettingsEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Keys = Array.AsReadOnly(
            session.Providers
                .Select(provider => new ProviderKeyEntry(this, provider))
                .ToArray());
        Providers = Array.AsReadOnly(
            session.Providers
                .Select(provider => new ProviderChoice(provider.Id, provider.DisplayName))
                .ToArray());
    }

    /// <summary>The choices for the active provider.</summary>
    public IReadOnlyList<ProviderChoice> Providers { get; }

    /// <summary>One entry per provider, in registration order.</summary>
    public IReadOnlyList<ProviderKeyEntry> Keys { get; }

    public string ActiveProviderId
    {
        get => _activeProviderId;
        set => SetProperty(ref _activeProviderId, value);
    }

    public ProviderKeyEntry? Find(string providerId) =>
        Keys.FirstOrDefault(entry => string.Equals(entry.Id, providerId, StringComparison.OrdinalIgnoreCase));

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _refreshing = true;
        try
        {
            ActiveProviderId = _session.Baseline.Providers.ActiveProviderId;
            foreach (ProviderKeyEntry entry in Keys)
            {
                entry.Credential = string.Empty;
                entry.DeleteKey = false;
                entry.HasSavedKey = await _session.HasCredentialAsync(entry.Id, cancellationToken);
            }
        }
        finally { _refreshing = false; }
    }

    internal void Stage(ProviderKeyEntry entry)
    {
        // The Settings window watches this view model as a whole to notice edits, so an edit to
        // any one key is announced here as well.
        OnPropertyChanged(nameof(Keys));
        if (_refreshing) return;
        if (entry.DeleteKey) _session.StageCredential(entry.Id, null);
        else if (!string.IsNullOrWhiteSpace(entry.Credential)) _session.StageCredential(entry.Id, entry.Credential);
        else _session.UnstageCredential(entry.Id);
    }
}

/// <summary>The key box, status and help link of one provider.</summary>
public sealed class ProviderKeyEntry : ObservableObject
{
    private readonly ProviderKeysViewModel _owner;
    private string _credential = string.Empty;
    private bool _deleteKey;
    private bool _hasSavedKey;

    internal ProviderKeyEntry(ProviderKeysViewModel owner, ProviderDescriptor provider)
    {
        _owner = owner;
        Id = provider.Id;
        DisplayName = provider.DisplayName;
        HelpUri = provider.CredentialHelpUri;
        Instructions = provider.CredentialInstructions;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public Uri? HelpUri { get; }

    public string? Instructions { get; }

    public bool HasHelpUri => HelpUri is not null;

    public bool HasInstructions => !string.IsNullOrWhiteSpace(Instructions);

    public string KeyHeader => $"{DisplayName} API key";

    public string RemoveLabel => $"Remove saved {DisplayName} key on Apply";

    public string HelpLabel => $"Open {DisplayName} API key help";

    /// <summary>A replacement key typed by the person. Empty means no change.</summary>
    public string Credential
    {
        get => _credential;
        set
        {
            if (SetProperty(ref _credential, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Placeholder));
                _owner.Stage(this);
            }
        }
    }

    public bool DeleteKey
    {
        get => _deleteKey;
        set
        {
            if (SetProperty(ref _deleteKey, value))
            {
                OnPropertyChanged(nameof(Placeholder));
                _owner.Stage(this);
            }
        }
    }

    public bool HasSavedKey
    {
        get => _hasSavedKey;
        internal set
        {
            if (SetProperty(ref _hasSavedKey, value))
            {
                OnPropertyChanged(nameof(Placeholder));
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public string Placeholder => HasSavedKey && !DeleteKey ? "●●●●●●●●●●●●" : "Enter API key";

    public string Status => HasSavedKey ? $"A {DisplayName} key is saved." : $"No {DisplayName} key is saved.";
}
