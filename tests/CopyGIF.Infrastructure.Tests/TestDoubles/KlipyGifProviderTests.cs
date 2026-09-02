using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Klipy;
using CopyGIF.Infrastructure.Tests.TestDoubles;

namespace CopyGIF.Infrastructure.Tests.Klipy;

[TestClass]
public sealed class KlipyGifProviderTests
{
    private const string EmptyPageJson =
        """
        {
          "result": true,
          "data": {
            "data": [],
            "current_page": 1,
            "per_page": 24,
            "has_next": false
          }
        }
        """;

    [TestMethod]
    public async Task SearchAsync_ValidResponse_MapsGif()
    {
        const string json =
            """
            {
              "result": true,
              "data": {
                "data": [
                  {
                    "id": 662,
                    "slug": "hello-hi-662",
                    "title": "Hello",
                    "file": {
                      "hd": {
                        "gif": {
                          "url": "https://static.klipy.com/full.gif",
                          "width": 640,
                          "height": 480,
                          "size": 1000
                        }
                      },
                      "md": {
                        "gif": {
                          "url": "https://static.klipy.com/medium.gif",
                          "width": 480,
                          "height": 360,
                          "size": 700
                        }
                      },
                      "sm": {
                        "gif": {
                          "url": "https://static.klipy.com/preview.gif",
                          "width": 320,
                          "height": 240,
                          "size": 500
                        },
                        "jpg": {
                          "url": "https://static.klipy.com/thumbnail.jpg",
                          "width": 160,
                          "height": 120,
                          "size": 100
                        }
                      }
                    }
                  }
                ],
                "current_page": 1,
                "per_page": 24,
                "has_next": true
              }
            }
            """;

        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    json));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        GifSearchPage page =
            await provider.SearchAsync(
                new GifSearchRequest
                {
                    Query = "hello world",
                    PageSize = 24
                });

        Assert.HasCount(
            1,
            page.Items);

        GifItem item = page.Items[0];

        Assert.AreEqual(
            "klipy",
            item.ProviderId);

        Assert.AreEqual(
            "hello-hi-662",
            item.Id);

        Assert.AreEqual(
            "Hello",
            item.Title);

        Assert.AreEqual(
            new Uri(
                "https://static.klipy.com/full.gif"),
            item.GifUri);

        Assert.AreEqual(
            new Uri(
                "https://static.klipy.com/preview.gif"),
            item.PreviewUri);

        Assert.AreEqual(
            new Uri(
                "https://static.klipy.com/thumbnail.jpg"),
            item.ThumbnailUri);

        Assert.AreEqual(
            640,
            item.Width);

        Assert.AreEqual(
            480,
            item.Height);

        Assert.AreEqual(
            1000L,
            item.SizeBytes);

        Assert.AreEqual(
            "2",
            page.ContinuationToken);

        Assert.IsTrue(
            page.HasMore);

        Assert.AreEqual(
            "/api/v1/test-key/gifs/search",
            handler.LastRequestUri!
                .AbsolutePath);

        string decodedQuery =
            Uri.UnescapeDataString(
                handler.LastRequestUri.Query);

        StringAssert.Contains(
            decodedQuery,
            "q=hello world");

        StringAssert.Contains(
            decodedQuery,
            "page=1");

        StringAssert.Contains(
            decodedQuery,
            "per_page=24");
    }

    [TestMethod]
    public async Task SearchAsync_ContinuationToken_UsesRequestedPage()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    EmptyPageJson));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        await provider.SearchAsync(
            new GifSearchRequest
            {
                Query = "cats",
                PageSize = 12,
                ContinuationToken = "3"
            });

        string query =
            handler.LastRequestUri!
                .Query;

        StringAssert.Contains(
            query,
            "page=3");

        StringAssert.Contains(
            query,
            "per_page=12");
    }

    [TestMethod]
    public async Task SearchAsync_ProviderMinimum_ClampsPageSize()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    EmptyPageJson));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        await provider.SearchAsync(
            new GifSearchRequest
            {
                Query = "cats",
                PageSize = 6
            });

        StringAssert.Contains(
            handler.LastRequestUri!.Query,
            "per_page=8");
    }

    [TestMethod]
    public async Task SearchAsync_Trending_UsesTrendingEndpoint()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    EmptyPageJson));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        await provider.SearchAsync(
            new GifSearchRequest
            {
                Query = string.Empty,
                Kind = GifSearchKind.Trending,
                PageSize = 24
            });

        Assert.AreEqual(
            "/api/v1/test-key/gifs/trending",
            handler.LastRequestUri!
                .AbsolutePath);

        Assert.IsFalse(
            handler.LastRequestUri.Query
                .Contains(
                    "q=",
                    StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SearchAsync_EmptySearch_ReturnsEmptyWithoutCredential()
    {
        TestHttpMessageHandler handler =
            new(
                _ => throw new AssertFailedException(
                    "No HTTP request was expected."));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            new(
                client,
                new TestSecretStore());

        GifSearchPage page =
            await provider.SearchAsync(
                new GifSearchRequest
                {
                    Query = "   "
                });

        Assert.IsEmpty(
            page.Items);

        Assert.IsNull(
            handler.LastRequestUri);
    }

    [TestMethod]
    public async Task SearchAsync_InvalidContinuationToken_IsRejected()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    EmptyPageJson));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.SearchAsync(
                new GifSearchRequest
                {
                    Query = "cats",
                    ContinuationToken =
                        "https://example.test"
                }));

        Assert.IsNull(
            handler.LastRequestUri);
    }

    [TestMethod]
    public async Task SearchAsync_MissingCredential_ThrowsExpectedFailure()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    EmptyPageJson));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            new(
                client,
                new TestSecretStore());

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
    public async Task SearchAsync_RateLimited_PreservesRetryAfter()
    {
        TimeSpan retryAfter =
            TimeSpan.FromSeconds(30);

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    HttpResponseMessage response =
                        new(
                            HttpStatusCode.TooManyRequests);

                    response.Headers.RetryAfter =
                        new RetryConditionHeaderValue(
                            retryAfter);

                    return response;
                });

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

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

        Assert.AreEqual(
            retryAfter,
            exception.RetryAfter);
    }

    [TestMethod]
    public async Task SearchAsync_Timeout_ThrowsExpectedFailure()
    {
        TestHttpMessageHandler handler =
            new(
                _ => throw new TaskCanceledException(
                    "Simulated timeout."));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        GifProviderException exception =
            await Assert.ThrowsAsync<
                GifProviderException>(
                () => provider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = "test"
                    }));

        Assert.AreEqual(
            GifProviderFailure.Timeout,
            exception.Failure);
    }

    [TestMethod]
    public async Task SearchAsync_MalformedJson_ThrowsInvalidResponse()
    {
        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    "{ not-valid-json"));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        GifProviderException exception =
            await Assert.ThrowsAsync<
                GifProviderException>(
                () => provider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = "test"
                    }));

        Assert.AreEqual(
            GifProviderFailure.InvalidResponse,
            exception.Failure);
    }

    [TestMethod]
    public async Task SearchAsync_OversizedResponse_ThrowsInvalidResponse()
    {
        byte[] oversizedBody =
            new byte[
                (2 * 1024 * 1024) + 1];

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                oversizedBody)
                    });

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        GifProviderException exception =
            await Assert.ThrowsAsync<
                GifProviderException>(
                () => provider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = "test"
                    }));

        Assert.AreEqual(
            GifProviderFailure.InvalidResponse,
            exception.Failure);
    }

    [TestMethod]
    public async Task SearchAsync_InsecureMediaUrl_ThrowsInvalidResponse()
    {
        const string json =
            """
            {
              "result": true,
              "data": {
                "data": [
                  {
                    "id": 1,
                    "slug": "unsafe-1",
                    "title": "Unsafe",
                    "file": {
                      "hd": {
                        "gif": {
                          "url": "http://static.klipy.com/full.gif",
                          "width": 100,
                          "height": 100,
                          "size": 100
                        }
                      }
                    }
                  }
                ],
                "current_page": 1,
                "per_page": 24,
                "has_next": false
              }
            }
            """;

        TestHttpMessageHandler handler =
            new(
                _ => JsonResponse(
                    HttpStatusCode.OK,
                    json));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        GifProviderException exception =
            await Assert.ThrowsAsync<
                GifProviderException>(
                () => provider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = "test"
                    }));

        Assert.AreEqual(
            GifProviderFailure.InvalidResponse,
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

        KlipyGifProvider provider =
            new(
                client,
                new TestSecretStore());

        CredentialValidationResult result =
            await provider.ValidateCredentialAsync(
                "invalid-key");

        Assert.IsFalse(
            result.IsValid);

        Assert.AreEqual(
            CredentialValidationFailure.InvalidCredential,
            result.Failure);

        Assert.AreEqual(
            "/api/v1/invalid-key/gifs/trending",
            handler.LastRequestUri!
                .AbsolutePath);
    }

    [TestMethod]
    public async Task RegisterShareAsync_PostsSlugAndQuery()
    {
        TestHttpMessageHandler handler =
            new(
                _ => new HttpResponseMessage(
                    HttpStatusCode.NoContent));

        using HttpClient client =
            CreateClient(handler);

        KlipyGifProvider provider =
            CreateProvider(client);

        await provider.RegisterShareAsync(
            "hello-hi-662",
            "  hello  ");

        Assert.AreEqual(
            HttpMethod.Post,
            handler.LastMethod);

        Assert.AreEqual(
            "/api/v1/test-key/gifs/share/hello-hi-662",
            handler.LastRequestUri!
                .AbsolutePath);

        using JsonDocument body =
            JsonDocument.Parse(
                handler.LastRequestBody!);

        Assert.AreEqual(
            "hello",
            body.RootElement
                .GetProperty("q")
                .GetString());
    }

    private static KlipyGifProvider CreateProvider(
        HttpClient client)
    {
        return new KlipyGifProvider(
            client,
            new TestSecretStore(
                SecretNames.KlipyApiKey,
                "test-key"));
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
