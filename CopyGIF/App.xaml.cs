using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CopyGIF
{
    public partial class App : Application
    {
        private const string SingleInstanceMutexName =
            @"Local\CopyGIF-96AE21CA-B26A-46DA-A429-450F1AE4E97C";

        private Mutex _singleInstanceMutex;
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private System.Drawing.Icon _trayDrawingIcon;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(
                true,
                SingleInstanceMutexName,
                out bool isFirstInstance);

            if (!isFirstInstance)
            {
                MessageBox.Show(
                    "CopyGIF is already running. Use its configured hotkey to open it.",
                    "CopyGIF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;

            var windowHelper = new WindowInteropHelper(_mainWindow);
            windowHelper.EnsureHandle();

            CreateTrayIcon();

            if (_mainWindow.NeedsApiKeySetup)
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        _mainWindow.ShowInitialApiKeySetup),
                    DispatcherPriority.ApplicationIdle);
            }
        }

        private void CreateTrayIcon()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip
            {
                ShowImageMargin = false
            };

            var openItem = new System.Windows.Forms.ToolStripMenuItem("Open");
            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");

            openItem.Click += (sender, args) =>
                Dispatcher.BeginInvoke(
                    new Action(() => _mainWindow?.ShowPicker()));

            settingsItem.Click += (sender, args) =>
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (_mainWindow == null)
                        {
                            return;
                        }

                        _mainWindow.OpenSettingsFromTray();
                    }));

            exitItem.Click += (sender, args) =>
                Dispatcher.BeginInvoke(
                    new Action(() => _mainWindow?.Close()));

            menu.Items.Add(openItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            _trayDrawingIcon = LoadTrayIcon();

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "CopyGIF",
                Icon = _trayDrawingIcon,
                ContextMenuStrip = menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (sender, args) =>
                Dispatcher.BeginInvoke(
                    new Action(() => _mainWindow?.ShowPicker()));
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            var iconUri = new Uri(
                "pack://application:,,,/Assets/Branding/CopyGIF.ico",
                UriKind.Absolute);

            var resource = System.Windows.Application.GetResourceStream(iconUri);

            if (resource?.Stream == null)
            {
                throw new InvalidOperationException(
                    "The CopyGIF tray icon resource could not be loaded.");
            }

            using (resource.Stream)
            using (var sourceIcon = new System.Drawing.Icon(resource.Stream))
            {
                return (System.Drawing.Icon)sourceIcon.Clone();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Icon = null;
                _trayIcon.ContextMenuStrip?.Dispose();
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_trayDrawingIcon != null)
            {
                _trayDrawingIcon.Dispose();
                _trayDrawingIcon = null;
            }

            _mainWindow = null;

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}
