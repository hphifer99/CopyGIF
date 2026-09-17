using CopyGIF.Core.Contracts;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Settings;

/// <summary>Only reversible preferences are exposed before Apply.</summary>
public sealed class EffectiveSettings(ISettingsStore store)
{
    private AppSettings? _preview;
    public event EventHandler? Changed;
    public bool IsEditing => Volatile.Read(ref _preview) is not null;
    public bool SuppressAutomaticUpdates => IsEditing;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        AppSettings saved = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        AppSettings? draft = Volatile.Read(ref _preview);
        if (draft is null) return AppSettingsNormalizer.Normalize(saved);
        return saved with
        {
            Search = draft.Search with
            {
                SaveSearchHistory = saved.Search.SaveSearchHistory && draft.Search.SaveSearchHistory,
                SearchHistoryLimit = saved.Search.SearchHistoryLimit
            },
            Appearance = draft.Appearance,
            Behavior = draft.Behavior,
            Window = saved.Window with
            {
                PlacementMode = draft.Window.PlacementMode,
                RememberWindowSize = draft.Window.RememberWindowSize,
                CenterOnTrayOpen = draft.Window.CenterOnTrayOpen
            }
        };
    }

    public void Preview(AppSettings draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Volatile.Write(ref _preview, draft);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void EndPreview()
    {
        Volatile.Write(ref _preview, null);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
