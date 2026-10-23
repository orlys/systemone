namespace SystemOneTests;

using System.Text.Json.Nodes;

public sealed class CancellationTests
{
    private static readonly SystemOne.Request s_request = SystemOne.Requests
        .Noul("big", "Large?")
        .For(new JsonObject { ["amount"] = 12000 });

    [Fact]
    public async Task DecideAsync_CancelledWhileSending_ThrowsOperationCanceled()
    {
        // Arrange
        var handler = new HangingHandler();
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var provider = SystemOne.Providers.UseSelfHosted(
            baseUri: new Uri("http://h:8000"),
            model: "m",
            http);
        using var cancellation = new CancellationTokenSource();

        // Act
        var call = provider.DecideAsync(s_request, cancellation.Token);
        await handler.Started;
        await cancellation.CancelAsync();
        Func<Task> act = async delegate { await call; };

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task UseFallback_CancelledWhileSending_StopsChain()
    {
        // Arrange
        var handler = new HangingHandler();
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var backup = SystemOne.Providers.UseMock();
        var chain = SystemOne.Providers.UseFallback(
            SystemOne.Providers.UseSelfHosted(
                baseUri: new Uri("http://h:8000"),
                model: "m",
                http),
            backup);
        using var cancellation = new CancellationTokenSource();

        // Act
        var call = chain.DecideAsync(s_request, cancellation.Token);
        await handler.Started;
        await cancellation.CancelAsync();
        Func<Task> act = async delegate { await call; };

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.Empty(backup.Requests);
    }

    // 送出後一直等到被取消，用來測試請求進行中的取消
    private sealed class HangingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started
        {
            get
            {
                return _started.Task;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);

            throw new InvalidOperationException("Unreachable: the delay only ends by cancellation.");
        }
    }
}