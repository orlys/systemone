namespace SystemOneTests;

using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class SerializationTests
{
    private static readonly Dictionary<string, SystemOne.Question> s_threeQuestions = new()
    {
        ["big"] = new SystemOne.NoulQuestion { Instructions = "Large?" },
        ["cat"] = new SystemOne.ChoiceQuestion
        {
            Criteria = new Dictionary<string, string?>
            {
                ["grocery"] = "supermarkets",
                ["convenience"] = null,
            },
        },
        ["risk"] = new SystemOne.ScoreQuestion { Instructions = "Risk", Criteria = ["low", "high"] },
    };

    private static string Serialize(SystemOne.Request request)
    {
        return JsonSerializer.Serialize(request, SystemOne.JsonContext.Default.Request);
    }

    private static SystemOne.Response Deserialize(string json)
    {
        if (JsonSerializer.Deserialize(json, SystemOne.JsonContext.Default.Response) is not { } response)
        {
            // 本文是 JSON 的 null，反序列化不出回應，視為測試資料錯誤
            throw new InvalidOperationException("null response");
        }

        return response;
    }

    [Fact]
    public void Serialize_ThreeQuestions_CanonicalWireFormat()
    {
        // Arrange
        var request = new SystemOne.Request
        {
            Model = "clef-flash",
            State = new JsonObject { ["amount"] = 12000 },
            Questions = s_threeQuestions,
        };

        // Act
        var json = Serialize(request);

        // Assert
        var expected =
            """{"model":"clef-flash","state":{"amount":12000},"questions":{"big":{"type":"noul",""" +
            "\"instructions\":\"Large?\"}" +
            ""","cat":{"type":"choice","criteria":{"grocery":"supermarkets","convenience":null}}""" +
            ""","risk":{"type":"score","criteria":["low","high"],"instructions":"Risk"}}}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void Serialize_StringState_KeepsJsonStringAndOmitsNullModel()
    {
        // Arrange
        var request = SystemOne.Requests.Noul("q", "?").For("charged twice");

        // Act
        var json = Serialize(request);

        // Assert
        Assert.StartsWith("""{"state":"charged twice",""", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"model\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_BoxWithoutInstructions_TypeOnly()
    {
        // Arrange
        var request = SystemOne.Requests.Box("where", null).For("photo");

        // Act
        var json = Serialize(request);

        // Assert
        Assert.Contains("""{"type":"box"}""", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WithImages_DoesNotEmitImages()
    {
        // Arrange
        var request = SystemOne.Requests.Noul("q", "?").For("photo") with
        {
            Images = new Dictionary<string, SystemOne.Image>
            {
                ["photo"] = SystemOne.Factory.Image((byte[])[1], "png"),
            },
        };

        // Act
        var json = Serialize(request);

        // Assert
        Assert.DoesNotContain("images", json, StringComparison.Ordinal);
        Assert.DoesNotContain("AQ==", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ClefFixture_TypedAnswers()
    {
        // Arrange
        var json = Fixtures.ClefMulti;

        // Act
        var response = Deserialize(json);

        // Assert
        Assert.Equal("clef-flash", response.Model);
        Assert.Equal(0.7931, Assert.IsType<SystemOne.NoulAnswer>(response.Answers["big"]).Probability);
        var cat = Assert.IsType<SystemOne.ChoiceAnswer>(response.Answers["cat"]);
        Assert.Equal("convenience", cat.Option);
        Assert.Equal(0.966, cat.Probabilities["convenience"]);
        var risk = Assert.IsType<SystemOne.ScoreAnswer>(response.Answers["risk"]);
        Assert.Equal(1.1739, risk.Level);
        Assert.Equal("3", risk.Legend["2"]);
        Assert.Equal(305, response.Usage.InputTokens);
        Assert.Equal(0, response.Usage.OutputTokens);
    }

    [Fact]
    public void Deserialize_TypeSafeFixture_ModelAndUsage()
    {
        // Arrange
        var json = Fixtures.TypeSafeMulti;

        // Act
        var response = Deserialize(json);

        // Assert
        Assert.Equal("jev-1.13.0", response.Model);
        Assert.Equal(62, response.Usage.OutputTokens);
        Assert.Equal("billing", Assert.IsType<SystemOne.ChoiceAnswer>(response.Answers["team"]).Option);
    }

    [Fact]
    public void Deserialize_BoxFixture_NullConfidence()
    {
        // Arrange
        var json = Fixtures.TypeSafeBox;

        // Act
        var box = Assert.IsType<SystemOne.BoxAnswer>(Deserialize(json).Answers["where"]);

        // Assert
        Assert.Equal(0.1, box.Bounds.Top);
        Assert.Equal(0.6, box.Bounds.Bottom);
        Assert.Equal(0.2, box.Bounds.Left);
        Assert.Equal(0.7, box.Bounds.Right);
        Assert.Null(box.Confidence);
    }

    [Fact]
    public void Deserialize_TypeNotFirst_StillResolvesAnswer()
    {
        // Arrange
        var json = Fixtures.TypeNotFirst;

        // Act
        var response = Deserialize(json);

        // Assert
        Assert.Equal(0.5, Assert.IsType<SystemOne.NoulAnswer>(response.Answers["big"]).Probability);
    }

    [Fact]
    public void Deserialize_UnknownTopLevelFields_Ignored()
    {
        // Arrange
        var json = Fixtures.WithProviderMetadata;

        // Act
        var response = Deserialize(json);

        // Assert
        Assert.Equal("typesafe-ai/jev", response.Model);
    }

    [Fact]
    public void Deserialize_WithoutUsage_DefaultsToZeroTokens()
    {
        // Arrange
        var json = Fixtures.WithoutUsage;

        // Act
        var response = Deserialize(json);

        // Assert
        Assert.Equal(0, response.Usage.InputTokens);
        Assert.Equal(0.5, response.Get<SystemOne.NoulAnswer>("big").Probability);
    }

    [Fact]
    public void Deserialize_UsageNull_DefaultsToZeroTokens()
    {
        // Arrange
        var json = Fixtures.UsageNull;

        // Act
        var response = Deserialize(json);

        // Assert
        Assert.Equal(0, response.Usage.InputTokens);
    }

    [Fact]
    public void Deserialize_AnswersNull_GetThrowsSystemOneException()
    {
        // Arrange
        var response = Deserialize(Fixtures.AnswersNull);

        // Act
        Action act = delegate { response.Get<SystemOne.NoulAnswer>("big"); };

        // Assert
        Assert.Throws<SystemOneException>(act);
    }

    [Fact]
    public void Deserialize_UnknownAnswerType_ThrowsJsonException()
    {
        // Arrange
        var json = Fixtures.UnknownAnswerType;

        // Act
        Action act = delegate { Deserialize(json); };

        // Assert
        Assert.Throws<JsonException>(act);
    }

    [Fact]
    public void Deserialize_MissingAnswerType_ThrowsNotSupported()
    {
        // Arrange
        var json = Fixtures.MissingAnswerType;

        // Act
        Action act = delegate { Deserialize(json); };

        // Assert
        Assert.Throws<NotSupportedException>(act);
    }
}