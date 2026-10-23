namespace SystemOneTests;

using System.Text.Json.Nodes;

public sealed class RequestBuilderTests
{
    [Fact]
    public void For_AnonymousObject_SerializesState()
    {
        // Arrange
        var state = new { amount = 12000, merchant = "7-Eleven" };

        // Act
        var request = SystemOne.Requests.Noul("big", "Large?").For(state);

        // Assert
        var amount = request.State["amount"];
        var merchant = request.State["merchant"];
        Assert.NotNull(amount);
        Assert.NotNull(merchant);
        Assert.Equal(12000, amount.GetValue<int>());
        Assert.Equal("7-Eleven", merchant.GetValue<string>());
        Assert.Single(request.Questions);
    }

    [Fact]
    public void For_String_KeepsJsonString()
    {
        // Arrange
        var state = "charged twice";

        // Act
        var request = SystemOne.Requests.Noul("q", "?").For(state);

        // Assert
        var value = Assert.IsAssignableFrom<JsonValue>(request.State);
        Assert.Equal("charged twice", value.GetValue<string>());
    }

    [Fact]
    public void Noul_FromAnchor_StartsTemplateWithoutState()
    {
        // Act
        var template = SystemOne.Requests.Noul("big", "Large?");

        // Assert
        Assert.Null(template.State);
        Assert.Equal(["big"], template.Questions.Keys);
    }

    [Fact]
    public void For_Template_ReturnsNewRequestAndKeepsTemplate()
    {
        // Arrange
        var template = SystemOne.Requests.Noul("big", "Large?");

        // Act
        var first = template.For(new { amount = 1 });
        var second = template.For(new { amount = 2 });

        // Assert
        Assert.Null(template.State);
        Assert.Equal(1, first.State["amount"]?.GetValue<int>());
        Assert.Equal(2, second.State["amount"]?.GetValue<int>());
        Assert.Equal(template.Questions.Keys, first.Questions.Keys);
    }

    [Fact]
    public void For_RequestWithState_ReplacesState()
    {
        // Arrange
        var request = SystemOne.Requests.Noul("q", "?").For("old");

        // Act
        var replaced = request.For("new");

        // Assert
        Assert.Equal("old", request.State.GetValue<string>());
        Assert.Equal("new", replaced.State.GetValue<string>());
    }

