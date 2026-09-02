namespace CopyGIF.Infrastructure.Tests.TestDoubles;

internal sealed class TestHttpMessageHandler :
    HttpMessageHandler
{
    private readonly Func<
        HttpRequestMessage,
        CancellationToken,
        Task<HttpResponseMessage>>
        _responseFactory;

    public TestHttpMessageHandler(
        Func<
            HttpRequestMessage,
            HttpResponseMessage> responseFactory)
    {
        ArgumentNullException.ThrowIfNull(
            responseFactory);

        _responseFactory =
            (request, _) =>
                Task.FromResult(
                    responseFactory(request));
    }

    public TestHttpMessageHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> responseFactory)
    {
        _responseFactory =
            responseFactory ??
            throw new ArgumentNullException(
                nameof(responseFactory));
    }

    public HttpRequestMessage? LastRequest
    {
        get;
        private set;
    }

    public HttpMethod? LastMethod
    {
        get;
        private set;
    }

    public Uri? LastRequestUri
    {
        get;
        private set;
    }

    public string? LastRequestBody
    {
        get;
        private set;
    }

    protected override async Task<HttpResponseMessage>
        SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        LastRequest = request;
        LastMethod = request.Method;
        LastRequestUri = request.RequestUri;

        LastRequestBody =
            request.Content is null
                ? null
                : await request.Content
                    .ReadAsStringAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

        return await _responseFactory(
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
