using System;
using CopyGIF.Presentation.ViewModels;
using Microsoft.UI.Xaml;

namespace CopyGIF.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel)
    {
        ViewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();
    }
}