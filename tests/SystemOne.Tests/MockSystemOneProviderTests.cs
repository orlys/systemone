namespace SystemOneTests;

public sealed class MockSystemOneProviderTests
{
    private static SystemOne.Request ThreeQuestions()
    {
        return SystemOne.Requests
            .Noul("big", "?")
            .Choice("cat", null, "grocery", "convenience")
            .Score("risk", null, "low", "mid", "high")
            .For("x");
    }

    [Fact]
    public async Task Noul_DerivedMock_OriginalUnchanged()
    {
        // Arrange
        var original = SystemOne.Providers.UseMock().Noul("big", 0.9);

        // Act
        var derived = original.Noul("big", 0.1);

        // Assert
        Assert.NotSame(original, derived);
        Assert.Equal(0.9, (await original.DecideAsync(ThreeQuestions())).Get<SystemOne.NoulAnswer>("big").Probability);
        Assert.Equal(0.1, (await derived.DecideAsync(ThreeQuestions())).Get<SystemOne.NoulAnswer>("big").Probability);
    }

    [Fact]
    public async Task DecideAsync_DerivedMock_RecordsSeparately()
    {
        // Arrange
        var original = SystemOne.Providers.UseMock();
        var derived = original.WithModel("fake");

        // Act
        await derived.DecideAsync(ThreeQuestions());

        // Assert
        Assert.Empty(original.Requests);
        Assert.Single(derived.Requests);
    }

    [Fact]
    public async Task DecideAsync_Answer_ReturnedAsIs()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Answer("big", SystemOne.Factory.Noul(0.99));

        // Act
        var response = await mock.DecideAsync(ThreeQuestions());

        // Assert
        Assert.Equal(0.99, response.Get<SystemOne.NoulAnswer>("big").Probability);
        Assert.Equal("mock", response.Model);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Noul_OutsideZeroToOne_ThrowsArgumentOutOfRange(double probability)
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock();

        // Act
        Action act = delegate { mock.Noul("big", probability); };

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public async Task DecideAsync_Choice_ProbabilitiesSpanEveryOption()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Choice("cat", "convenience");

        // Act
        var cat = (await mock.DecideAsync(ThreeQuestions())).Get<SystemOne.ChoiceAnswer>("cat");

