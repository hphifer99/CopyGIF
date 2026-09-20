using CopyGIF.Application.Settings;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Onboarding;

public sealed class ProviderOnboardingCoordinator(ISettingsCoordinator settings, ISecretStore secrets,
    IEnumerable<IGifProviderCredentialManager> managers, IStartupService startup,
    IApplicationPaths paths)
{
    public async Task<CredentialValidationResult> CompleteAsync(string providerId, string credential, CancellationToken token)
    {
        IGifProviderCredentialManager manager = managers.Single(m => m.ProviderId == providerId);
        var validation = await manager.ValidateCredentialAsync(credential, token).ConfigureAwait(false);
        if (!validation.IsValid) return validation;
        string name = manager.SecretName;
        bool firstSetup = !File.Exists(paths.SettingsPath);
        string? previous = await secrets.GetAsync(name, token).ConfigureAwait(false);
        try
        {
            await manager.SaveCredentialAsync(credential, token).ConfigureAwait(false);
            SettingsSaveResult saved = await settings.UpdateAsync(latest => latest with
            {
                Providers = latest.Providers with { ActiveProviderId = providerId }
            }, token).ConfigureAwait(false);
            if (!saved.Succeeded)
                throw new UserFacingException(saved.ErrorMessage ?? "Provider settings could not be saved.");
            if (firstSetup && saved.EffectiveSettings.Startup.StartWithWindows)
            {
                try { await startup.SetEnabledAsync(true, token).ConfigureAwait(false); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    RepairDiagnostics.Record("startup-registration", providerId, exception.GetType().Name);
                }
            }
        }
        catch (Exception failure)
        {
            try
            {
                if (previous is null) await secrets.DeleteAsync(name, CancellationToken.None).ConfigureAwait(false);
                else await secrets.SetAsync(name, previous, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rollback)
            {
                throw new AggregateException("Provider setup failed and its previous key could not be restored.", failure, rollback);
            }
            throw;
        }
        return validation;
    }
}
