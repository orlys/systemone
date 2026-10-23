namespace SystemOneTests;

public sealed class ResponseTests
{
    private static readonly SystemOne.Response s_response = SystemOne.Factory.Response(
        "clef-flash",
        ("big", SystemOne.Factory.Noul(0.79)));

    private static readonly SystemOne.Response s_everyType = SystemOne.Factory.Response(
        "m",
        ("big", SystemOne.Factory.Noul(0.79)),
        ("cat", SystemOne.Factory.Choice(("grocery", 0.2), ("convenience", 0.8))),
        ("risk", ThreeLevelScore()),
        ("where", SystemOne.Factory.Box(top: 0.1, bottom: 0.6, left: 0.2, right: 0.9)));

    private static SystemOne.ScoreAnswer ThreeLevelScore()
    {
        return new SystemOne.ScoreAnswer
        {
            Level = 1.5,
            Confidence = 0.6,
            Legend = new Dictionary<string, string>
            {
                ["0"] = "low",
                ["1"] = "mid",
                ["2"] = "high",
            },
            Probabilities = new Dictionary<string, double>
            {
                ["0"] = 0.1,
                ["1"] = 0.3,
                ["2"] = 0.6,
            },
        };
    }

    [Fact]
    public void TypedGetters_EveryAnswerType_ReturnTypedAnswers()
    {
        // Arrange
        var response = s_everyType;

        // Act
        var noul = response.GetNoul("big");
        var choice = response.GetChoice("cat");
        var score = response.GetScore("risk");
        var box = response.GetBox("where");

        // Assert
        Assert.Equal(0.79, noul.Probability);
        Assert.Equal("convenience", choice.Option);
        Assert.Equal(1.5, score.Level);
        Assert.Equal(0.6, box.Bounds.Bottom);
    }

    [Fact]
    public void GetChoice_NoulAnswer_ThrowsSystemOneException()
    {
        // Arrange
        var response = s_everyType;

        // Act
        Action act = delegate { response.GetChoice("big"); };

        // Assert
        var exception = Assert.Throws<SystemOneException>(act);
        Assert.Contains(
            expectedSubstring: nameof(SystemOne.ChoiceAnswer),
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public void Get_MatchingType_ReturnsAnswer()
    {
        // Arrange
        var response = s_response;

        // Act
        var answer = response.Get<SystemOne.NoulAnswer>("big");

        // Assert
        Assert.Equal(0.79, answer.Probability);
    }

    [Fact]
    public void Get_MissingName_ThrowsSystemOneException()
    {
        // Arrange
        var response = s_response;

        // Act
        Action act = delegate { response.Get<SystemOne.NoulAnswer>("nope"); };

        // Assert
        var exception = Assert.Throws<SystemOneException>(act);
        Assert.Contains("'nope'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("big", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public void Get_WrongType_ThrowsSystemOneException()
    {
        // Arrange
        var response = s_response;

        // Act
        Action act = delegate { response.Get<SystemOne.ChoiceAnswer>("big"); };

        // Assert
        var exception = Assert.Throws<SystemOneException>(act);
        Assert.Contains(
            expectedSubstring: nameof(SystemOne.NoulAnswer),
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal);
        Assert.Contains(
            expectedSubstring: nameof(SystemOne.ChoiceAnswer),
            actualString: exception.Message,
            comparisonType: StringComparison.Ordinal);
    }

    [Fact]
    public void Usage_NotSet_DefaultsToZero()
    {
        // Arrange
        var response = s_response;

        // Act
        var usage = response.Usage;

        // Assert
        Assert.Equal(0, usage.InputTokens);
        Assert.Equal(0, usage.OutputTokens);
    }
}