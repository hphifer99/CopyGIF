using CopyGIF.Models;
using CopyGIF.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CopyGIF
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly KlipyClient _klipyClient;
        private readonly SettingsService _settingsService;
        private readonly ClipboardService _clipboardService;
        private readonly GifLibraryService _libraryService;
        private readonly HotkeyService _hotkeyService;
        private readonly PreviewCacheService _previewCacheService;
        private readonly StartupRegistrationService
            _startupRegistrationService;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _statusClearTimer;

        private AppSettings _settings;
        private List<GifItem> _lastSearchResults =
            new List<GifItem>();
        private PickerView _currentView = PickerView.Search;
        private CancellationTokenSource _searchCancellation;
        private CancellationTokenSource _copyCancellation;
        private CancellationTokenSource _favoriteCancellation;
        private CancellationTokenSource _hoverPreviewCancellation;
        private bool _suppressAutoHide;
        private bool _isOperationBusy;
        private bool _isClosing;
        private string _temporaryStatusMessage;
        private SettingsWindow _settingsWindow;
        private GifItem _activePreviewItem;

        public bool NeedsApiKeySetup =>
            _settings == null ||
            string.IsNullOrWhiteSpace(_settings.ApiKey);

        public MainWindow()
        {
            InitializeComponent();

            _searchDebounceTimer = new DispatcherTimer();
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            _statusClearTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _statusClearTimer.Tick += StatusClearTimer_Tick;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(25)
            };

            Version applicationVersion =
                typeof(MainWindow).Assembly.GetName().Version;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "CopyGIF/" + applicationVersion.ToString(3));

            _klipyClient = new KlipyClient(_httpClient);
            _settingsService = new SettingsService();
            _clipboardService = new ClipboardService(_httpClient);
            _previewCacheService =
                new PreviewCacheService(_httpClient);
            _startupRegistrationService =
                new StartupRegistrationService();
            _libraryService = new GifLibraryService(
                _settingsService,
                _clipboardService);
            _hotkeyService = new HotkeyService();

            LoadSettingsAndLibrary();
            ApplyRuntimeSettings();

            if (!TryApplyStartupSetting(out string startupError) &&
                !NeedsApiKeySetup)
            {
                StatusTextBlock.Text = startupError;
            }

            ShowView(PickerView.Search);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                RegisterHotkey(_settings.Hotkey);
            }
            catch (Exception exception)
            {
                StatusTextBlock.Text = exception.Message;

                Dispatcher.BeginInvoke(
                    new Action(ShowPicker),
                    DispatcherPriority.ApplicationIdle);
            }
        }

        private void LoadSettingsAndLibrary()
        {
            try
            {
                _settings = _settingsService.LoadOrCreate();
            }
            catch (Exception)
            {
                _settings = AppSettings.CreateDefault();
                StatusTextBlock.Text =
                    "Settings could not be loaded. Defaults are in use.";
            }

            try
            {
                _libraryService.Load();
            }
            catch (Exception)
            {
                StatusTextBlock.Text =
                    "Favorites and Recents could not be loaded.";
            }

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                StatusTextBlock.Text =
                    "Add your API key in Settings to begin.";
            }
            else if (string.IsNullOrWhiteSpace(StatusTextBlock.Text))
            {
                StatusTextBlock.Text = "Ready to search.";
            }
        }

        private void ApplyRuntimeSettings()
        {
            _settings.Normalize();

            _searchDebounceTimer.Interval =
                TimeSpan.FromMilliseconds(
                    _settings.SearchDebounceMilliseconds);

            HotkeyHintTextBlock.Text =
                _settings.Hotkey + " to toggle | Esc to hide";

            UpdateTabLabels();
        }

        private void RegisterHotkey(string hotkey)
        {
            _hotkeyService.Register(
                this,
                hotkey,
                TogglePicker);
        }

        private void TogglePicker()
        {
            if (_suppressAutoHide)
            {
                return;
            }

            if (IsVisible && WindowState != WindowState.Minimized)
            {
                HideApplication();
            }
            else
            {
                ShowPicker();
            }
        }

        public void ShowPicker()
        {
            ShowPicker(true);
        }

        private void ShowPicker(bool focusSearch)
        {
            bool wasVisible =
                IsVisible && WindowState != WindowState.Minimized;

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (!wasVisible)
            {
                ApplyWindowPlacement();

                if (!IsVisible)
                {
                    Show();
                }
            }

            Activate();

            if (!focusSearch)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (_currentView == PickerView.Search)
                    {
                        SearchTextBox.Focus();
                        Keyboard.Focus(SearchTextBox);
                        SearchTextBox.SelectAll();
                    }
                }),
                DispatcherPriority.Input);
        }

        public void OpenSettingsFromTray()
        {
            ShowPicker(false);
            OpenSettings();
        }

        public void ShowInitialApiKeySetup()
        {
            ShowApiKeySetup(
                "Welcome to CopyGIF. Add a KLIPY API key to begin.");
        }

        private void ShowApiKeySetup(string message)
        {
            ShowPicker(false);
            StatusTextBlock.Text = message;
            OpenSettings(true);
        }

        private void HideApplication()
        {
            _searchDebounceTimer.Stop();
            _searchCancellation?.Cancel();
            CancelHoverPreviewRequest();
            StopActivePreview();
            SaveWindowGeometry();
            Hide();
        }

        private void ApplyWindowPlacement()
        {
            if (_settings.RememberWindowSize)
            {
                Width = _settings.WindowWidth;
                Height = _settings.WindowHeight;
            }

            Matrix fromDevice = GetTransformFromDevice();
            Matrix toDevice = GetTransformToDevice();
            var cursorPixel = System.Windows.Forms.Cursor.Position;

            bool useRememberedPosition =
                string.Equals(
                    _settings.WindowPlacementMode,
                    "Remember",
                    StringComparison.OrdinalIgnoreCase) &&
                _settings.HasSavedWindowPlacement;

            System.Windows.Forms.Screen activeScreen;

            if (useRememberedPosition)
            {
                Point rememberedCenterPixel = toDevice.Transform(
                    new Point(
                        _settings.WindowLeft + Width / 2,
                        _settings.WindowTop + Height / 2));

                activeScreen = System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point(
                        (int)Math.Round(rememberedCenterPixel.X),
                        (int)Math.Round(rememberedCenterPixel.Y)));
            }
            else
            {
                activeScreen =
                    System.Windows.Forms.Screen.FromPoint(cursorPixel);
            }

            Point cursor = fromDevice.Transform(
                new Point(cursorPixel.X, cursorPixel.Y));

            System.Drawing.Rectangle workingPixels =
                activeScreen.WorkingArea;

            Point workingTopLeft = fromDevice.Transform(
                new Point(
                    workingPixels.Left,
                    workingPixels.Top));

            Point workingBottomRight = fromDevice.Transform(
                new Point(
                    workingPixels.Right,
                    workingPixels.Bottom));

            var workingArea = new Rect(
                workingTopLeft,
                workingBottomRight);

            double proposedLeft;
            double proposedTop;

            if (useRememberedPosition)
            {
                proposedLeft = _settings.WindowLeft;
                proposedTop = _settings.WindowTop;
            }
            else if (string.Equals(
                         _settings.WindowPlacementMode,
                         "Center",
                         StringComparison.OrdinalIgnoreCase))
            {
                proposedLeft =
                    workingArea.Left +
                    (workingArea.Width - Width) / 2;

                proposedTop =
                    workingArea.Top +
                    (workingArea.Height - Height) / 2;
            }
            else
            {
                const double offset = 14;

                proposedLeft = cursor.X + offset;
                proposedTop = cursor.Y + offset;

                if (proposedLeft + Width > workingArea.Right)
                {
                    proposedLeft = cursor.X - Width - offset;
                }

                if (proposedTop + Height > workingArea.Bottom)
                {
                    proposedTop = cursor.Y - Height - offset;
                }
            }

            Left = ClampToWorkingArea(
                proposedLeft,
                workingArea.Left,
                workingArea.Right - Width);

            Top = ClampToWorkingArea(
                proposedTop,
                workingArea.Top,
                workingArea.Bottom - Height);
        }

        private Matrix GetTransformFromDevice()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);

            return source?.CompositionTarget?.TransformFromDevice
                ?? Matrix.Identity;
        }

        private Matrix GetTransformToDevice()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);

            return source?.CompositionTarget?.TransformToDevice
                ?? Matrix.Identity;
        }

        private static double ClampToWorkingArea(
            double value,
            double minimum,
            double maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void SaveWindowGeometry()
        {
            if (_settings == null)
            {
                return;
            }

            Rect bounds = WindowState == WindowState.Normal
                ? new Rect(
                    Left,
                    Top,
                    ActualWidth,
                    ActualHeight)
                : RestoreBounds;

            if (_settings.RememberWindowSize &&
                bounds.Width >= MinWidth &&
                bounds.Height >= MinHeight)
            {
                _settings.WindowWidth = bounds.Width;
                _settings.WindowHeight = bounds.Height;
            }

            if (string.Equals(
                    _settings.WindowPlacementMode,
                    "Remember",
                    StringComparison.OrdinalIgnoreCase))
            {
                _settings.WindowLeft = bounds.Left;
                _settings.WindowTop = bounds.Top;
                _settings.HasSavedWindowPlacement = true;
            }

            try
            {
                _settingsService.Save(_settings);
            }
            catch (Exception)
            {
                StatusTextBlock.Text =
                    "Window position could not be saved.";
            }
        }

        private void Window_Deactivated(
            object sender,
            EventArgs e)
        {
            if (IsVisible &&
                !_suppressAutoHide &&
                _settings.CloseWhenFocusLost)
            {
                HideApplication();
            }
        }

        private void TitleBar_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            Point pointerPosition = e.GetPosition(this);

            if (e.ChangedButton != MouseButton.Left ||
                pointerPosition.Y > 52 ||
                FindAncestor<Button>(
                    e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            try
            {
                DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _isClosing = true;
            base.OnClosing(e);
        }

        private void ResultsListBox_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            const double preferredTileWidth = 220;

            double usableWidth =
                e.NewSize.Width -
                SystemParameters.VerticalScrollBarWidth -
                8;

            int columns = Math.Max(
                1,
                (int)(usableWidth / preferredTileWidth));

            ResultsListBox.Tag = columns;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                HideApplication();
                return;
            }

            if (e.Key == Key.OemComma &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                OpenSettings();
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void SearchTabButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowView(PickerView.Search);
        }

        private void FavoritesTabButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowView(PickerView.Favorites);
        }

        private void RecentsTabButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowView(PickerView.Recents);
        }

        private void ShowView(PickerView view)
        {
            CancelHoverPreviewRequest();
            StopActivePreview();

            if (view != PickerView.Search)
            {
                _searchDebounceTimer.Stop();
                _searchCancellation?.Cancel();
                ResultsListBox.IsEnabled = !_isOperationBusy;
            }

            _currentView = view;

            SearchTabButton.IsChecked =
                view == PickerView.Search;

            FavoritesTabButton.IsChecked =
                view == PickerView.Favorites;

            RecentsTabButton.IsChecked =
                view == PickerView.Recents;

            SearchPanel.Visibility =
                view == PickerView.Search
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ClearRecentsButton.Visibility =
                view == PickerView.Recents
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            switch (view)
            {
                case PickerView.Favorites:
                    DisplayItems(
                        _libraryService.Favorites,
                        "No favorites yet. Select the star on any GIF to save it.");

                    StatusTextBlock.Text =
                        "Your saved GIFs appear here.";
                    break;

                case PickerView.Recents:
                    DisplayItems(
                        _libraryService.Recents,
                        "No recent GIFs yet.");

                    StatusTextBlock.Text =
                        "GIFs you copy appear here.";
                    break;

                default:
                    DisplayItems(
                        _lastSearchResults,
                        string.IsNullOrWhiteSpace(
                            SearchTextBox.Text)
                            ? "Type above to search for GIFs."
                            : "No GIFs found.");

                    break;
            }

            UpdateTabLabels();
        }

        private void DisplayItems(
            IEnumerable<GifItem> items,
            string emptyMessage)
        {
            CancelHoverPreviewRequest();
            StopActivePreview();

            List<GifItem> materialized =
                (items ?? Enumerable.Empty<GifItem>())
                .ToList();

            _libraryService.MarkFavoriteState(materialized);

            foreach (GifItem item in materialized)
            {
                item.SetAnimatedPreviewEnabled(false);
            }

            ResultsListBox.ItemsSource = materialized;

            EmptyStateTextBlock.Text = emptyMessage;
            EmptyStateTextBlock.Visibility =
                materialized.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UpdateTabLabels()
        {
            FavoritesTabButton.Content =
                "Favorites (" +
                _libraryService.Favorites.Count +
                ")";

            RecentsTabButton.Content =
                "Recents (" +
                _libraryService.Recents.Count +
                ")";
        }

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_searchDebounceTimer == null ||
                _currentView != PickerView.Search)
            {
                return;
            }

            _searchDebounceTimer.Stop();
            _searchCancellation?.Cancel();
            CancelHoverPreviewRequest();
            StopActivePreview();

            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                _lastSearchResults.Clear();

                DisplayItems(
                    _lastSearchResults,
                    "Type above to search for GIFs.");

                StatusTextBlock.Text = "Type to search.";
                return;
            }

            _searchDebounceTimer.Start();
        }

        private async void SearchDebounceTimer_Tick(
            object sender,
            EventArgs e)
        {
            _searchDebounceTimer.Stop();
            await SearchAsync();
        }

        private async void SearchButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            await SearchAsync();
        }

        private async void SearchTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _searchDebounceTimer.Stop();
                await SearchAsync();
            }
        }

        private async Task SearchAsync()
        {
            if (_isOperationBusy)
            {
                return;
            }

            string query = SearchTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                ShowApiKeySetup(
                    "Add a KLIPY API key in Settings to begin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                _lastSearchResults.Clear();

                DisplayItems(
                    _lastSearchResults,
                    "Type above to search for GIFs.");

                StatusTextBlock.Text = "Type to search.";
                return;
            }

            _searchDebounceTimer.Stop();
            _searchCancellation?.Cancel();
            CancelHoverPreviewRequest();
            StopActivePreview();

            var currentSearch =
                new CancellationTokenSource();

            _searchCancellation = currentSearch;

            SearchButton.IsHitTestVisible = false;
            ResultsListBox.IsHitTestVisible = false;
            StatusTextBlock.Text = "Searching...";

            try
            {
                IReadOnlyList<GifItem> results =
                    await _klipyClient.SearchAsync(
                        _settings.ApiKey,
                        query,
                        _settings.ResultsPerSearch,
                        currentSearch.Token);

                currentSearch.Token
                    .ThrowIfCancellationRequested();

                _lastSearchResults = results.ToList();

                _libraryService.MarkFavoriteState(
                    _lastSearchResults);

                if (_currentView == PickerView.Search)
                {
                    DisplayItems(
                        _lastSearchResults,
                        "No GIFs found.");

                    StatusTextBlock.Text =
                        _lastSearchResults.Count == 0
                            ? "No GIFs found."
                            : "Found " +
                              _lastSearchResults.Count +
                              " GIFs.";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                if (ReferenceEquals(
                        _searchCancellation,
                        currentSearch))
                {
                    StatusTextBlock.Text =
                        "The API key was not accepted. Update it in Settings.";

                    ShowApiKeySetup(
                        "The KLIPY API key was not accepted. Enter a valid key.");
                }
            }
            catch (KlipyRateLimitException)
            {
                if (ReferenceEquals(
                        _searchCancellation,
                        currentSearch))
                {
                    StatusTextBlock.Text =
                        "KLIPY's API limit was reached. Wait a while and try again.";
                }
            }
            catch (HttpRequestException)
            {
                if (ReferenceEquals(
                        _searchCancellation,
                        currentSearch))
                {
                    StatusTextBlock.Text =
                        "Could not reach KLIPY. Check your connection and try again.";
                }
            }
            catch (Exception)
            {
                if (ReferenceEquals(
                        _searchCancellation,
                        currentSearch))
                {
                    StatusTextBlock.Text =
                        "Search failed. Please try again.";
                }
            }
            finally
            {
                if (ReferenceEquals(
                        _searchCancellation,
                        currentSearch))
                {
                    _searchCancellation = null;
                    SearchButton.IsHitTestVisible = !_isOperationBusy;
                    ResultsListBox.IsHitTestVisible = !_isOperationBusy;
                }

                currentSearch.Dispose();
            }
        }

        private async void GifTile_MouseEnter(
            object sender,
            MouseEventArgs e)
        {
            if (!_settings.AnimatePreviews)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var gif = element?.DataContext as GifItem;

            if (element == null ||
                gif == null)
            {
                return;
            }

            CancelHoverPreviewRequest();
            StopActivePreview();

            var hoverCancellation =
                new CancellationTokenSource();

            _hoverPreviewCancellation =
                hoverCancellation;

            try
            {
                await EnsurePreviewCachedAsync(
                    gif,
                    hoverCancellation.Token);

                hoverCancellation.Token
                    .ThrowIfCancellationRequested();

                if (!element.IsMouseOver ||
                    !ReferenceEquals(
                        element.DataContext,
                        gif) ||
                    !_settings.AnimatePreviews)
                {
                    return;
                }

                _activePreviewItem = gif;
                gif.SetAnimatedPreviewEnabled(true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // The static thumbnail remains available.
            }
            finally
            {
                if (ReferenceEquals(
                        _hoverPreviewCancellation,
                        hoverCancellation))
                {
                    _hoverPreviewCancellation = null;
                }

                hoverCancellation.Dispose();
            }
        }

        private void GifTile_MouseLeave(
            object sender,
            MouseEventArgs e)
        {
            var element = sender as FrameworkElement;
            var gif = element?.DataContext as GifItem;

            CancelHoverPreviewRequest();

            if (ReferenceEquals(
                    _activePreviewItem,
                    gif))
            {
                StopActivePreview();
            }
            else
            {
                gif?.SetAnimatedPreviewEnabled(false);
            }
        }

        private void StopActivePreview()
        {
            GifItem activeItem =
                _activePreviewItem;

            _activePreviewItem = null;

            if (activeItem == null)
            {
                return;
            }

            activeItem.SetAnimatedPreviewEnabled(false);
        }

        private async Task EnsurePreviewCachedAsync(
            GifItem gif,
            CancellationToken cancellationToken)
        {
            if (gif == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    gif.LocalPreviewFilePath) &&
                File.Exists(
                    gif.LocalPreviewFilePath))
            {
                return;
            }

            string previewUrl = gif.PreviewGifUrl;

            if (string.IsNullOrWhiteSpace(previewUrl))
            {
                if (!string.IsNullOrWhiteSpace(
                        gif.LocalFilePath) &&
                    File.Exists(gif.LocalFilePath))
                {
                    return;
                }

                previewUrl = gif.FullGifUrl;
            }

            if (string.IsNullOrWhiteSpace(previewUrl))
            {
                return;
            }

            string localPreviewPath =
                await _previewCacheService
                    .GetLocalPreviewAsync(
                        previewUrl,
                        cancellationToken);

            cancellationToken
                .ThrowIfCancellationRequested();

            gif.LocalPreviewFilePath =
                localPreviewPath;
        }

        private void CancelHoverPreviewRequest()
        {
            _hoverPreviewCancellation?.Cancel();
        }

        private async void GifTile_PreviewMouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (FindAncestor<Button>(
                    e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            var listBoxItem = sender as ListBoxItem;
            var gif = listBoxItem?.DataContext as GifItem;

            if (gif == null)
            {
                return;
            }

            e.Handled = true;
            await CopyGifAsync(gif);
        }

        private async Task CopyGifAsync(GifItem gif)
        {
            if (_copyCancellation != null ||
                _isOperationBusy)
            {
                return;
            }

            _copyCancellation =
                new CancellationTokenSource();

            SetBusy(
                true,
                "Copying GIF...");

            try
            {
                string copiedFilePath =
                    await _clipboardService
                        .DownloadAndCopyAsync(
                            gif,
                            _copyCancellation.Token);

                bool recentSaveFailed = false;

                try
                {
                    await _libraryService.AddRecentAsync(
                        gif,
                        copiedFilePath,
                        _settings,
                        _copyCancellation.Token);
                }
                catch (Exception)
                {
                    recentSaveFailed = true;
                }

                UpdateTabLabels();

                StatusTextBlock.Text = recentSaveFailed
                    ? "GIF copied, but it could not be added to Recents."
                    : "GIF copied. Paste it with Ctrl+V.";

                if (_settings.HideAfterCopy)
                {
                    HideApplication();
                }
                else if (_currentView == PickerView.Recents)
                {
                    ShowView(PickerView.Recents);
                }
            }
            catch (OperationCanceledException)
            {
                StatusTextBlock.Text =
                    "Copy canceled.";
            }
            catch (Exception)
            {
                StatusTextBlock.Text =
                    "Could not copy this GIF. Try another GIF.";
            }
            finally
            {
                SetBusy(false, null);
                _copyCancellation.Dispose();
                _copyCancellation = null;
            }
        }

        private async void FavoriteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            e.Handled = true;

            if (_favoriteCancellation != null ||
                _isOperationBusy)
            {
                return;
            }

            var button = sender as Button;
            var gif = button?.Tag as GifItem;

            if (gif == null)
            {
                return;
            }

            _favoriteCancellation =
                new CancellationTokenSource();

            SetBusy(
                true,
                gif.IsFavorite
                    ? "Removing favorite..."
                    : "Adding favorite...");

            try
            {
                bool isFavorite =
                    await _libraryService
                        .ToggleFavoriteAsync(
                            gif,
                            _settings,
                            _favoriteCancellation.Token);

                _libraryService.MarkFavoriteState(
                    _lastSearchResults);

                UpdateTabLabels();

                StatusTextBlock.Text = isFavorite
                    ? "Added to Favorites."
                    : "Removed from Favorites.";

                if (_currentView == PickerView.Favorites)
                {
                    ShowView(PickerView.Favorites);
                }
                else if (_currentView == PickerView.Recents)
                {
                    ShowView(PickerView.Recents);
                }
            }
            catch (OperationCanceledException)
            {
                StatusTextBlock.Text =
                    "Favorite update canceled.";
            }
            catch (Exception)
            {
                StatusTextBlock.Text =
                    "Could not update Favorites. Please try again.";
            }
            finally
            {
                SetBusy(false, null);
                _favoriteCancellation.Dispose();
                _favoriteCancellation = null;
            }
        }

        private void ClearRecentsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_libraryService.Recents.Count == 0)
            {
                return;
            }

            _suppressAutoHide = true;

            try
            {
                MessageBoxResult answer =
                    MessageBox.Show(
                        this,
                        "Clear all recent GIFs? Favorites will not be affected.",
                        "Clear Recents",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }

                _libraryService.ClearRecents();
                ShowView(PickerView.Recents);
                StatusTextBlock.Text =
                    "Recents cleared.";
            }
            catch (Exception)
            {
                StatusTextBlock.Text =
                    "Could not clear Recents. Please try again.";
            }
            finally
            {
                _suppressAutoHide = false;
                Activate();
            }
        }

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenSettings();
        }

        public void OpenSettings()
        {
            OpenSettings(false);
        }

        private void OpenSettings(bool isApiKeySetup)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            if (_isOperationBusy)
            {
                return;
            }

            _searchDebounceTimer.Stop();
            _searchCancellation?.Cancel();
            CancelHoverPreviewRequest();
            StopActivePreview();
            _suppressAutoHide = true;

            _settingsWindow = new SettingsWindow(
                _settings.Clone(),
                SaveSettingsAsync,
                _settings.CloseWhenFocusLost,
                isApiKeySetup)
            {
                Owner = this
            };

            _settingsWindow.Closed +=
                SettingsWindow_Closed;

            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void SettingsWindow_Closed(
            object sender,
            EventArgs e)
        {
            var closedWindow =
                sender as SettingsWindow;

            if (closedWindow != null)
            {
                closedWindow.Closed -=
                    SettingsWindow_Closed;
            }

            bool closedBecauseFocusLost =
                closedWindow?.ClosedBecauseFocusLost == true;

            _settingsWindow = null;
            _suppressAutoHide = false;

            if (_isClosing)
            {
                return;
            }

            if (closedBecauseFocusLost)
            {
                if (_settings.CloseWhenFocusLost)
                {
                    HideApplication();
                }

                return;
            }

            if (IsVisible)
            {
                Activate();
            }
        }

        private async Task<SettingsSaveResult>
            SaveSettingsAsync(
                AppSettings candidate,
                CancellationToken cancellationToken)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(
                    nameof(candidate));
            }

            string validationError =
                candidate.ValidateForSave();

            if (validationError != null)
            {
                throw new InvalidOperationException(
                    validationError);
            }

            candidate.ApiKey =
                (candidate.ApiKey ?? string.Empty)
                .Trim();

            if (string.IsNullOrWhiteSpace(candidate.ApiKey))
            {
                return new SettingsSaveResult(
                    false,
                    "Enter a KLIPY API key before saving.",
                    _settings.ApiKey);
            }

            candidate.Hotkey =
                HotkeyService.Normalize(
                    candidate.Hotkey);

            AppSettings previousSettings =
                _settings.Clone();

            bool apiKeyChanged = !string.Equals(
                previousSettings.ApiKey,
                candidate.ApiKey,
                StringComparison.Ordinal);

            bool apiKeyRejected = false;
            bool apiKeyVerificationUnavailable = false;

            if (apiKeyChanged)
            {
                try
                {
                    await _klipyClient
                        .ValidateApiKeyAsync(
                            candidate.ApiKey,
                            cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (UnauthorizedAccessException)
                {
                    apiKeyRejected = true;
                }
                catch (KlipyRateLimitException)
                {
                    apiKeyVerificationUnavailable = true;
                }
                catch (HttpRequestException)
                {
                    apiKeyVerificationUnavailable = true;
                }
                catch (Exception)
                {
                    apiKeyVerificationUnavailable = true;
                }

                if (apiKeyRejected)
                {
                    candidate.ApiKey =
                        previousSettings.ApiKey;
                }
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            CaptureExistingGeometry(candidate);

            bool previousWasRegistered =
                _hotkeyService.IsRegistered;

            bool registrationChanged =
                !previousWasRegistered ||
                !string.Equals(
                    previousSettings.Hotkey,
                    candidate.Hotkey,
                    StringComparison.OrdinalIgnoreCase);

            if (registrationChanged)
            {
                _hotkeyService.Unregister();

                try
                {
                    RegisterHotkey(candidate.Hotkey);
                }
                catch (Exception)
                {
                    RestorePreviousHotkey(
                        previousSettings.Hotkey,
                        previousWasRegistered);

                    throw new InvalidOperationException(
                        "That keyboard shortcut is unavailable. Choose another one.");
                }
            }

            try
            {
                _settingsService.Save(candidate);
            }
            catch (Exception)
            {
                if (registrationChanged)
                {
                    _hotkeyService.Unregister();

                    RestorePreviousHotkey(
                        previousSettings.Hotkey,
                        previousWasRegistered);
                }

                throw new InvalidOperationException(
                    "Settings could not be saved. Please try again.");
            }

            _settings = candidate.Clone();
            ApplyRuntimeSettings();

            bool startupUpdateFailed =
                !TryApplyStartupSetting(
                    out string startupError);

            bool libraryUpdateFailed = false;

            try
            {
                _libraryService.TrimToLimits(
                    _settings);
            }
            catch (Exception)
            {
                libraryUpdateFailed = true;
            }

            RefreshCurrentView();

            var notices = new List<string>();

            if (apiKeyRejected)
            {
                notices.Add(
                    string.IsNullOrWhiteSpace(
                        previousSettings.ApiKey)
                        ? "The KLIPY API key was not accepted. Enter a valid key."
                        : "The KLIPY API key was not accepted, so the previous key was kept.");
            }
            else if (apiKeyVerificationUnavailable)
            {
                notices.Add(
                    "The API key could not be checked right now. CopyGIF will verify it when you search.");
            }

            if (startupUpdateFailed)
            {
                notices.Add(startupError);
            }

            if (libraryUpdateFailed)
            {
                notices.Add(
                    "Saved GIF limits could not be updated.");
            }

            string resultMessage = notices.Count == 0
                ? "Settings saved."
                : "Settings saved. " +
                  string.Join(" ", notices);

            ShowTemporaryStatus(resultMessage);

            return new SettingsSaveResult(
                !apiKeyRejected,
                resultMessage,
                _settings.ApiKey);
        }

        private bool TryApplyStartupSetting(
            out string error)
        {
            try
            {
                _startupRegistrationService.Apply(
                    _settings.StartWithWindows);

                error = null;
                return true;
            }
            catch (Exception)
            {
                error =
                    "CopyGIF could not update its Windows startup entry.";

                return false;
            }
        }

        private void ShowTemporaryStatus(string message)
        {
            _statusClearTimer.Stop();

            _temporaryStatusMessage =
                message ?? string.Empty;

            StatusTextBlock.Text =
                _temporaryStatusMessage;

            if (!string.IsNullOrWhiteSpace(
                    _temporaryStatusMessage))
            {
                _statusClearTimer.Start();
            }
        }

        private void StatusClearTimer_Tick(
            object sender,
            EventArgs e)
        {
            _statusClearTimer.Stop();

            if (string.Equals(
                    StatusTextBlock.Text,
                    _temporaryStatusMessage,
                    StringComparison.Ordinal))
            {
                StatusTextBlock.Text =
                    string.Empty;
            }

            _temporaryStatusMessage = null;
        }

        private void CaptureExistingGeometry(
            AppSettings candidate)
        {
            candidate.WindowWidth =
                _settings.WindowWidth;
            candidate.WindowHeight =
                _settings.WindowHeight;
            candidate.WindowLeft =
                _settings.WindowLeft;
            candidate.WindowTop =
                _settings.WindowTop;
            candidate.HasSavedWindowPlacement =
                _settings.HasSavedWindowPlacement;

            if (WindowState == WindowState.Normal)
            {
                if (candidate.RememberWindowSize)
                {
                    candidate.WindowWidth =
                        ActualWidth;
                    candidate.WindowHeight =
                        ActualHeight;
                }

                if (string.Equals(
                        candidate.WindowPlacementMode,
                        "Remember",
                        StringComparison.OrdinalIgnoreCase))
                {
                    candidate.WindowLeft = Left;
                    candidate.WindowTop = Top;
                    candidate.HasSavedWindowPlacement =
                        true;
                }
            }
        }

        private void RestorePreviousHotkey(
            string previousHotkey,
            bool previousWasRegistered)
        {
            if (!previousWasRegistered)
            {
                return;
            }

            try
            {
                RegisterHotkey(previousHotkey);
            }
            catch (Exception)
            {
                StatusTextBlock.Text =
                    "The previous keyboard shortcut could not be restored.";
            }
        }

        private void RefreshCurrentView()
        {
            ShowView(_currentView);
        }

        private void SetBusy(
            bool isBusy,
            string message)
        {
            if (isBusy)
            {
                CancelHoverPreviewRequest();
                StopActivePreview();
            }

            _isOperationBusy = isBusy;

            BusyBorder.Visibility =
                isBusy
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            BusyTextBlock.Text =
                message ?? string.Empty;

            ResultsListBox.IsEnabled = !isBusy;
            SearchTextBox.IsEnabled = !isBusy;

            SearchButton.IsEnabled =
                !isBusy &&
                _searchCancellation == null;

            SearchTabButton.IsEnabled = !isBusy;
            FavoritesTabButton.IsEnabled = !isBusy;
            RecentsTabButton.IsEnabled = !isBusy;
            ClearRecentsButton.IsEnabled = !isBusy;
            SettingsButton.IsEnabled = !isBusy;
        }

        private static T FindAncestor<T>(
            DependencyObject source)
            where T : DependencyObject
        {
            DependencyObject current = source;

            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                try
                {
                    current =
                        VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException)
                {
                    current =
                        LogicalTreeHelper.GetParent(current);
                }
            }

            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            _searchDebounceTimer.Stop();
            _statusClearTimer.Stop();
            _searchCancellation?.Cancel();
            _copyCancellation?.Cancel();
            _favoriteCancellation?.Cancel();
            CancelHoverPreviewRequest();
            StopActivePreview();

            SaveWindowGeometry();
            _hotkeyService.Dispose();
            _httpClient.Dispose();

            base.OnClosed(e);
        }

        private enum PickerView
        {
            Search,
            Favorites,
            Recents
        }
    }
}
