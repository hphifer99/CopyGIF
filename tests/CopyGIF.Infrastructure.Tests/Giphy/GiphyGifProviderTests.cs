using System.Net;
using System.Text;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Giphy;
using CopyGIF.Infrastructure.Tests.TestDoubles;

namespace CopyGIF.Infrastructure.Tests.Giphy;

[TestClass]
public sealed class GiphyGifProviderTests
{
    [TestMethod]
    public async Task SearchPreservesMediaQueryAndUsesIndependentCredential()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Response) });
        using var client = new HttpClient(handler);
        var secrets = new TestSecretStore(SecretNames.GiphyApiKey, "giphy-key");
        await secrets.SetAsync(SecretNames.KlipyApiKey, "must-not-use");
        var provider = new GiphyGifProvider(client, secrets);
        var page = await provider.SearchAsync(new() { Kind = GifSearchKind.Search, Query = "cats & dogs", PageSize = 100 });
        Assert.IsTrue(handler.Uri!.Query.Contains("api_key=giphy-key", StringComparison.Ordinal));
        Assert.IsTrue(handler.Uri.Query.Contains("limit=50", StringComparison.Ordinal));
        Assert.IsTrue(handler.Uri.Query.Contains("q=cats%20%26%20dogs", StringComparison.Ordinal));
        Assert.AreEqual("https://media.giphy.com/media/id/giphy.gif?cid=approved&rid=giphy.gif", page.Items[0].GifUri.AbsoluteUri);
        Assert.AreEqual("giphy", page.Items[0].ProviderId);
        Assert.AreEqual("1", page.ContinuationToken);
    }

    [TestMethod]
    public async Task MissingGiphyKeyDoesNotUseKlipyKeyOrSendRequest()
    {
        using var handler = new Handler(_ => throw new InvalidOperationException("Unexpected request"));
        using var client = new HttpClient(handler);
        var provider = new GiphyGifProvider(client, new TestSecretStore(SecretNames.KlipyApiKey, "klipy"));
        var failure = await Assert.ThrowsExactlyAsync<GifProviderException>(() => provider.SearchAsync(new() { Query = "cats" }));
        Assert.AreEqual(GifProviderFailure.MissingCredential, failure.Failure);
        Assert.IsNull(handler.Uri);
    }

    [TestMethod]
    public async Task CredentialValidationReportsRateLimitWithoutSaving()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.TooManyRequests));
        using var client = new HttpClient(handler);
        var secrets = new TestSecretStore();
        var provider = new GiphyGifProvider(client, secrets);
        var result = await provider.ValidateCredentialAsync("test-key");
        Assert.AreEqual(CredentialValidationFailure.RateLimited, result.Failure);
        Assert.IsNull(await secrets.GetAsync(SecretNames.GiphyApiKey));
    }

    [TestMethod]
    public async Task MediaHostSubstitutionIsSkippedWithoutExposingTheHost()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent(Response.Replace("media.giphy.com", "media.giphy.com.attacker.example", StringComparison.Ordinal)) });
        using var client = new HttpClient(handler);
        var provider = new GiphyGifProvider(client, new TestSecretStore(SecretNames.GiphyApiKey, "key"));
        var page = await provider.SearchAsync(new() { Query = "cats" });
        Assert.IsEmpty(page.Items);
    }

    [TestMethod]
    public async Task SearchAndTrendingUseSelectedRating()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(Response) });
        using var client = new HttpClient(handler);
        var provider = new GiphyGifProvider(client, new TestSecretStore(SecretNames.GiphyApiKey, "key"));
        await provider.SearchAsync(new() { Query = "cats", ContentRating = GifContentRating.Pg13 });
        StringAssert.Contains(handler.Uri!.Query, "rating=pg-13");
        await provider.SearchAsync(new() { Kind = GifSearchKind.Trending, Query = "", ContentRating = GifContentRating.G });
        StringAssert.Contains(handler.Uri!.Query, "rating=g");
        await provider.SearchAsync(new() { Query = "cats" });
        Assert.IsFalse(handler.Uri!.Query.Contains("rating=", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TrendingOffsetBeyondProviderLimitIsRejectedBeforeNetwork()
    {
        using var handler = new Handler(_ => throw new InvalidOperationException("Unexpected request"));
        using var client = new HttpClient(handler);
        var provider = new GiphyGifProvider(client, new TestSecretStore(SecretNames.GiphyApiKey, "key"));
        await Assert.ThrowsExactlyAsync<GifProviderException>(() => provider.SearchAsync(new()
        { Kind = GifSearchKind.Trending, Query = "", ContinuationToken = "500" }));
        Assert.IsNull(handler.Uri);
    }

    private const string Response = """
        {"meta":{"status":200},"pagination":{"offset":0,"count":1,"total_count":2},"data":[
        {"id":"id","title":"A GIF","url":"https://giphy.com/gifs/id","images":{
        "original":{"url":"https://media.giphy.com/media/id/giphy.gif?cid=approved&rid=giphy.gif","width":"480","height":"360"},
        "fixed_width":{"url":"https://media.giphy.com/media/id/200w.gif?cid=approved"},
        "fixed_width_still":{"url":"https://media.giphy.com/media/id/200w_s.gif?cid=approved"}}}]}
        """;
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Uri = request.RequestUri; return Task.FromResult(respond(request)); }
    }
}
