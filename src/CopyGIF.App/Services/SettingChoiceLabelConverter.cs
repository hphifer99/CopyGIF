using CopyGIF.Core.Settings;
using Microsoft.UI.Xaml.Data;

namespace CopyGIF.App.Services;

public sealed class SettingChoiceLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        UpdateMode.Recommended => "Recommended",
        UpdateMode.NotifyOnly => "Notify only",
        UpdateMode.DownloadAndPrompt => "Download and ask to install",
        UpdateMode.DownloadAndInstall => "Download and install",
        WindowPlacementMode.Mouse => "Near the mouse pointer",
        WindowPlacementMode.Center => "Center of the current screen",
        WindowPlacementMode.Remember => "Remember last position",
        AppTheme.System => "Follow Windows",
        AppTheme.Light => "Light",
        AppTheme.Dark => "Dark",
        _ => value?.ToString() ?? string.Empty
    };
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
