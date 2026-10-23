namespace SystemOneTests;

using System.Text.Json.Nodes;

public sealed class SystemOneProviderTests
{
    private static readonly SystemOne.Response s_response = SystemOne.Factory.Response(
        "m",
        ("big", SystemOne.Factory.Noul(0.5)));

    private static SystemOne.Request Valid()
    {
        return SystemOne.Requests.Noul("big", "Large?").For(new JsonObject { ["amount"] = 1 });
    }

    private static Dictionary<string, string?> Options(int count)
    {
        return Enumerable
            .Range(0, count)
            .ToDictionary(
                keySelector: i => FormattableString.Invariant($"o{i}"),
                elementSelector: delegate
                {
                    return (string?)null;
                });
    }

    [Fact]
    public async Task DecideAsync_NullRequest_ThrowsArgumentNull()
    {
        // Arrange
        var client = new RecordingProvider();

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(null!); };

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
        Assert.Null(client.Received);
    }

    [Fact]
    public async Task DecideAsync_NoQuestions_ThrowsArgumentException()
    {
        // Arrange
        var client = new RecordingProvider();
        var request = new SystemOne.Request
        {
            State = JsonValue.Create("x"),
            Questions = new Dictionary<string, SystemOne.Question>(),
        };

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(request); };

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Null(client.Received);
    }

    [Fact]
    public async Task DecideAsync_TemplateWithoutState_ThrowsArgumentException()
    {
        // Arrange
        var client = new RecordingProvider();
        var template = SystemOne.Requests.Noul("big", "Large?");

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(template); };

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains(".For(", exception.Message, StringComparison.Ordinal);
        Assert.Null(client.Received);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public async Task DecideAsync_ChoiceOptionCountOutOfRange_ThrowsArgumentException(int count)
    {
        // Arrange
        var client = new RecordingProvider();
        var request = new SystemOne.Request
        {
            State = JsonValue.Create("x"),
            Questions = new Dictionary<string, SystemOne.Question>
            {
                ["cat"] = new SystemOne.ChoiceQuestion { Criteria = Options(count) },
            },
        };

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(request); };

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("'cat'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_ScoreWithElevenLevels_ThrowsArgumentException()
    {
        // Arrange
        var client = new RecordingProvider();
        var levels = Enumerable.Range(0, 11).Select(i => FormattableString.Invariant($"l{i}")).ToArray();
        var request = SystemOne.Requests.Score("risk", null, levels).For("x");

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(request); };

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("'risk'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_EmptyImageBytes_ThrowsArgumentException()
    {
        // Arrange
        var client = new RecordingProvider();
        var request = Valid() with
        {
            Images = new Dictionary<string, SystemOne.Image>
            {
                ["photo"] = SystemOne.Factory.Image(ReadOnlyMemory<byte>.Empty, "png"),
            },
        };

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(request); };

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("'photo'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        var client = new RecordingProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> act = async delegate { await client.DecideAsync(Valid(), cancellation.Token); };

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.Null(client.Received);
    }

    [Fact]
    public async Task DecideAsync_ValidRequest_CallsCore()
    {
        // Arrange
        var client = new RecordingProvider();
        var request = Valid();

        // Act
        var response = await client.DecideAsync(request);

        // Assert
        Assert.Same(request, client.Received);
        Assert.Same(s_response, response);
    }

    private sealed class RecordingProvider : SystemOneProvider
    {
        public SystemOne.Request? Received { get; private set; }

        protected override Task<SystemOne.Response> DecideCoreAsync(
            SystemOne.Request request,
            CancellationToken cancellationToken)
        {
            Received = request;

            return Task.FromResult(s_response);
        }
    }
}