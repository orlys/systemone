namespace SystemOneTests;

using System.Net;

public sealed class TypeSafeSystemOneProviderTests : IDisposable
{
    private static readonly SystemOne.Request s_request = SystemOne.Requests
        .Noul("big", "Large?")
        .For("photo");

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

    private (TypeSafeSystemOneProvider Provider, StubHandler Stub) Make(string fixture)
    {
        var stub = new StubHandler(delegate
        {
            return StubHandler.Json(HttpStatusCode.OK, fixture);
        });
        var http = new HttpClient(stub);
        _clients.Add(http);

        var provider = SystemOne.Providers.UseTypeSafeAi("sk-1", http: http);

        return (Assert.IsType<TypeSafeSystemOneProvider>(provider), stub);
    }

    private static bool HasDangerZoneHeader(HttpRequestMessage request)
    {
        return request.Headers.TryGetValues("TypeSafe-Danger-Zone-Images", out var values) &&
            (values.Single() is "true");
    }

    [Fact]
    public void Constructor_Default_HostedJevLatestEndpoint()
    {
        // Arrange & Act
        var (provider, _) = Make(Fixtures.TypeSafeMulti);

        // Assert
        Assert.Equal("https://api.typesafe.ai/v1/systemone", provider.Endpoint.Uri.AbsoluteUri);
        Assert.Equal(SystemOneEndpoint.LocalityKind.Hosted, provider.Endpoint.Locality);
        Assert.Equal("jev-latest", provider.Endpoint.DefaultModel);
        Assert.Equal("sk-1", provider.Endpoint.BearerToken);
    }

    [Fact]
    public async Task DecideAsync_TextOnly_BearerWithoutDangerZoneHeader()
    {
        // Arrange
        var (provider, stub) = Make(Fixtures.TypeSafeMulti);

        // Act
        await provider.DecideAsync(s_request);

        // Assert
        Assert.NotNull(stub.LastRequest);
        Assert.NotNull(stub.LastRequest.Headers.Authorization);
        Assert.Equal("Bearer sk-1", stub.LastRequest.Headers.Authorization.ToString());
        Assert.False(HasDangerZoneHeader(stub.LastRequest));
    }

    [Fact]
    public async Task DecideAsync_Images_AssetsAndDangerZoneHeader()
    {
        // Arrange
        var (provider, stub) = Make(Fixtures.TypeSafeMulti);

        // Act
        await provider.DecideAsync(s_request with { Images = s_onePng });

        // Assert
        Assert.Contains(
            expectedSubstring: """"assets":{"photo":{"type":"image","format":"png","data_base64":"AQ=="}}"""",
            actualString: stub.LastBody,
            comparisonType: StringComparison.Ordinal);
        Assert.DoesNotContain("images", stub.LastBody, StringComparison.Ordinal);
        Assert.NotNull(stub.LastRequest);
        Assert.True(HasDangerZoneHeader(stub.LastRequest));
    }

    [Fact]
    public async Task DecideAsync_BoxQuestion_DangerZoneHeader()
    {
        // Arrange
        var (provider, stub) = Make(Fixtures.TypeSafeBox);
        var request = SystemOne.Requests.Box("where", "the cat").For("photo");

        // Act
        var response = await provider.DecideAsync(request);

        // Assert
        Assert.NotNull(stub.LastRequest);
        Assert.True(HasDangerZoneHeader(stub.LastRequest));
        Assert.Equal(0.1, response.Get<SystemOne.BoxAnswer>("where").Bounds.Top);
    }
}