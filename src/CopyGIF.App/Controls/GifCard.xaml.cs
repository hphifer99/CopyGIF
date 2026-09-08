using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CopyGIF.App.Controls;

public sealed partial class GifCard :
    UserControl
{
    private const int PreviewDecodePixelWidth =
        320;

    public static readonly DependencyProperty
        TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(GifCard),
                new PropertyMetadata(
                    string.Empty,
                    HandleAccessibleTextChanged));

    public static readonly DependencyProperty
        ProviderNameProperty =
            DependencyProperty.Register(
                nameof(ProviderName),
                typeof(string),
                typeof(GifCard),
                new PropertyMetadata(
                    string.Empty,
                    HandleAccessibleTextChanged));

    public static readonly DependencyProperty
        ThumbnailUriProperty =
            DependencyProperty.Register(
                nameof(ThumbnailUri),
                typeof(Uri),
                typeof(GifCard),
                new PropertyMetadata(
                    null,
                    HandleThumbnailUriChanged));

    public static readonly DependencyProperty
        PreviewUriProperty =
            DependencyProperty.Register(
                nameof(PreviewUri),
                typeof(Uri),
                typeof(GifCard),
                new PropertyMetadata(
                    null,
                    HandlePreviewUriChanged));

    public static readonly DependencyProperty
        IsFavoriteProperty =
            DependencyProperty.Register(
                nameof(IsFavorite),
                typeof(bool),
                typeof(GifCard),
                new PropertyMetadata(
                    false,
                    HandleIsFavoriteChanged));

    public static readonly DependencyProperty
        IsBusyProperty =
            DependencyProperty.Register(
                nameof(IsBusy),
                typeof(bool),
                typeof(GifCard),
                new PropertyMetadata(
                    false,
                    HandleIsBusyChanged));

    public static readonly DependencyProperty
        AnimatePreviewProperty =
            DependencyProperty.Register(
                nameof(AnimatePreview),
                typeof(bool),
                typeof(GifCard),
                new PropertyMetadata(
                    true,
                    HandleAnimationPreferenceChanged));

    public static readonly DependencyProperty
        SystemAnimationsEnabledProperty =
            DependencyProperty.Register(
                nameof(SystemAnimationsEnabled),
                typeof(bool),
                typeof(GifCard),
                new PropertyMetadata(
                    true,
                    HandleAnimationPreferenceChanged));

    public static readonly DependencyProperty
        SelectCommandProperty =
            DependencyProperty.Register(
                nameof(SelectCommand),
                typeof(ICommand),
                typeof(GifCard),
                new PropertyMetadata(
                    null));

    public static readonly DependencyProperty
        SelectCommandParameterProperty =
            DependencyProperty.Register(
                nameof(SelectCommandParameter),
                typeof(object),
                typeof(GifCard),
                new PropertyMetadata(
                    null));

    public static readonly DependencyProperty
        ToggleFavoriteCommandProperty =
            DependencyProperty.Register(
                nameof(ToggleFavoriteCommand),
                typeof(ICommand),
                typeof(GifCard),
                new PropertyMetadata(
                    null));

    public static readonly DependencyProperty
        ToggleFavoriteCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ToggleFavoriteCommandParameter),
                typeof(object),
                typeof(GifCard),
                new PropertyMetadata(
                    null));

    private BitmapImage? _previewBitmap;

    private bool _isPointerOver;

    private bool _hasKeyboardFocus;

    private bool _isPreviewReady;

    public GifCard()
    {
        InitializeComponent();

        UpdateFavoriteState();
        UpdateBusyState();
        UpdateAccessibleText();
        RefreshThumbnail();
    }

    public string Title
    {
        get =>
            (string)GetValue(
                TitleProperty);

        set =>
            SetValue(
                TitleProperty,
                value);
    }

    public string ProviderName
    {
        get =>
            (string)GetValue(
                ProviderNameProperty);

        set =>
            SetValue(
                ProviderNameProperty,
                value);
    }

    public Uri? ThumbnailUri
    {
        get =>
            GetValue(
                ThumbnailUriProperty)
                as Uri;

        set =>
            SetValue(
                ThumbnailUriProperty,
                value);
    }

    public Uri? PreviewUri
    {
        get =>
            GetValue(
                PreviewUriProperty)
                as Uri;

        set =>
            SetValue(
                PreviewUriProperty,
                value);
    }

    public bool IsFavorite
    {
        get =>
            (bool)GetValue(
                IsFavoriteProperty);

        set =>
            SetValue(
                IsFavoriteProperty,
                value);
    }

    public bool IsBusy
    {
        get =>
            (bool)GetValue(
                IsBusyProperty);

        set =>
            SetValue(
                IsBusyProperty,
                value);
    }

    public bool AnimatePreview
    {
        get =>
            (bool)GetValue(
                AnimatePreviewProperty);

        set =>
            SetValue(
                AnimatePreviewProperty,
                value);
    }

    public bool SystemAnimationsEnabled
    {
        get =>
            (bool)GetValue(
                SystemAnimationsEnabledProperty);

        set =>
            SetValue(
                SystemAnimationsEnabledProperty,
                value);
    }

    public ICommand? SelectCommand
    {
        get =>
            GetValue(
                SelectCommandProperty)
                as ICommand;

        set =>
            SetValue(
                SelectCommandProperty,
                value);
    }

    public object? SelectCommandParameter
    {
        get =>
            GetValue(
                SelectCommandParameterProperty);

        set =>
            SetValue(
                SelectCommandParameterProperty,
                value);
    }

    public ICommand? ToggleFavoriteCommand
    {
        get =>
            GetValue(
                ToggleFavoriteCommandProperty)
                as ICommand;

        set =>
            SetValue(
                ToggleFavoriteCommandProperty,
                value);
    }

    public object? ToggleFavoriteCommandParameter
    {
        get =>
            GetValue(
                ToggleFavoriteCommandParameterProperty);

        set =>
            SetValue(
                ToggleFavoriteCommandParameterProperty,
                value);
    }

    private bool CanAnimatePreview =>
        AnimatePreview &&
        SystemAnimationsEnabled &&
        PreviewUri is not null;

    private static void HandleAccessibleTextChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((GifCard)sender)
            .UpdateAccessibleText();
    }

    private static void HandleThumbnailUriChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((GifCard)sender)
            .RefreshThumbnail();
    }

    private static void HandlePreviewUriChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        GifCard card =
            (GifCard)sender;

        card.ResetPreview();
        card.UpdatePreviewPlayback();
    }

    private static void HandleIsFavoriteChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((GifCard)sender)
            .UpdateFavoriteState();
    }

    private static void HandleIsBusyChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((GifCard)sender)
            .UpdateBusyState();
    }

    private static void HandleAnimationPreferenceChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((GifCard)sender)
            .UpdatePreviewPlayback();
    }

    private void SelectButton_PointerEntered(
        object sender,
        PointerRoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _isPointerOver =
            true;

        UpdatePreviewPlayback();
    }

    private void SelectButton_PointerExited(
        object sender,
        PointerRoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _isPointerOver =
            false;

        UpdatePreviewPlayback();
    }

    private void SelectButton_GotFocus(
        object sender,
        RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _hasKeyboardFocus =
            true;

        UpdatePreviewPlayback();
    }

    private void SelectButton_LostFocus(
        object sender,
        RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _hasKeyboardFocus =
            false;

        UpdatePreviewPlayback();
    }

    private void AnimatedImage_ImageOpened(
        object sender,
        RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _isPreviewReady =
            true;

        UpdatePreviewPlayback();
    }

    private void AnimatedImage_ImageFailed(
        object sender,
        ExceptionRoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        ResetPreview();
    }

    private void Root_Unloaded(
        object sender,
        RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _isPointerOver =
            false;

        _hasKeyboardFocus =
            false;

        ResetPreview();
    }

    private void RefreshThumbnail()
    {
        Uri? thumbnailUri =
            ThumbnailUri;

        if (thumbnailUri is null)
        {
            ThumbnailImage.Source =
                null;

            PlaceholderIcon.Visibility =
                Visibility.Visible;

            return;
        }

        ThumbnailImage.Source =
            new BitmapImage
            {
                AutoPlay = false,
                DecodePixelWidth =
                    PreviewDecodePixelWidth,
                UriSource =
                    thumbnailUri
            };

        PlaceholderIcon.Visibility =
            Visibility.Collapsed;
    }

    private void EnsurePreviewLoaded()
    {
        if (_previewBitmap is not null ||
            PreviewUri is not Uri previewUri)
        {
            return;
        }

        _previewBitmap =
            new BitmapImage
            {
                AutoPlay = false,
                DecodePixelWidth =
                    PreviewDecodePixelWidth,
                UriSource =
                    previewUri
            };

        AnimatedImage.Source =
            _previewBitmap;
    }

    private void UpdatePreviewPlayback()
    {
        bool shouldPlay =
            CanAnimatePreview &&
            (_isPointerOver ||
             _hasKeyboardFocus);

        if (!shouldPlay)
        {
            StopPreviewPlayback();

            return;
        }

        EnsurePreviewLoaded();

        if (!_isPreviewReady ||
            _previewBitmap is null)
        {
            return;
        }

        AnimatedImage.Opacity =
            1;

        if (_previewBitmap.IsAnimatedBitmap &&
            !_previewBitmap.IsPlaying)
        {
            _previewBitmap.Play();
        }
    }

    private void StopPreviewPlayback()
    {
        if (_previewBitmap?.IsPlaying ==
            true)
        {
            _previewBitmap.Stop();
        }

        AnimatedImage.Opacity =
            0;
    }

    private void ResetPreview()
    {
        StopPreviewPlayback();

        AnimatedImage.Source =
            null;

        _previewBitmap =
            null;

        _isPreviewReady =
            false;
    }

    private void UpdateFavoriteState()
    {
        FavoriteIcon.Glyph =
            IsFavorite
                ? "\uE735"
                : "\uE734";

        AutomationProperties.SetName(
            FavoriteButton,
            IsFavorite
                ? "Remove GIF from Favorites"
                : "Add GIF to Favorites");
    }

    private void UpdateBusyState()
    {
        BusyOverlay.Visibility =
            IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        SelectButton.IsEnabled =
            !IsBusy;
    }

    private void UpdateAccessibleText()
    {
        string accessibleTitle =
            string.IsNullOrWhiteSpace(
                Title)
                ? "GIF result"
                : $"Copy GIF: {Title.Trim()}";

        AutomationProperties.SetName(
            SelectButton,
            accessibleTitle);

        AutomationProperties.SetHelpText(
            SelectButton,
            string.IsNullOrWhiteSpace(
                ProviderName)
                ? "Press Enter to copy this GIF."
                : $"Provided by {ProviderName.Trim()}. Press Enter to copy this GIF.");

        ProviderTextBlock.Visibility =
            string.IsNullOrWhiteSpace(
                ProviderName)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }
}
