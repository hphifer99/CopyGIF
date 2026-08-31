namespace CopyGIF.Infrastructure.Tests.TestDoubles;

internal sealed class TestHttpMessageHandler
    : HttpMessageHandler
{
    private readonly Func<
        HttpRequestMessage,
        HttpResponseMessage> _responseFactory;

    public TestHttpMessageHandler(
        Func<
            HttpRequestMessage,
            HttpResponseMessage> responseFactory)
    {
        _responseFactory =
            responseFactory ??
            throw new ArgumentNullException(
                nameof(responseFactory));
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LastRequest = request;

        return Task.FromResult(
            _responseFactory(request));
    }
}