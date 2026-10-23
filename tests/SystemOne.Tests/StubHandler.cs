namespace SystemOneTests;

using System.Net;
using System.Text;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public int Calls { get; private set; }

    public static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage Html(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/html"),
        };
    }

    public static HttpResponseMessage Bytes(byte[] body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        LastRequest = request;
        LastBody = request.Content is { } content
            ? await content.ReadAsStringAsync(cancellationToken)
            : null;

        return _respond(request);
    }
}