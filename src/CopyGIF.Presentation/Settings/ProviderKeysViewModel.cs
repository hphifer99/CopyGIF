using CommunityToolkit.Mvvm.ComponentModel;
using CopyGIF.Application.Settings;

namespace CopyGIF.Presentation.Settings;

public sealed record ProviderChoice(string Id, string Name);

public sealed class ProviderKeysViewModel(SettingsEditSession session) : ObservableObject
{
    private string _activeProviderId = "klipy";
    private string _klipyCredential = string.Empty;
    private string _giphyCredential = string.Empty;
    private bool _deleteKlipyKey;
    private bool _deleteGiphyKey;
    private bool _hasKlipyKey;
    private bool _hasGiphyKey;
    private bool _refreshing;
    public IReadOnlyList<ProviderChoice> Providers { get; } =
        [new("klipy", "KLIPY"), new("giphy", "GIPHY")];
    public string ActiveProviderId { get => _activeProviderId; set => SetProperty(ref _activeProviderId, value); }
    public string KlipyCredential
    {
        get => _klipyCredential;
        set { if (SetProperty(ref _klipyCredential, value ?? string.Empty)) Stage("klipy", value, DeleteKlipyKey); }
    }
    public string GiphyCredential
    {
        get => _giphyCredential;
        set { if (SetProperty(ref _giphyCredential, value ?? string.Empty)) Stage("giphy", value, DeleteGiphyKey); }
    }
    public bool DeleteKlipyKey
    {
        get => _deleteKlipyKey;
        set { if (SetProperty(ref _deleteKlipyKey, value)) Stage("klipy", KlipyCredential, value); }
    }
    public bool DeleteGiphyKey
    {
        get => _deleteGiphyKey;
        set { if (SetProperty(ref _deleteGiphyKey, value)) Stage("giphy", GiphyCredential, value); }
    }
    public string KlipyStatus => _hasKlipyKey ? "A KLIPY key is saved." : "No KLIPY key is saved.";
    public string GiphyStatus => _hasGiphyKey ? "A GIPHY key is saved." : "No GIPHY key is saved.";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _refreshing = true;
        try
        {
            ActiveProviderId = session.Baseline.Providers.ActiveProviderId;
            KlipyCredential = GiphyCredential = string.Empty;
            DeleteKlipyKey = DeleteGiphyKey = false;
            _hasKlipyKey = await session.HasCredentialAsync("klipy", cancellationToken);
            _hasGiphyKey = await session.HasCredentialAsync("giphy", cancellationToken);
            OnPropertyChanged(nameof(KlipyStatus));
            OnPropertyChanged(nameof(GiphyStatus));
        }
        finally { _refreshing = false; }
    }

    private void Stage(string id, string? value, bool delete)
    {
        if (_refreshing) return;
        if (delete) session.StageCredential(id, null);
        else if (!string.IsNullOrWhiteSpace(value)) session.StageCredential(id, value);
        else session.UnstageCredential(id);
    }
}
