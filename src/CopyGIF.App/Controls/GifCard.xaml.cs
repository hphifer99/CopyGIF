using System.Windows.Input;
using CopyGIF.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CopyGIF.App.Controls;

public sealed partial class GifCard :
UserControl
{
    public static readonly DependencyProperty DecodeByHeightProperty =
        DependencyProperty.Register(nameof(DecodeByHeight), typeof(bool), typeof(GifCard),
            new PropertyMetadata(false, HandleDecodePixelSizeChanged));
    public bool DecodeByHeight
    {
        get => (bool)GetValue(DecodeByHeightProperty);
        set => SetValue(DecodeByHeightProperty, value);
    }

    public static readonly DependencyProperty DecodePixelSizeProperty =
        DependencyProperty.Register(nameof(DecodePixelSize), typeof(int), typeof(GifCard),
            new PropertyMetadata(256, HandleDecodePixelSizeChanged));
    public int DecodePixelSize
    {
        get => (int)GetValue(DecodePixelSizeProperty);
        set => SetValue(DecodePixelSizeProperty, value);
    }
    private static void HandleDecodePixelSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        GifCard card = (GifCard)sender;
        card.ResetPreview();
        card.RefreshThumbnail();
        card.UpdatePreviewPlayback();
    }

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

    public static readonly DependencyProperty LoadThumbnailCommandProperty =
    DependencyProperty.Register(nameof(LoadThumbnailCommand), typeof(ICommand),
    typeof(GifCard), new PropertyMetadata(null, HandleLoadThumbnailCommandChanged));

    public static readonly DependencyProperty StartPreviewCommandProperty =
    DependencyProperty.Register(nameof(StartPreviewCommand), typeof(ICommand),
    typeof(GifCard), new PropertyMetadata(null, HandleStartPreviewCommandChanged));

    public static readonly DependencyProperty StopPreviewCommandProperty =
    DependencyProperty.Register(nameof(StopPreviewCommand), typeof(ICommand),
    typeof(GifCard), new PropertyMetadata(null));

    public ICommand? LoadThumbnailCommand
    {
        get => GetValue(LoadThumbnailCommandProperty) as ICommand;
        set => SetValue(LoadThumbnailCommandProperty, value);
    }

    public ICommand? StartPreviewCommand
    {
        get => GetValue(StartPreviewCommandProperty) as ICommand;
        set => SetValue(StartPreviewCommandProperty, value);
    }

    public ICommand? StopPreviewCommand
    {
        get => GetValue(StopPreviewCommandProperty) as ICommand;
        set => SetValue(StopPreviewCommandProperty, value);
    }

    private ICommand? _observedThumbnailCommand;
    private bool _thumbnailRequested;
    private CancellationTokenSource? _thumbnailCancellation;
    private CancellationTokenSource? _previewCancellation;
    private ICommand? _observedStartPreviewCommand;
    private bool _previewRequested;
    private XamlRoot? _observedRoot;
    private bool _isLoaded;
    private bool _inViewport = true;

    private int _thumbnailVersion;

    private BitmapImage? _previewBitmap;

    private bool _isPointerOver;


    private bool _isPreviewReady;

    public GifCard()
    {
        InitializeComponent();
        Loaded += Root_Loaded;
        EffectiveViewportChanged += (_, args) =>
        {
            var viewport = args.EffectiveViewport;
            _inViewport = viewport.Width > 0 && viewport.Height > 0 &&
                viewport.Right > 0 && viewport.Bottom > 0 && viewport.Left < ActualWidth && viewport.Top < ActualHeight;
            if (!_inViewport)
            {
                _isPointerOver = false;
                _previewRequested = false;
                ExecuteIfAvailable(StopPreviewCommand);
                ResetPreview();
                ReleaseThumbnail();
            }
            else if (_isLoaded)
            {
                RequestThumbnail();
                if (ThumbnailImage.Source is null && _thumbnailCancellation is null) RefreshThumbnail();
            }
        };

        Loaded += (_, _) => UpdateFavoriteState();

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
    DependencyPropertyChangedEventArgs eventArgs)
    {
        GifCard card = (GifCard)sender;
        if (!card.AnimatePreview || !card.SystemAnimationsEnabled)
        {
            card._previewRequested = false;
            ExecuteIfAvailable(card.StopPreviewCommand);
            card.ResetPreview();
        }
        else
        {
            card.RequestPreview();
        }
        card.UpdatePreviewPlayback();
    }

    private static void HandleStartPreviewCommandChanged(
    DependencyObject sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        GifCard card = (GifCard)sender;
        card.ObserveStartPreviewCommand();
        card.RequestPreview();
    }

    private void ObserveStartPreviewCommand()
    {
        if (_observedStartPreviewCommand is not null)
        {
            _observedStartPreviewCommand.CanExecuteChanged -= StartPreview_CanExecuteChanged;
        }
        _observedStartPreviewCommand = _isLoaded ? StartPreviewCommand : null;
        if (_observedStartPreviewCommand is not null)
        {
            _observedStartPreviewCommand.CanExecuteChanged += StartPreview_CanExecuteChanged;
        }
    }

    private void StartPreview_CanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        RequestPreview();
    }

    private async void SelectButton_Click(object sender, RoutedEventArgs args)
    {
        CopyGIF.Core.Models.RepairDiagnostics.Record("card-click", ProviderName, "received");
        if (SelectCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand command && command.CanExecute(SelectCommandParameter))
        {
            await command.ExecuteAsync(SelectCommandParameter);
        }
        else
        {
            CopyGIF.Core.Models.RepairDiagnostics.Record("card-click", ProviderName, "command-unavailable");
        }
    }

    private static void HandleLoadThumbnailCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        GifCard card = (GifCard)sender;
        card._thumbnailRequested = false;
        card.ObserveThumbnailCommand();
        card.RequestThumbnail();
    }

    private void ObserveThumbnailCommand()
    {
        if (_observedThumbnailCommand is not null)
            _observedThumbnailCommand.CanExecuteChanged -= ThumbnailCommandChanged;
        _observedThumbnailCommand = _isLoaded ? LoadThumbnailCommand : null;
        if (_observedThumbnailCommand is not null)
            _observedThumbnailCommand.CanExecuteChanged += ThumbnailCommandChanged;
    }

    private void ThumbnailCommandChanged(object? sender, EventArgs args) => RequestThumbnail();

    private async void RequestThumbnail()
    {
        if (!_isLoaded || !_inViewport || _thumbnailRequested || XamlRoot?.IsHostVisible != true ||
            LoadThumbnailCommand is not CommunityToolkit.Mvvm.Input.IAsyncRelayCommand command || !command.CanExecute(null)) return;
        _thumbnailRequested = true;
        try
        {
            await command.ExecuteAsync(null);
            if (_isLoaded && _inViewport)
            {
                await RefreshThumbnailAsync();
                if (ThumbnailUri is null) RetryThumbnailButton.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            CopyGIF.Core.Models.RepairDiagnostics.Record("card-thumbnail", ProviderName, exception.GetType().Name);
            if (_isLoaded) RetryThumbnailButton.Visibility = Visibility.Visible;
        }
    }

    private void ReleaseThumbnail()
    {
        _thumbnailVersion++;
        _thumbnailCancellation?.Cancel();
        ThumbnailImage.Source = null;
    }

    private void SelectButton_PointerEntered(
    object sender,
    PointerRoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _isPointerOver = true;
        RequestPreview();
        UpdatePreviewPlayback();
    }

    private void SelectButton_PointerExited(
    object sender,
    PointerRoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        _isPointerOver = false;
        StopInactivePreview();
        UpdatePreviewPlayback();
    }

    private void SelectButton_GotFocus(
    object sender,
    RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        RequestPreview();
        UpdatePreviewPlayback();
    }

    private void SelectButton_LostFocus(
    object sender,
    RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        StopInactivePreview();
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

        _isLoaded = false;
        _thumbnailRequested = false;
        ObserveThumbnailCommand();
        (LoadThumbnailCommand as CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)?.Cancel();
        ReleaseThumbnail();
        _previewRequested = false;
        ObserveStartPreviewCommand();
        _thumbnailVersion++;
        ThumbnailImage.Source = null;
        _isPointerOver = false;
        if (_observedRoot is not null)
        {
            _observedRoot.Changed -= Root_Changed;
            _observedRoot = null;
        }
        ExecuteIfAvailable(StopPreviewCommand);
        ResetPreview();
    }

    private void Root_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        _isLoaded = true;
        ObserveThumbnailCommand();
        ObserveStartPreviewCommand();
        if (_observedRoot is not null)
        {
            _observedRoot.Changed -= Root_Changed;
        }
        _observedRoot = XamlRoot;
        if (_observedRoot is not null)
        {
            _observedRoot.Changed += Root_Changed;
        }
        RequestThumbnail();
        RefreshThumbnail();
        RequestPreview();
    }

    private void Root_Changed(XamlRoot sender, XamlRootChangedEventArgs eventArgs)
    {
        if (!sender.IsHostVisible)
        {
            _previewRequested = false;
            _isPointerOver = false;
                ExecuteIfAvailable(StopPreviewCommand);
            ResetPreview();
            ReleaseThumbnail();
        }
        else
        {
            RequestThumbnail();
            if (ThumbnailImage.Source is null && _thumbnailCancellation is null) RefreshThumbnail();
        }
    }

    private void RequestPreview()
    {
        if (_isLoaded && _inViewport && !_previewRequested && XamlRoot?.IsHostVisible == true &&
        AnimatePreview && SystemAnimationsEnabled &&
        _isPointerOver &&
        StartPreviewCommand?.CanExecute(null) == true)
        {
            _previewRequested = true;
            StartPreviewCommand.Execute(null);
        }
    }

    private void StopInactivePreview()
    {
        if (!_isPointerOver)
        {
            _previewRequested = false;
            ExecuteIfAvailable(StopPreviewCommand);
            ResetPreview();
        }
    }

    private static void ExecuteIfAvailable(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private static bool IsSafeLocalSource(Uri? uri)
    {
        return CopyGIF.Core.Policies.ProviderMediaPolicy.IsDirectMediaUri(uri) ||
            uri is { IsAbsoluteUri: true, IsFile: true, IsUnc: false } &&
        string.IsNullOrEmpty(uri.Host) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    }

    private async void RetryThumbnail_Click(object sender, RoutedEventArgs args)
    {
        if (LoadThumbnailCommand is not CommunityToolkit.Mvvm.Input.IAsyncRelayCommand command || !command.CanExecute(true)) return;
        RetryThumbnailButton.IsEnabled = false;
        try { await command.ExecuteAsync(true); await RefreshThumbnailAsync(); }
        catch (Exception) { RetryThumbnailButton.Visibility = Visibility.Visible; }
        finally { RetryThumbnailButton.IsEnabled = true; }
    }

    private void RefreshThumbnail()
    {
        _ = RefreshThumbnailAsync();
    }

    private async Task RefreshThumbnailAsync()
    {
        int version = ++_thumbnailVersion;
        _thumbnailCancellation?.Cancel();
        Uri? thumbnailUri = ThumbnailUri;
        ThumbnailImage.Source = null;
        PlaceholderIcon.Visibility = Visibility.Visible;
        RetryThumbnailButton.Visibility = Visibility.Collapsed;

        if (!_isLoaded || !_inViewport || XamlRoot?.IsHostVisible != true || thumbnailUri is null) return;
        if (!IsSafeLocalSource(thumbnailUri))
        {
            RetryThumbnailButton.Visibility = Visibility.Visible;
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        BitmapImage bitmap = CreateBitmap();
        bool loaded;
        try { loaded = await LocalBitmapLoader.TryLoadAsync(bitmap, thumbnailUri, cancellation.Token); }
        finally { if (ReferenceEquals(_thumbnailCancellation, cancellation)) _thumbnailCancellation = null; }
        if (cancellation.IsCancellationRequested) return;
        if (version != _thumbnailVersion || !_isLoaded) return;
        if (!loaded) { RetryThumbnailButton.Visibility = Visibility.Visible; return; }

        ThumbnailImage.Source = bitmap;
        PlaceholderIcon.Visibility = Visibility.Collapsed;
    }

    private BitmapImage CreateBitmap() => new BitmapImage()
    {
        AutoPlay = false,
        DecodePixelWidth = DecodeByHeight ? 0 : Math.Clamp(DecodePixelSize, 128, 512),
        DecodePixelHeight = DecodeByHeight ? Math.Clamp(DecodePixelSize, 128, 512) : 0
    };

    private void EnsurePreviewLoaded()
    {
        if (_previewBitmap is not null ||
        PreviewUri is not Uri previewUri || !IsSafeLocalSource(previewUri))
        {
            return;
        }

        BitmapImage bitmap = CreateBitmap();
        _previewBitmap = bitmap;
        _ = LoadPreviewAsync(bitmap, previewUri);
    }

    private async Task LoadPreviewAsync(BitmapImage bitmap, Uri previewUri)
    {
        using var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        bool loaded;
        try { loaded = await LocalBitmapLoader.TryLoadAsync(bitmap, previewUri, cancellation.Token); }
        finally { if (ReferenceEquals(_previewCancellation, cancellation)) _previewCancellation = null; }
        if (cancellation.IsCancellationRequested) return;
        if (!ReferenceEquals(_previewBitmap, bitmap) || !_isLoaded)
        {
            return;
        }

        if (!loaded)
        {
            ResetPreview();
            return;
        }

        AnimatedImage.Source = bitmap;
        _isPreviewReady = true;
        UpdatePreviewPlayback();
    }

    private void UpdatePreviewPlayback()
    {
        bool shouldPlay =
        _isLoaded && _inViewport && XamlRoot?.IsHostVisible == true &&
        CanAnimatePreview &&
        _isPointerOver;

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
        _previewCancellation?.Cancel();
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
        VisualStateManager.GoToState(this, IsFavorite ? "Favorite" : "NotFavorite", false);
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
        string providerDisplayName = string.Equals(ProviderName?.Trim(), "klipy", StringComparison.OrdinalIgnoreCase)
            ? "KLIPY"
            : ProviderName?.Trim() ?? string.Empty;
        ProviderTextBlock.Text = providerDisplayName;
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
        : $"Provided by {providerDisplayName}. Press Enter to copy this GIF.");

        ProviderTextBlock.Visibility =
        string.IsNullOrWhiteSpace(
        ProviderName)
        ? Visibility.Collapsed
        : Visibility.Visible;
    }
}