        // Assert
        Assert.Equal("convenience", cat.Option);
        Assert.Equal(1, cat.Confidence);
        Assert.Equal(0, cat.Probabilities["grocery"]);
        Assert.Equal(1, cat.Probabilities["convenience"]);
    }

    [Fact]
    public async Task DecideAsync_Score_LegendAndProbabilitiesFromCriteria()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Score("risk", 2);

        // Act
        var risk = (await mock.DecideAsync(ThreeQuestions())).Get<SystemOne.ScoreAnswer>("risk");

        // Assert
        Assert.Equal(2, risk.Level);
        Assert.Equal("high", risk.Legend["2"]);
        Assert.Equal(1, risk.Probabilities["2"]);
        Assert.Equal(0, risk.Probabilities["0"]);
    }

    [Fact]
    public async Task DecideAsync_ChoiceWithUnknownOption_ThrowsInvalidOperation()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Choice("cat", "pharmacy");

        // Act
        Func<Task> act = async delegate { await mock.DecideAsync(ThreeQuestions()); };

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("pharmacy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_ScoreBeyondLevels_ThrowsInvalidOperation()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Score("risk", 3);

        // Act
        Func<Task> act = async delegate { await mock.DecideAsync(ThreeQuestions()); };

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task DecideAsync_Box_ReturnsConfiguredBounds()
    {
        // Arrange
        var mock = SystemOne.Providers
            .UseMock()
            .Box(
                "where",
                top: 0.1,
                bottom: 0.6,
                left: 0.2,
                right: 0.9);
        var request = SystemOne.Requests.Box("where", "the cat").For("photo");

        // Act
        var response = await mock.DecideAsync(request);

        // Assert
        var bounds = response.GetBox("where").Bounds;
        Assert.Equal((0.1, 0.6, 0.2, 0.9), (bounds.Top, bounds.Bottom, bounds.Left, bounds.Right));
    }

    [Fact]
    public async Task DecideAsync_BoxForNonBoxQuestion_ThrowsInvalidOperation()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Box("big", top: 0, bottom: 1, left: 0, right: 1);

        // Act
        Func<Task> act = async delegate { await mock.DecideAsync(ThreeQuestions()); };

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task DecideAsync_AnswerForWrongQuestionType_ThrowsInvalidOperation()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Choice("big", "yes");

        // Act
        Func<Task> act = async delegate { await mock.DecideAsync(ThreeQuestions()); };

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("big", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_UnconfiguredQuestions_DeterministicDefaultsShapedByCriteria()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock();

        // Act
        var response = await mock.DecideAsync(ThreeQuestions());

        // Assert
        Assert.Equal(0.5, response.Get<SystemOne.NoulAnswer>("big").Probability);
        var cat = response.Get<SystemOne.ChoiceAnswer>("cat");
        Assert.Equal("grocery", cat.Option);
        Assert.Equal(["grocery", "convenience"], cat.Probabilities.Keys);
        Assert.Equal(0.5, cat.Confidence);
        var risk = response.Get<SystemOne.ScoreAnswer>("risk");
        Assert.Equal(1, risk.Level);
        Assert.Equal("mid", risk.Legend["1"]);
        Assert.Equal(3, risk.Probabilities.Count);
        Assert.Equal(0, response.Usage.InputTokens);
    }

    [Fact]
    public async Task DecideAsync_Model_UsedUnlessRequestOverrides()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().WithModel("fake");

        // Act
        var configured = await mock.DecideAsync(ThreeQuestions());
        var overridden = await mock.DecideAsync(ThreeQuestions() with { Model = "custom" });

        // Assert
        Assert.Equal("fake", configured.Model);
        Assert.Equal("custom", overridden.Model);
    }

    [Fact]
    public async Task DecideAsync_EveryCall_Recorded()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock();
        var request = ThreeQuestions();

        // Act
        await mock.DecideAsync(request);
        await mock.DecideAsync(request);

        // Assert
        Assert.Equal(2, mock.Requests.Count);
        Assert.Same(request, mock.Requests[0]);
    }

    [Fact]
    public async Task DecideAsync_ExceptionMock_ThrowsOnEveryCall()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock(new SystemOneException("outage"));

        // Act
        Func<Task> act = async delegate { await mock.DecideAsync(ThreeQuestions()); };

        // Assert
        Assert.Equal("outage", (await Assert.ThrowsAsync<SystemOneException>(act)).Message);
        Assert.Equal("outage", (await Assert.ThrowsAsync<SystemOneException>(act)).Message);
        Assert.Equal(2, mock.Requests.Count);
    }

    [Fact]
    public async Task DecideAsync_HandlerMock_FirstSucceedsThenFails()
    {
        // Arrange
        var calls = default(int);
        var mock = SystemOne.Providers.UseMock(delegate
        {
            if (Interlocked.Increment(ref calls) > 1)
            {
                throw new SystemOneException("down after first call");
            }

            return SystemOne.Factory.Response("handler");
        });

        // Act
        var first = await mock.DecideAsync(ThreeQuestions());
        Func<Task> second = async delegate { await mock.DecideAsync(ThreeQuestions()); };

        // Assert
        Assert.Equal("handler", first.Model);
        await Assert.ThrowsAsync<SystemOneException>(second);
    }

    [Fact]
    public void Noul_OnExceptionMock_ThrowsInvalidOperation()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock(new SystemOneException("outage"));

        // Act
        Action act = delegate { mock.Noul("big", 0.9); };

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public async Task DecideAsync_BoxQuestion_DefaultsToWholeImage()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock();
        var request = SystemOne.Requests.Box("where", null).For("photo");

        // Act
        var box = (await mock.DecideAsync(request)).Get<SystemOne.BoxAnswer>("where");

        // Assert
        Assert.Equal(0, box.Bounds.Top);
        Assert.Equal(1, box.Bounds.Bottom);
        Assert.Null(box.Confidence);
    }

    [Fact]
    public async Task DecideAsync_ConcurrentCalls_RecordsEveryRequest()
    {
        // Arrange
        var mock = SystemOne.Providers.UseMock().Noul("big", 0.9);
        var request = ThreeQuestions();

        // Act
        var calls = new Task[200];

        for (var i = 0; i < calls.Length; i++)
        {
            calls[i] = Task.Run(delegate
            {
                return mock.DecideAsync(request);
            });
        }

        await Task.WhenAll(calls);

        // Assert
        Assert.Equal(200, mock.Requests.Count);
    }
}