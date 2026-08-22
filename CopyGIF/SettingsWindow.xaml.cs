using CopyGIF.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Navigation;

namespace CopyGIF
{
    public partial class SettingsWindow : Window
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;
        private const int DwmwaBorderColor = 34;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;

        private readonly Func<
            AppSettings,
            CancellationToken,
            Task<SettingsSaveResult>> _saveAsync;
        private readonly bool _closeWhenFocusLost;
        private readonly bool _isApiKeySetup;
        private CancellationTokenSource _saveCancellation;
        private string _originalApiKey;
        private bool _isSynchronizingApiKey;
        private bool _isSaving;
        private bool _closeWhenSaveFinishes;
        private bool _isClosingNormally;

        public bool ClosedBecauseFocusLost { get; private set; }

        public SettingsWindow(
            AppSettings settings,
            Func<AppSettings, CancellationToken, Task<SettingsSaveResult>>
                saveAsync,
            bool closeWhenFocusLost,
            bool isApiKeySetup)
        {
            InitializeComponent();

            _saveAsync = saveAsync
                ?? throw new ArgumentNullException(
                    nameof(saveAsync));
            _isApiKeySetup = isApiKeySetup;
            _closeWhenFocusLost =
                closeWhenFocusLost && !isApiKeySetup;

            AppSettings initialSettings =
                settings ?? AppSettings.CreateDefault();

            _originalApiKey = initialSettings.ApiKey ?? string.Empty;
            Populate(initialSettings);

            if (_isApiKeySetup)
            {
                SettingsTitleTextBlock.Text =
                    string.IsNullOrWhiteSpace(_originalApiKey)
                        ? "Welcome to CopyGIF"
                        : "Update your KLIPY API key";

                SettingsStatusTextBlock.Text =
                    "A valid KLIPY API key is required before you can search.";
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_isApiKeySetup)
            {
                ApiKeyPasswordBox.Focus();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyDarkTitleBar();
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                IntPtr windowHandle =
                    new WindowInteropHelper(this).Handle;
                int enabled = 1;
                int valueSize = Marshal.SizeOf(typeof(int));

                int result = DwmSetWindowAttribute(
                    windowHandle,
                    DwmwaUseImmersiveDarkMode,
                    ref enabled,
                    valueSize);

                if (result != 0)
                {
                    DwmSetWindowAttribute(
                        windowHandle,
                        DwmwaUseImmersiveDarkModeLegacy,
                        ref enabled,
                        valueSize);
                }

                int borderColor = 0x0047413F;
                int captionColor = 0x00252220;
                int textColor = 0x00FFFFFF;

                DwmSetWindowAttribute(
                    windowHandle,
                    DwmwaBorderColor,
                    ref borderColor,
                    valueSize);
                DwmSetWindowAttribute(
                    windowHandle,
                    DwmwaCaptionColor,
                    ref captionColor,
                    valueSize);
                DwmSetWindowAttribute(
                    windowHandle,
                    DwmwaTextColor,
                    ref textColor,
                    valueSize);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private void Populate(AppSettings settings)
        {
            SetApiKey(settings.ApiKey);

            HotkeyTextBox.Text = settings.Hotkey;
            ResultsPerSearchTextBox.Text =
                settings.ResultsPerSearch.ToString();
            SearchDelayTextBox.Text =
                settings.SearchDebounceMilliseconds.ToString();
            FavoriteLimitTextBox.Text =
                settings.FavoriteLimit.ToString();
            RecentLimitTextBox.Text =
                settings.RecentLimit.ToString();

            CloseWhenFocusLostCheckBox.IsChecked =
                settings.CloseWhenFocusLost;
            HideAfterCopyCheckBox.IsChecked =
                settings.HideAfterCopy;
            RememberWindowSizeCheckBox.IsChecked =
                settings.RememberWindowSize;
            AnimatePreviewsCheckBox.IsChecked =
                settings.AnimatePreviews;
            StartWithWindowsCheckBox.IsChecked =
                settings.StartWithWindows;
            StoreFavoritesLocallyCheckBox.IsChecked =
                settings.StoreFavoritesLocally;
            StoreRecentsLocallyCheckBox.IsChecked =
                settings.StoreRecentsLocally;

            SelectPlacement(settings.WindowPlacementMode);
        }

        private void SetApiKey(string apiKey)
        {
            string value = apiKey ?? string.Empty;

            _isSynchronizingApiKey = true;
            ApiKeyPasswordBox.Password = value;
            ApiKeyTextBox.Text = value;
            _isSynchronizingApiKey = false;
        }

        private void SelectPlacement(string placementMode)
        {
            foreach (object item in WindowPlacementComboBox.Items)
            {
                var comboBoxItem = item as ComboBoxItem;

                if (comboBoxItem != null &&
                    string.Equals(
                        comboBoxItem.Tag as string,
                        placementMode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    WindowPlacementComboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }

            WindowPlacementComboBox.SelectedIndex = 0;
        }

        private AppSettings ReadSettings(out string error)
        {
            error = null;

            string apiKey =
                (ApiKeyPasswordBox.Password ?? string.Empty)
                .Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                error =
                    "Enter a KLIPY API key before saving.";

                ApiKeyPasswordBox.Focus();
                return null;
            }

            if (!TryReadInt(
                    ResultsPerSearchTextBox,
                    "Results per search",
                    out int resultsPerSearch,
                    out error) ||
                !TryReadInt(
                    SearchDelayTextBox,
                    "Search delay",
                    out int searchDelay,
                    out error) ||
                !TryReadInt(
                    FavoriteLimitTextBox,
                    "Maximum favorites",
                    out int favoriteLimit,
                    out error) ||
                !TryReadInt(
                    RecentLimitTextBox,
                    "Recent history limit",
                    out int recentLimit,
                    out error))
            {
                return null;
            }

            var selectedPlacement =
                WindowPlacementComboBox.SelectedItem as ComboBoxItem;

            var settings = new AppSettings
            {
                ApiKey = apiKey,
                Hotkey = HotkeyTextBox.Text,
                ResultsPerSearch = resultsPerSearch,
                SearchDebounceMilliseconds = searchDelay,
                FavoriteLimit = favoriteLimit,
                RecentLimit = recentLimit,
                WindowPlacementMode =
                    selectedPlacement?.Tag as string ?? "Mouse",
                CloseWhenFocusLost =
                    CloseWhenFocusLostCheckBox.IsChecked == true,
                HideAfterCopy =
                    HideAfterCopyCheckBox.IsChecked == true,
                RememberWindowSize =
                    RememberWindowSizeCheckBox.IsChecked == true,
                AnimatePreviews =
                    AnimatePreviewsCheckBox.IsChecked == true,
                StartWithWindows =
                    StartWithWindowsCheckBox.IsChecked == true,
                AutoLoadMoreResults = false,
                StoreFavoritesLocally =
                    StoreFavoritesLocallyCheckBox.IsChecked == true,
                StoreRecentsLocally =
                    StoreRecentsLocallyCheckBox.IsChecked == true
            };

            string validationError = settings.ValidateForSave();

            if (validationError != null)
            {
                error = validationError;
                return null;
            }

            try
            {
                settings.Hotkey = HotkeyService.Normalize(settings.Hotkey);
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return null;
            }

            return settings;
        }

        private void KlipyDevelopersLink_RequestNavigate(
            object sender,
            RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });

                e.Handled = true;
            }
            catch (Exception)
            {
                SettingsStatusTextBlock.Text =
                    "Open https://klipy.com/developers in your browser.";
            }
        }

