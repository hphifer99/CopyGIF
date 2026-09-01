using System.Net;
using System.Text;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Klipy;
using CopyGIF.Infrastructure.Tests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Klipy;

[TestClass]
public sealed class KlipyGifProviderTests
{
    [TestMethod]
    public async Task SearchAsync_ValidResponse_MapsGif()
    {
        const string json =
            """
            {
              "results": [
                {
                  "id": "12345",
                  "title": "Test GIF",
                  "content_description": "A test animation",
                  "media_formats": {
                    "gif": {
                      "url": "https://static.klipy.com/full.gif",
                      "dims": [640, 480],
                      "size": 1000
                    },
                    "mediumgif": {
                      "url": "https://static.klipy.com/preview.gif",
                      "dims": [320, 240],
                      "size": 500
                    },
                    "tinygifpreview": {
                      "url": "https://static.klipy.com/thumbnail.gif",
                      "dims": [160, 120],
                      "size": 100
                    }
                  }
                }
              ],
              "next": "continuation-value"
            }
            """;

        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    json));

        using HttpClient client =
            CreateClient(handler);

        TestSecretStore secrets =
            new(
                SecretNames.KlipyApiKey,
                "test-key");

        KlipyGifProvider provider =
            new(
                client,
                secrets);

        GifSearchPage page =
            await provider.SearchAsync(
                new GifSearchRequest
                {
                    Query = "hello",
                    PageSize = 24
                });

        Assert.HasCount(
            1,
            page.Items);

        GifItem item =
            page.Items[0];

        Assert.AreEqual(
            "klipy",
            item.ProviderId);

        Assert.AreEqual(
            "12345",
            item.Id);

        Assert.AreEqual(
            "Test GIF",
            item.Title);

        Assert.AreEqual(
            "A test animation",
            item.Description);

        Assert.AreEqual(
            640,
            item.Width);

        Assert.AreEqual(
            480,
            item.Height);

        Assert.AreEqual(
            "continuation-value",
            page.ContinuationToken);

        Assert.IsTrue(
            page.HasMore);
    }

    [TestMethod]
    public async Task SearchAsync_ContinuationToken_AddsPosParameter()
    {
        const string json =
            """
        {
          "results": [],
          "next": ""
        }
        """;

        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    json));

        using HttpClient client =
            CreateClient(handler);

        TestSecretStore secrets =
            new(
                SecretNames.KlipyApiKey,
                "test-key");

        KlipyGifProvider provider =
            new(
                client,
                secrets);

        await provider.SearchAsync(
            new GifSearchRequest
            {
                Query = "hello world",
                PageSize = 12,
                ContinuationToken =
                    "next-value"
            });

        Uri requestUri =
            handler.LastRequest!
                .RequestUri!;

        string decodedQuery =
            Uri.UnescapeDataString(
                requestUri.Query);

        StringAssert.Contains(
            decodedQuery,
            "limit=12");

        StringAssert.Contains(
            decodedQuery,
            "pos=next-value");

        StringAssert.Contains(
            decodedQuery,
            "q=hello world");
    }

    [TestMethod]
    public async Task SearchAsync_MissingCredential_ThrowsExpectedFailure()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    "{}"));

        using HttpClient client =
            CreateClient(handler);

        TestSecretStore secrets =
            new();

        KlipyGifProvider provider =
            new(
                client,
                secrets);

        GifProviderException exception =
            await Assert.ThrowsAsync<
                GifProviderException>(
                () => provider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = "test"
                    }));

        Assert.AreEqual(
            GifProviderFailure.MissingCredential,
            exception.Failure);
    }

    [TestMethod]
    public async Task SearchAsync_RateLimited_ThrowsExpectedFailure()
    {
        TestHttpMessageHandler handler =
            new(
                _ => new HttpResponseMessage(
                    HttpStatusCode.TooManyRequests));

        using HttpClient client =
            CreateClient(handler);

        TestSecretStore secrets =
            new(
                SecretNames.KlipyApiKey,
                "test-key");

        KlipyGifProvider provider =
            new(
                client,
                secrets);

        GifProviderException exception =
            await Assert.ThrowsAsync<
                GifProviderException>(
                () => provider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = "test"
                    }));

        Assert.AreEqual(
            GifProviderFailure.RateLimited,
            exception.Failure);
    }

    [TestMethod]
    public async Task ValidateCredentialAsync_Unauthorized_ReturnsInvalid()
    {
        TestHttpMessageHandler handler =
            new(
                _ => new HttpResponseMessage(
                    HttpStatusCode.Unauthorized));

        using HttpClient client =
            CreateClient(handler);

        TestSecretStore secrets =
            new();

        KlipyGifProvider provider =
            new(
                client,
                secrets);

        CredentialValidationResult result =
            await provider.ValidateCredentialAsync(
                "invalid-key");

        Assert.IsFalse(
            result.IsValid);
    }

    private static HttpClient CreateClient(
        HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress =
                new Uri(
                    "https://api.klipy.com/")
        };
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };
    }
}