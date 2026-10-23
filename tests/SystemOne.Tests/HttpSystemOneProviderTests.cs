namespace SystemOneTests;

using System.Net;
using System.Text.Json.Nodes;

public sealed class HttpSystemOneProviderTests : IDisposable
{
    private static readonly SystemOne.Request s_request = SystemOne.Requests
        .Noul("big", "Large?")
        .For(new JsonObject { ["amount"] = 12000 });

    private static readonly Dictionary<string, SystemOne.Image> s_onePng = new()
    {
        ["photo"] = SystemOne.Factory.Image((byte[])[1], "png"),
    };

    // 輔助方法建立的用戶端，測試結束時由 xUnit 呼叫 Dispose 釋放
    private readonly List<HttpClient> _clients = [];

    public void Dispose()
    {
        foreach (var http in _clients)
        {
            http.Dispose();
        }
    }

    private (HttpSystemOneProvider Provider, StubHandler Stub) Make(
        SystemOneEndpoint endpoint,
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var stub = new StubHandler(respond);
        var http = new HttpClient(stub);
        _clients.Add(http);

        return (new HttpSystemOneProvider(http, endpoint), stub);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Json(HttpStatusCode status, string body)
    {
        return delegate
        {
            return StubHandler.Json(status, body);
        };
    }

    private static SystemOneEndpoint Local(string? model = "m")
    {
        return SystemOneEndpoint.SelfHosted(new Uri("http://h:8000"), model);
    }

    [Fact]
    public async Task DecideAsync_SelfHosted_PostsCanonicalJsonWithoutAuth()
    {
        // Arrange
        var (client, stub) = Make(Local("clef-flash"), Json(HttpStatusCode.OK, Fixtures.ClefMulti));

        // Act
        var response = await client.DecideAsync(s_request);

        // Assert
        Assert.NotNull(stub.LastRequest);
        Assert.Equal(HttpMethod.Post, stub.LastRequest.Method);
        Assert.NotNull(stub.LastRequest.RequestUri);
        Assert.Equal("http://h:8000/v1/systemone", stub.LastRequest.RequestUri.AbsoluteUri);
        Assert.Null(stub.LastRequest.Headers.Authorization);
        Assert.False(stub.LastRequest.Headers.Contains("TypeSafe-Danger-Zone-Images"));
        var contentType = stub.LastRequest.Content?.Headers.ContentType;
        Assert.NotNull(contentType);
        Assert.Equal("application/json", contentType.MediaType);
        Assert.Equal(
            expected: """{"model":"clef-flash","state":{"amount":12000}""" +
                ""","questions":{"big":{"type":"noul","instructions":"Large?"}}}""",
            actual: stub.LastBody);
        Assert.Equal(0.7931, response.Get<SystemOne.NoulAnswer>("big").Probability);
    }

    [Fact]
    public async Task DecideAsync_RequestModel_OverridesEndpointDefault()
    {
        // Arrange
        var (client, stub) = Make(Local("default"), Json(HttpStatusCode.OK, Fixtures.ClefMulti));

        // Act
        await client.DecideAsync(s_request with { Model = "override" });

        // Assert
        Assert.Contains("\"model\":\"override\"", stub.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_NoModelAnywhere_ThrowsBeforeSending()
    {
        // Arrange
        var (client, stub) = Make(
            endpoint: Local(null),
            respond: delegate
            {
                throw new InvalidOperationException("must not send");
            });

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Null(stub.LastRequest);
    }

    [Fact]
    public async Task DecideAsync_BearerToken_SentPerRequest()
    {
        // Arrange
        var (client, stub) = Make(Local() with { BearerToken = "sk-1" }, Json(HttpStatusCode.OK, Fixtures.ClefMulti));

        // Act
        await client.DecideAsync(s_request);

        // Assert
        Assert.NotNull(stub.LastRequest);
        Assert.NotNull(stub.LastRequest.Headers.Authorization);
        Assert.Equal("Bearer sk-1", stub.LastRequest.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task DecideAsync_ImagesOnSelfHosted_DataUrlArrayWithoutDangerZoneHeader()
    {
        // Arrange
        var (client, stub) = Make(Local(), Json(HttpStatusCode.OK, Fixtures.ClefMulti));

        // Act
        await client.DecideAsync(s_request with { Images = s_onePng });

        // Assert
        Assert.Contains(
            expectedSubstring: """"images":["data:image/png;base64,AQ=="]"""",
            actualString: stub.LastBody,
            comparisonType: StringComparison.Ordinal);
        Assert.DoesNotContain("assets", stub.LastBody, StringComparison.Ordinal);
        Assert.NotNull(stub.LastRequest);
        Assert.False(stub.LastRequest.Headers.Contains("TypeSafe-Danger-Zone-Images"));
    }

    [Fact]
    public async Task DecideAsync_JpgImage_DataUrlUsesJpeg()
    {
        // Arrange
        var (client, stub) = Make(Local(), Json(HttpStatusCode.OK, Fixtures.ClefMulti));
        var images = new Dictionary<string, SystemOne.Image>
        {
            ["photo"] = SystemOne.Factory.Image((byte[])[1], "JPG"),
        };

        // Act
        await client.DecideAsync(s_request with { Images = images });

        // Assert
        Assert.Contains("data:image/jpeg;base64,AQ==", stub.LastBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, Fixtures.ErrorClefMlx, "q: type must be noul, choice, or score")]
    [InlineData(HttpStatusCode.BadRequest, Fixtures.ErrorVercel, Fixtures.ErrorVercelMessage)]
    [InlineData(HttpStatusCode.UnprocessableEntity, Fixtures.ErrorTypeSafe422, "Field required")]
    [InlineData(HttpStatusCode.BadRequest, Fixtures.ErrorTypeSafe400, "Unknown model: nope")]
    [InlineData(HttpStatusCode.BadRequest, Fixtures.ErrorCloudflare, "No such model @cf/cloudflare/nope")]
    [InlineData(HttpStatusCode.NotFound, Fixtures.ErrorPlainString, "model not found")]
    public async Task DecideAsync_ErrorBody_MapsToSystemOneException(
        HttpStatusCode status,
        string body,
        string expectedMessage)
    {
        // Arrange
        var (client, _) = Make(Local(), Json(status, body));

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(body, exception.ResponseBody);
    }

    [Fact]
    public async Task DecideAsync_ErrorNonJsonBody_UsesRawBody()
    {
        // Arrange
        var (client, _) = Make(
            endpoint: Local(),
            respond: delegate
            {
                return StubHandler.Html(HttpStatusCode.BadGateway, "<html>502</html>");
            });

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Equal("<html>502</html>", exception.Message);
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    }

    [Fact]
    public async Task DecideAsync_EmptySuccessBody_ThrowsSystemOneException()
    {
        // Arrange
        var (client, _) = Make(Local(), Json(HttpStatusCode.OK, ""));

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
    }

    [Theory]
    [InlineData(Fixtures.UnknownAnswerType)]
    [InlineData(Fixtures.MissingAnswerType)]
    public async Task DecideAsync_UnparsableSuccessBody_WrapsWithBody(string body)
    {
        // Arrange
        var (client, _) = Make(Local(), Json(HttpStatusCode.OK, body));

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Equal(body, exception.ResponseBody);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task DecideAsync_TransportFailure_WrapsWithInner()
    {
        // Arrange
        var (client, _) = Make(
            endpoint: Local(),
            respond: delegate
            {
                throw new HttpRequestException("connection refused");
            });

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Null(exception.StatusCode);
        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Contains("connection refused", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_HttpClientTimeout_WrapsAsSystemOneException()
    {
        // Arrange：HttpClient.Timeout 以 TaskCanceledException 浮出，但呼叫端的權杖沒有取消
        var (client, _) = Make(
            endpoint: Local(),
            respond: delegate
            {
                throw new TaskCanceledException("HttpClient.Timeout elapsed");
            });

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request, CancellationToken.None); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
        Assert.Contains("timed out", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_Cancelled_Propagates()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var (client, _) = Make(Local(), Json(HttpStatusCode.OK, Fixtures.ClefMulti));

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request, cancellation.Token); };

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNull()
    {
        // Arrange
        var endpoint = Local();
        var http = SystemOneProvider.DefaultHttpClient;

        // Act
        Action noHttp = delegate { _ = new HttpSystemOneProvider(null!, endpoint); };
        Action noEndpoint = delegate { _ = new HttpSystemOneProvider(http, null!); };

        // Assert
        Assert.Throws<ArgumentNullException>(noHttp);
        Assert.Throws<ArgumentNullException>(noEndpoint);
    }

    [Theory]
    [InlineData("""{"model":null,"answers":{"big":{"type":"noul","noul":0.5}}}""")]
    [InlineData("""{"model":"m","answers":{"big":null}}""")]
    public async Task DecideAsync_NullModelOrAnswer_ThrowsSystemOneException(string body)
    {
        // Arrange
        var (client, _) = Make(Local(), Json(HttpStatusCode.OK, body));

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Equal(body, exception.ResponseBody);
    }

    [Fact]
    public async Task DecideAsync_ReadResponseOverrideThrows_WrapsAsSystemOneException()
    {
        // Arrange：子類別解析信封時丟出非 JSON 例外，仍應視為伺服器失敗，讓後援鏈換下一個
        var stub = new StubHandler(Json(HttpStatusCode.OK, "[]"));
        using var http = new HttpClient(stub);
        var client = new EnvelopeProvider(http, Local());

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private sealed class EnvelopeProvider : HttpSystemOneProvider
    {
        public EnvelopeProvider(HttpClient http, SystemOneEndpoint endpoint)
            : base(http, endpoint)
        {
        }

        protected override SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)
        {
            throw new InvalidOperationException("Root is not an object.");
        }
    }
}