using Microsoft.UI.Xaml;

namespace CopyGIF.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void HealthCheckButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        StatusText.Text = "Application shell verified.";
    }
}