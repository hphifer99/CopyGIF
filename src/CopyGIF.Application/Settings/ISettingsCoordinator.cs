using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Settings;

public interface ISettingsCoordinator
{
    Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads settings for display in the Settings window. Unlike <see cref="LoadAsync"/>, the
    /// "Start with Windows" value reflects what Windows actually has registered, so a startup
    /// entry removed outside the app (for example in Task Manager) is shown as off.
    /// The default simply returns the saved settings.
    /// </summary>
    Task<AppSettings> LoadForEditingAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            cancellationToken);

    Task<SettingsSaveResult> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);

    async Task<SettingsSaveResult> UpdateAsync(Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        return await SaveAsync(update(await LoadAsync(cancellationToken)), cancellationToken);
    }

    Task<SettingsSaveResult> RestoreDefaultsAsync(
        CancellationToken cancellationToken = default);

    Task<SettingsSaveResult?>
        ChooseLibraryStorageRootAsync(
            CancellationToken cancellationToken = default);
}

public sealed record SettingsSaveResult
{
    public required bool Succeeded { get; init; }

    public required AppSettings EffectiveSettings { get; init; }

    public HotkeyRegistrationFailure HotkeyFailure { get; init; }

    public string? ErrorMessage { get; init; }

    public static SettingsSaveResult Success(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        return new SettingsSaveResult
        {
            Succeeded = true,
            EffectiveSettings = settings
        };
    }

    public static SettingsSaveResult HotkeyRejected(
        AppSettings settings,
        HotkeyRegistrationResult registrationResult)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        ArgumentNullException.ThrowIfNull(
            registrationResult);

        if (registrationResult.Succeeded ||
            registrationResult.Failure ==
            HotkeyRegistrationFailure.None)
        {
            throw new ArgumentException(
                "A rejected save requires a failed hotkey registration.",
                nameof(registrationResult));
        }

        return new SettingsSaveResult
        {
            Succeeded = false,
            EffectiveSettings = settings,
            HotkeyFailure = registrationResult.Failure,
            ErrorMessage = registrationResult.Message
        };
    }
}
