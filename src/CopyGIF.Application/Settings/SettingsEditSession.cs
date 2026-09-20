using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Settings;

/// <summary>One settings transaction, with credential compensation on failure.</summary>
public sealed class SettingsEditSession(
    ISettingsCoordinator coordinator,
    ISecretStore secrets,
    IEnumerable<IGifProviderCredentialManager> credentialManagers,
    EffectiveSettings effective,
    IProviderCatalog providerCatalog)
{
    private readonly Dictionary<string, IGifProviderCredentialManager> _managers =
        credentialManagers.ToDictionary(manager => manager.ProviderId, StringComparer.OrdinalIgnoreCase);
    // Every provider that has an API key to manage, in registration order. Settings builds its
    // API key section from this list, so a newly registered provider appears there by itself.
    public IReadOnlyList<ProviderDescriptor> Providers { get; } =
        providerCatalog.Providers
            .Where(provider => provider.RequiresCredential &&
                credentialManagers.Any(manager => string.Equals(manager.ProviderId, provider.Id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    private readonly Dictionary<string, string?> _credentials = new(StringComparer.OrdinalIgnoreCase);
    public AppSettings Baseline { get; private set; } = new();
    public AppSettings Draft { get; private set; } = new();
    public bool HasChanges => Draft != Baseline || _credentials.Count != 0;
    public bool IsApplying { get; private set; }
    public event EventHandler? Changed;

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        Baseline = await coordinator.LoadForEditingAsync(cancellationToken).ConfigureAwait(false);
        Draft = Baseline;
        _credentials.Clear();
        effective.Preview(Draft);
    }

    public void SetDraft(AppSettings settings)
    {
        if (IsApplying) return;
        Draft = settings;
        effective.Preview(settings);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void StageCredential(string providerId, string? credential)
    {
        if (IsApplying) return;
        if (!_managers.ContainsKey(providerId)) throw new ArgumentException("Unknown provider.", nameof(providerId));
        _credentials[providerId] = credential?.Trim();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UnstageCredential(string providerId)
    {
        if (IsApplying) return;
        _credentials.Remove(providerId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> HasCredentialAsync(string providerId, CancellationToken cancellationToken = default) =>
        _managers[providerId].HasCredentialAsync(cancellationToken);

    public async Task<SettingsSaveResult> ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (IsApplying) throw new InvalidOperationException("Settings are already being applied.");
        IsApplying = true;
        var originals = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var written = new List<string>();
        try
        {
            var issues = AppSettingsValidator.Validate(Draft);
            if (issues.Count != 0)
                throw new UserFacingException(string.Join(Environment.NewLine, issues.Select(i => $"{i.Path}: {i.Message}")));
            // Validate all changed keys before writing any of them.
            foreach (var (id, value) in _credentials)
            {
                string? old = await secrets.GetAsync(SecretName(id), cancellationToken).ConfigureAwait(false);
                originals[id] = old;
                if (value is null || string.Equals(old, value, StringComparison.Ordinal)) continue;
                var validation = await _managers[id].ValidateCredentialAsync(value, cancellationToken).ConfigureAwait(false);
                if (!validation.IsValid) throw new UserFacingException($"{_managers[id].DisplayName}: {validation.Message}");
            }
            foreach (var (id, value) in _credentials)
            {
                if (string.Equals(originals[id], value, StringComparison.Ordinal)) continue;
                written.Add(id); // Include a write that might fail after changing storage.
                if (value is null) await secrets.DeleteAsync(SecretName(id), cancellationToken).ConfigureAwait(false);
                else await secrets.SetAsync(SecretName(id), value, cancellationToken).ConfigureAwait(false);
            }
            SettingsSaveResult result = await coordinator.UpdateAsync(latest => Draft with
            {
                Window = Draft.Window with
                {
                    Width = latest.Window.Width, Height = latest.Window.Height,
                    Left = latest.Window.Left, Top = latest.Window.Top,
                    LastMonitorId = latest.Window.LastMonitorId
                }
            }, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                throw new UserFacingException(result.ErrorMessage ?? "Windows rejected the requested hotkey.");
            Baseline = Draft = result.EffectiveSettings;
            _credentials.Clear();
            effective.Preview(Draft);
            return result;
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (string id in written.AsEnumerable().Reverse())
            {
                try
                {
                    if (originals[id] is string old)
                        await secrets.SetAsync(SecretName(id), old, CancellationToken.None).ConfigureAwait(false);
                    else await secrets.DeleteAsync(SecretName(id), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception rollback) { failures.Add(rollback); }
            }
            if (failures.Count > 1)
                throw new AggregateException("Apply failed and a previous API key could not be restored. Check both provider keys before retrying.", failures);
            throw;
        }
        finally { IsApplying = false; }
    }

    public void Discard()
    {
        if (IsApplying) throw new InvalidOperationException("Wait for Apply to finish.");
        Draft = Baseline;
        _credentials.Clear();
        effective.EndPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Close() => effective.EndPreview();

    private string SecretName(string providerId) =>
        _managers.TryGetValue(providerId, out IGifProviderCredentialManager? manager)
            ? manager.SecretName
            : throw new ArgumentException("Unknown provider.", nameof(providerId));
}