        private static bool TryReadInt(
            TextBox textBox,
            string fieldName,
            out int value,
            out string error)
        {
            if (!int.TryParse(textBox.Text?.Trim(), out value))
            {
                error = fieldName + " must be a whole number.";
                textBox.Focus();
                textBox.SelectAll();
                return false;
            }

            error = null;
            return true;
        }

        private async void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isSaving)
            {
                return;
            }

            AppSettings settings = ReadSettings(out string error);

            if (settings == null)
            {
                SettingsStatusTextBlock.Text = error;
                return;
            }

            _saveCancellation = new CancellationTokenSource();
            SetSavingState(true);

            bool apiKeyChanged = !string.Equals(
                _originalApiKey,
                (settings.ApiKey ?? string.Empty).Trim(),
                StringComparison.Ordinal);

            SettingsStatusTextBlock.Text = apiKeyChanged
                ? "Checking API key..."
                : "Saving...";

            bool closeAfterSave = false;

            try
            {
                SettingsSaveResult result = await _saveAsync(
                    settings,
                    _saveCancellation.Token);

                _originalApiKey = result.EffectiveApiKey ?? string.Empty;
                SetApiKey(_originalApiKey);
                SettingsStatusTextBlock.Text = result.Message;
                closeAfterSave = result.CloseWindow;
            }
            catch (OperationCanceledException)
            {
                SettingsStatusTextBlock.Text = "Save canceled.";
            }
            catch (Exception exception)
            {
                SettingsStatusTextBlock.Text = exception.Message;
            }
            finally
            {
                _saveCancellation?.Dispose();
                _saveCancellation = null;

                if (IsLoaded)
                {
                    SetSavingState(false);
                }
            }

            if (IsLoaded &&
                (closeAfterSave || _closeWhenSaveFinishes))
            {
                _isClosingNormally = !ClosedBecauseFocusLost;
                Close();
            }
        }

        private void SetSavingState(bool isSaving)
        {
            _isSaving = isSaving;
            SaveButton.IsEnabled = !isSaving;
            CancelButton.IsEnabled = true;
            CancelButton.Content = isSaving
                ? "Cancel save"
                : "Cancel";
            RestoreDefaultsButton.IsEnabled = !isSaving;
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_isSaving)
            {
                _isClosingNormally = true;
                Close();
                return;
            }

            e.Handled = true;
            SettingsStatusTextBlock.Text = "Canceling...";
            _saveCancellation?.Cancel();
        }

        private void RestoreDefaultsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string apiKey = ApiKeyPasswordBox.Password;
            AppSettings defaults = AppSettings.CreateDefault();
            defaults.ApiKey = apiKey;
            Populate(defaults);

            SettingsStatusTextBlock.Text =
                "Defaults restored. Select Save to apply them.";
        }

        private void ShowApiKeyCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            bool showKey = ShowApiKeyCheckBox.IsChecked == true;

            ApiKeyPasswordBox.Visibility =
                showKey ? Visibility.Collapsed : Visibility.Visible;
            ApiKeyTextBox.Visibility =
                showKey ? Visibility.Visible : Visibility.Collapsed;

            if (showKey)
            {
                ApiKeyTextBox.Focus();
                ApiKeyTextBox.CaretIndex = ApiKeyTextBox.Text.Length;
            }
            else
            {
                ApiKeyPasswordBox.Focus();
            }
        }

        private void ApiKeyPasswordBox_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (_isSynchronizingApiKey)
            {
                return;
            }

            _isSynchronizingApiKey = true;
            ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
            _isSynchronizingApiKey = false;
        }

        private void ApiKeyTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_isSynchronizingApiKey)
            {
                return;
            }

            _isSynchronizingApiKey = true;
            ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
            _isSynchronizingApiKey = false;
        }

        private void Window_Closing(
            object sender,
            CancelEventArgs e)
        {
            if (_isSaving)
            {
                _closeWhenSaveFinishes = true;
                _saveCancellation?.Cancel();
                e.Cancel = true;
                return;
            }

            _isClosingNormally =
                _isClosingNormally || !ClosedBecauseFocusLost;
            _saveCancellation?.Cancel();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_isClosingNormally ||
                !_closeWhenFocusLost ||
                !IsVisible)
            {
                return;
            }

            ClosedBecauseFocusLost = true;

            if (_isSaving)
            {
                _closeWhenSaveFinishes = true;
                _saveCancellation?.Cancel();
                return;
            }

            Close();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
    IntPtr windowHandle,
    int attribute,
    ref int attributeValue,
    int attributeSize);
    }

    public sealed class SettingsSaveResult
    {
        public SettingsSaveResult(
            bool closeWindow,
            string message,
            string effectiveApiKey)
        {
            CloseWindow = closeWindow;
            Message = message ?? string.Empty;
            EffectiveApiKey = effectiveApiKey ?? string.Empty;
        }

        public bool CloseWindow { get; }

        public string Message { get; }

        public string EffectiveApiKey { get; }
    }
}
