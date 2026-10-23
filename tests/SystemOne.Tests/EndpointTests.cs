namespace SystemOneTests;

public sealed class EndpointTests
{
    [Fact]
    public void SelfHosted_BaseUriWithPath_ReplacesPathAndHasNoToken()
    {
        // Arrange
        var baseUri = new Uri("http://h:8000/anything");

        // Act
        var endpoint = SystemOneEndpoint.SelfHosted(baseUri, "clef-flash");

        // Assert
        Assert.Equal("http://h:8000/v1/systemone", endpoint.Uri.AbsoluteUri);
        Assert.Equal(SystemOneEndpoint.LocalityKind.SelfHosted, endpoint.Locality);
        Assert.Equal("clef-flash", endpoint.DefaultModel);
        Assert.Null(endpoint.BearerToken);
    }

    [Fact]
    public void ToString_WithToken_MasksToken()
    {
        // Arrange
        var endpoint = SystemOneEndpoint.SelfHosted(new Uri("http://h")) with { BearerToken = "sk-secret" };

        // Act
        var text = endpoint.ToString();

        // Assert
        Assert.DoesNotContain("sk-secret", text, StringComparison.Ordinal);
        Assert.Contains("***", text, StringComparison.Ordinal);
    }
}