    [Fact]
    public void For_NullState_ThrowsArgumentNullException()
    {
        // Arrange
        var template = SystemOne.Requests.Noul("q", "?");

        // Act
        Action act = delegate { template.For((JsonNode)null!); };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Noul_ThenChoice_AddsBothQuestions()
    {
        // Act
        var request = SystemOne.Requests
            .Noul("big", "Large?")
            .Choice("cat", "Category", "grocery", "convenience");

        // Assert
        Assert.Equal(["big", "cat"], request.Questions.Keys);
        Assert.Equal("Large?", Assert.IsType<SystemOne.NoulQuestion>(request.Questions["big"]).Instructions);
    }

    [Fact]
    public void Choice_OptionsLambda_KeepsOrderAndDescriptions()
    {
        // Act
        var request = SystemOne.Requests.Choice("cat", null, options => options
            .Option("grocery", "supermarkets")
            .Option("convenience"));

        // Assert
        var question = Assert.IsType<SystemOne.ChoiceQuestion>(request.Questions["cat"]);
        Assert.Null(question.Instructions);
        Assert.Equal("supermarkets", question.Criteria["grocery"]);
        Assert.Equal(["grocery", "convenience"], question.Criteria.Keys);
        Assert.Null(question.Criteria["convenience"]);
    }

    [Fact]
    public void Choice_OptionsLambdaDuplicateOption_Throws()
    {
        // Act
        Action act = delegate { SystemOne.Requests.Choice("cat", null, options => options.Option("a").Option("a")); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Choice_OptionsBuilderStep_ReturnsNewBuilder()
    {
        // Act
        var request = SystemOne.Requests.Choice("cat", null, options =>
        {
            var first = options.Option("a");
            first.Option("ignored");
            return first;
        });

        // Assert
        Assert.Equal(["a"], Assert.IsType<SystemOne.ChoiceQuestion>(request.Questions["cat"]).Criteria.Keys);
    }

    [Fact]
    public void Score_Levels_KeepsOrder()
    {
        // Act
        var request = SystemOne.Requests.Score("risk", "Risk", "low", "mid", "high");

        // Assert
        var question = Assert.IsType<SystemOne.ScoreQuestion>(request.Questions["risk"]);
        Assert.Equal(["low", "mid", "high"], question.Criteria);
    }

    [Fact]
    public void Box_ExistingRequest_ReturnsNewRequestAndKeepsOriginal()
    {
        // Arrange
        var original = SystemOne.Requests.Noul("a", "?").For("x");

        // Act
        var extended = original.Box("where", "the cat");

        // Assert
        Assert.Single(original.Questions);
        Assert.Equal(2, extended.Questions.Count);
        Assert.Same(original.State, extended.State);
    }

    [Fact]
    public void Image_Bytes_AddsImageToNewRequest()
    {
        // Arrange
        var original = SystemOne.Requests.Box("where", "the cat");

        // Act
        var withImage = original.Image("photo", (byte[])[1, 2], "png");

        // Assert
        Assert.Null(original.Images);
        Assert.NotNull(withImage.Images);
        var image = Assert.Single(withImage.Images).Value;
        Assert.Equal([1, 2], image.Bytes.ToArray());
        Assert.Equal("png", image.Format);
    }

    [Fact]
    public void Image_Path_ReadsFileAndFormatFromExtension()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        File.WriteAllBytes(path, [7, 8]);

        try
        {
            // Act
            var request = SystemOne.Requests.Image("photo", path);

            // Assert
            Assert.NotNull(request.Images);
            Assert.Equal([7, 8], request.Images["photo"].Bytes.ToArray());
            Assert.Equal("png", request.Images["photo"].Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Image_SameName_ReplacesAndKeepsOthers()
    {
        // Arrange
        var request = SystemOne.Requests
            .Image("front", (byte[])[1], "png")
            .Image("back", (byte[])[2], "png");

        // Act
        var replaced = request.Image("front", (byte[])[3], "jpg");

        // Assert
        Assert.NotNull(replaced.Images);
        Assert.NotNull(request.Images);
        Assert.Equal(2, replaced.Images.Count);
        Assert.Equal("jpg", replaced.Images["front"].Format);
        Assert.Equal("png", request.Images["front"].Format);
    }

    [Fact]
    public void WithModel_Name_SetsModelOnNewRequest()
    {
        // Arrange
        var original = SystemOne.Requests.Noul("a", "?");

        // Act
        var pinned = original.WithModel("jev-preview");

        // Assert
        Assert.Null(original.Model);
        Assert.Equal("jev-preview", pinned.Model);
    }

    [Fact]
    public void WithModel_FromAnchor_StartsTemplateWithModel()
    {
        // Act
        var template = SystemOne.Requests.WithModel("jev-preview");

        // Assert
        Assert.Equal("jev-preview", template.Model);
        Assert.Empty(template.Questions);
    }

    [Fact]
    public void WithModel_Blank_ThrowsArgumentException()
    {
        // Act
        Action act = delegate { SystemOne.Requests.WithModel(" "); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Choice_BlankOptionName_Throws(string option)
    {
        // Act
        Action act = delegate { SystemOne.Requests.Choice("pick", null, "a", option); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void For_JsonNode_LaterChangesToCallerNodeDoNotLeakIn()
    {
        // Arrange
        var state = new JsonObject { ["amount"] = 1 };
        var request = SystemOne.Requests.Noul("big", "Large?").For(state);

        // Act
        state["amount"] = 2;

        // Assert
        Assert.Equal(1, request.State["amount"]?.GetValue<int>());
    }
}