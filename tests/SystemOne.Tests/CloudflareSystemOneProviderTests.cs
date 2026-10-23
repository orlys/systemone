namespace SystemOneTests;

using System.Net;
using System.Text.Json.Nodes;

public sealed class CloudflareSystemOneProviderTests : IDisposable
{
    private static readonly SystemOne.Request s_request = SystemOne.Requests
        .Noul("big", "Large?")
        .For(new JsonObject { ["amount"] = 12000 });

    // 輔助方法建立的用戶端，測試結束時由 xUnit 呼叫 Dispose 釋放
    private readonly List<HttpClient> _clients = [];

    public void Dispose()
    {
        foreach (var http in _clients)
        {
            http.Dispose();
        }
    }

    private (CloudflareSystemOneProvider Provider, StubHandler Stub) Make(string fixture)
    {
        var stub = new StubHandler(delegate
        {
            return StubHandler.Json(HttpStatusCode.OK, fixture);
        });
        var http = new HttpClient(stub);
        _clients.Add(http);

        var provider = SystemOne.Providers.UseCloudflareWorkersAi(
            accountId: "a",
            apiToken: "t",
            http: http);

        return (Assert.IsType<CloudflareSystemOneProvider>(provider), stub);
    }

    [Fact]
    public void Constructor_AccountWithSpace_EscapesHostedUrl()
    {
        // Arrange & Act
        var provider = Assert.IsType<CloudflareSystemOneProvider>(
            SystemOne.Providers.UseCloudflareWorkersAi("acc 1", "tok"));

        // Assert
        Assert.Equal(
            expected: "https://api.cloudflare.com/client/v4/accounts/acc%201/ai/run/@cf/cloudflare/clef-flash",
            actual: provider.Endpoint.Uri.AbsoluteUri);
        Assert.Equal(SystemOneEndpoint.LocalityKind.Hosted, provider.Endpoint.Locality);
        Assert.Equal("tok", provider.Endpoint.BearerToken);
    }

    [Fact]
    public async Task DecideAsync_SuccessEnvelope_Unwrapped()
    {
        // Arrange
        var (provider, _) = Make(Fixtures.CloudflareOk);

        // Act
        var response = await provider.DecideAsync(s_request);

        // Assert
        Assert.Equal(0.9, response.Get<SystemOne.NoulAnswer>("big").Probability);
    }

    [Fact]
    public async Task DecideAsync_200WithSuccessFalse_Throws()
    {
        // Arrange
        var (provider, _) = Make(Fixtures.ErrorCloudflare);

        // Act
        Func<Task> act = async delegate { await provider.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Contains("5007", exception.Message, StringComparison.Ordinal);
        Assert.Contains("No such model", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_SuccessWithoutResult_Throws()
    {
        // Arrange
        var (provider, _) = Make(Fixtures.CloudflareSuccessNoResult);

        // Act
        Func<Task> act = async delegate { await provider.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Contains("without a result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_Images_DataUrlArray()
    {
        // Arrange
        var (provider, stub) = Make(Fixtures.CloudflareOk);
        var images = new Dictionary<string, SystemOne.Image>
        {
            ["photo"] = SystemOne.Factory.Image((byte[])[1], "png"),
        };

        // Act
        await provider.DecideAsync(s_request with { Images = images });

        // Assert
        Assert.Contains(
            expectedSubstring: """"images":["data:image/png;base64,AQ=="]"""",
            actualString: stub.LastBody,
            comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_RequestModel_RoutesUrlToThatModel()
    {
        // Arrange：Workers AI 的模型在網址路徑裡，所以請求指定的模型必須改寫網址
        var (provider, stub) = Make(Fixtures.CloudflareOk);

        // Act
        await provider.DecideAsync(s_request.WithModel("clef-pro"));

        // Assert
        Assert.NotNull(stub.LastRequest?.RequestUri);
        Assert.EndsWith(
            expectedEndString: "/ai/run/@cf/cloudflare/clef-pro",
            actualString: stub.LastRequest.RequestUri.AbsolutePath,
            comparisonType: StringComparison.Ordinal);
        Assert.Contains("\"model\":\"clef-pro\"", stub.LastBody, StringComparison.Ordinal);
    }
}