using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Clipboard;
using CopyGIF.Platform.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using WinRT.Interop;

namespace CopyGIF.App.Services;

// The clipboard owner is the actual picker HWND, including when it is hidden.
// Resolve it only when copying, after WindowManager has created the window.
internal sealed class WinUiClipboardService(
    WinUiDispatcher dispatcher,
    IServiceProvider services) : IClipboardService
{
    public Task CopyGifAsync(DownloadedGif gif, CancellationToken cancellationToken = default) =>
        dispatcher.InvokeAsync(async () =>
        {
            var window = services.GetRequiredService<WindowManager>().MainWindow
                ?? throw new InvalidOperationException("The GIF window is not available for copying.");
            var clipboard = new GifClipboardService(new Owner(WindowNative.GetWindowHandle(window)));
            await clipboard.CopyGifAsync(gif, cancellationToken);
        });

    private sealed class Owner(nint handle) : IWindowHandleProvider
    {
        public nint GetWindowHandle() => handle;
    }
}
