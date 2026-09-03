using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Settings;

public interface ISettingsCoordinator
{
    Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default);

    Task<SettingsSaveResult> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);

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
