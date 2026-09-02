using CopyGIF.Platform.Windows.Shell;

namespace CopyGIF.Platform.Windows.Tests.Shell;

[TestClass]
public sealed class UriLauncherServiceTests
{
    [TestMethod]
    public async Task TryLaunchAsync_HttpsUri_LaunchesThroughWindowsShell()
    {
        RecordingUriLaunchNativeApi nativeApi =
            new(result: true);

        UriLauncherService service =
            new(nativeApi);

        Uri uri =
            new(
                "https://klipy.com/developers");

        bool result =
            await service.TryLaunchAsync(uri);

        Assert.IsTrue(result);

        Assert.AreEqual(
            uri,
            nativeApi.LaunchedUri);
    }

    [TestMethod]
    public async Task TryLaunchAsync_HttpUri_IsRejected()
    {
        RecordingUriLaunchNativeApi nativeApi =
            new(result: true);

        UriLauncherService service =
            new(nativeApi);

        bool result =
            await service.TryLaunchAsync(
                new Uri(
                    "http://example.com"));

        Assert.IsFalse(result);

        Assert.IsNull(
            nativeApi.LaunchedUri);
    }

    [TestMethod]
    public async Task TryLaunchAsync_UriWithUserInfo_IsRejected()
    {
        RecordingUriLaunchNativeApi nativeApi =
            new(result: true);

        UriLauncherService service =
            new(nativeApi);

        bool result =
            await service.TryLaunchAsync(
                new Uri(
                    "https://user:password@example.com"));

        Assert.IsFalse(result);

        Assert.IsNull(
            nativeApi.LaunchedUri);
    }

    [TestMethod]
    public async Task TryLaunchAsync_ShellRejectsLaunch_ReturnsFalse()
    {
        UriLauncherService service =
            new(
                new RecordingUriLaunchNativeApi(
                    result: false));

        bool result =
            await service.TryLaunchAsync(
                new Uri(
                    "https://example.com"));

        Assert.IsFalse(result);
    }

    private sealed class RecordingUriLaunchNativeApi :
        IUriLaunchNativeApi
    {
        private readonly bool _result;

        public RecordingUriLaunchNativeApi(
            bool result)
        {
            _result = result;
        }

        public Uri? LaunchedUri { get; private set; }

        public bool TryLaunch(
            Uri uri)
        {
            LaunchedUri = uri;

            return _result;
        }
    }
}
