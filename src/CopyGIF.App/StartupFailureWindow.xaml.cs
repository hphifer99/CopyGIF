using Microsoft.UI.Xaml;

namespace CopyGIF.App;

public sealed partial class StartupFailureWindow :
    Window
{
    public StartupFailureWindow(
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        InitializeComponent();

        MessageTextBlock.Text =
            message.Trim();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
