namespace SystemOneTests;

public sealed class FallbackSystemOneProviderTests
{
    private static readonly SystemOne.Request s_request = SystemOne.Requests.Noul("q", "?").For("x");

    private static MockSystemOneProvider Failing(string message)
    {
        return SystemOne.Providers.UseMock(new SystemOneException(message));
    }

    [Fact]
    public async Task DecideAsync_FirstSucceeds_SecondUntouched()
    {
        // Arrange
        var first = SystemOne.Providers.UseMock().WithModel("first");
        var second = SystemOne.Providers.UseMock().WithModel("second");
        var fallback = new FallbackSystemOneProvider([first, second]);

        // Act
        var response = await fallback.DecideAsync(s_request);

        // Assert
        Assert.Equal("first", response.Model);
        Assert.Empty(second.Requests);
    }

    [Fact]
    public async Task DecideAsync_FirstThrowsSystemOneException_MovesToSecond()
    {
        // Arrange
        var second = SystemOne.Providers.UseMock().WithModel("second");
        var fallback = new FallbackSystemOneProvider([Failing("down"), second]);

        // Act
        var response = await fallback.DecideAsync(s_request);

        // Assert
        Assert.Equal("second", response.Model);
    }

    [Fact]
    public async Task DecideAsync_Cancelled_DoesNotFallBack()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var second = SystemOne.Providers.UseMock();
        var fallback = new FallbackSystemOneProvider([SystemOne.Providers.UseMock(), second]);

        // Act
        Func<Task> act = async delegate { await fallback.DecideAsync(s_request, cancellation.Token); };

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.Empty(second.Requests);
    }

    [Fact]
    public async Task DecideAsync_AllFail_ThrowsWithEveryMessageAndLastInner()
    {
        // Arrange
        var fallback = new FallbackSystemOneProvider([Failing("one"), Failing("two")]);

        // Act
        Func<Task> act = async delegate { await fallback.DecideAsync(s_request); };

        // Assert
        var exception = await Assert.ThrowsAsync<SystemOneException>(act);
        Assert.Contains("[0] one", exception.Message, StringComparison.Ordinal);
        Assert.Contains("[1] two", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
        Assert.Equal("two", exception.InnerException.Message);
    }

    [Fact]
    public void Constructor_NoProviders_ThrowsArgumentException()
    {
        // Arrange
        ISystemOne[] providers = [];

        // Act
        Action act = delegate { _ = new FallbackSystemOneProvider(providers); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Providers_TwoProviders_ExposesConfiguredOrder()
    {
        // Arrange
        var first = SystemOne.Providers.UseMock();
        var second = SystemOne.Providers.UseMock();

        // Act
        var fallback = new FallbackSystemOneProvider([first, second]);

        // Assert
        Assert.Equal([first, second], fallback.Providers);
    }

    [Fact]
    public void UseFallback_NullProvider_Throws()
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Providers.UseFallback(SystemOne.Providers.UseMock(), null!); };

        // Assert
        Assert.ThrowsAny<ArgumentException>(act);
    }

    [Fact]
    public async Task UseFallback_CallerChangesArrayLater_ChainUnchanged()
    {
        // Arrange
        var first = SystemOne.Providers.UseMock();
        var other = SystemOne.Providers.UseMock();
        ISystemOne[] providers = [first];
        var chain = SystemOne.Providers.UseFallback(providers);

        // Act
        providers[0] = other;
        await chain.DecideAsync(SystemOne.Requests.Noul("big", "Large?").For("x"));

        // Assert
        Assert.Single(first.Requests);
        Assert.Empty(other.Requests);
    }
}