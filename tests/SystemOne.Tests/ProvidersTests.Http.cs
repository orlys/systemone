namespace SystemOneTests;

public sealed partial class ProvidersTests
{
    private static readonly Uri s_base = new("http://h:8000");

    [Fact]
    public void UseClef_BaseUri_SelfHostedClefFlashEndpoint()
    {
        // Arrange & Act
        var client = Assert.IsType<HttpSystemOneProvider>(SystemOne.Providers.UseClef(s_base));

        // Assert
        Assert.Equal("http://h:8000/v1/systemone", client.Endpoint.Uri.AbsoluteUri);
        Assert.Equal("clef-flash", client.Endpoint.DefaultModel);
        Assert.Equal(SystemOneEndpoint.LocalityKind.SelfHosted, client.Endpoint.Locality);
    }

    [Fact]
    public void UseSelfHosted_Model_PassedThrough()
    {
        // Arrange & Act
        var client = Assert.IsType<HttpSystemOneProvider>(SystemOne.Providers.UseSelfHosted(s_base, "kev"));

        // Assert
        Assert.Equal("kev", client.Endpoint.DefaultModel);
    }

    [Fact]
    public void UseSelfHosted_DefaultHttpClient_HasTimeout()
    {
        // Arrange & Act
        var client = Assert.IsType<HttpSystemOneProvider>(SystemOne.Providers.UseSelfHosted(s_base, "m"));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(60), client.HttpClient.Timeout);
    }

    [Fact]
    public void HostedFactories_Keys_ReturnVendorProviders()
    {
        // Arrange & Act
        var typeSafe = Assert.IsType<TypeSafeSystemOneProvider>(SystemOne.Providers.UseTypeSafeAi("k"));
        var cloudflare = Assert.IsType<CloudflareSystemOneProvider>(
            SystemOne.Providers.UseCloudflareWorkersAi("acc", "tok"));
        var openRouter = Assert.IsType<HttpSystemOneProvider>(
            SystemOne.Providers.UseOpenRouter("k", "cloudflare/clef-flash"));
        var vercel = Assert.IsType<HttpSystemOneProvider>(
            SystemOne.Providers.UseVercelAiGateway("k", "typesafe-ai/jev"));

        // Assert
        Assert.Equal("jev-latest", typeSafe.Endpoint.DefaultModel);
        Assert.Equal("tok", cloudflare.Endpoint.BearerToken);
        Assert.Equal("https://openrouter.ai/api/v1/systemone", openRouter.Endpoint.Uri.AbsoluteUri);
        Assert.Equal("cloudflare/clef-flash", openRouter.Endpoint.DefaultModel);
        Assert.Equal("https://ai-gateway.vercel.sh/typesafe/v1/systemone", vercel.Endpoint.Uri.AbsoluteUri);
        Assert.Equal("typesafe-ai/jev", vercel.Endpoint.DefaultModel);
        Assert.Same(SystemOneProvider.DefaultHttpClient, vercel.HttpClient);
    }

    [Fact]
    public void UseSelfHosted_CustomHttpClient_Used()
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(delegate
        {
            throw new InvalidOperationException();
        }));

        // Act
        var client = Assert.IsType<HttpSystemOneProvider>(
            SystemOne.Providers.UseSelfHosted(
                baseUri: s_base,
                model: "m",
                http));

        // Assert
        Assert.Same(http, client.HttpClient);
    }

    [Fact]
    public void UseMockAndUseFallback_Called_ReturnConfiguredTypes()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock();

        // Act
        var fallback = Assert.IsType<FallbackSystemOneProvider>(SystemOne.Providers.UseFallback(mock, mock));

        // Assert
        Assert.IsType<MockSystemOneProvider>(mock);
        Assert.Equal(2, fallback.Providers.Count);
    }
}