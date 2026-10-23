namespace SystemOneTests;

public sealed class FactoryTests
{
    [Fact]
    public void Noul_Probability_Answer()
    {
        // Arrange & Act
        var answer = SystemOne.Factory.Noul(0.9);

        // Assert
        Assert.Equal(0.9, answer.Probability);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Noul_OutsideZeroToOne_ThrowsArgumentOutOfRange(double probability)
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Noul(probability); };

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Box_Coordinates_AnswerWithOptionalConfidence()
    {
        // Arrange & Act
        var plain = SystemOne.Factory.Box(top: 0.1, bottom: 0.6, left: 0.2, right: 0.9);
        var sure = SystemOne.Factory.Box(top: 0, bottom: 1, left: 0, right: 1, confidence: 0.8);

        // Assert
        Assert.Equal(0.1, plain.Bounds.Top);
        Assert.Equal(0.6, plain.Bounds.Bottom);
        Assert.Equal(0.2, plain.Bounds.Left);
        Assert.Equal(0.9, plain.Bounds.Right);
        Assert.Null(plain.Confidence);
        Assert.Equal(0.8, sure.Confidence);
    }

    [Theory]
    [InlineData(-0.1, 0.5, 0, 1)]
    [InlineData(0, 1.1, 0, 1)]
    [InlineData(0, 1, -0.1, 1)]
    [InlineData(0, 1, 0, 1.1)]
    [InlineData(double.NaN, 1, 0, 1)]
    public void Box_EdgeOutsideZeroToOne_ThrowsArgumentOutOfRange(
        double top,
        double bottom,
        double left,
        double right)
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Box(top, bottom, left, right); };

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Box_ConfidenceOutsideZeroToOne_ThrowsArgumentOutOfRange()
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Box(top: 0, bottom: 1, left: 0, right: 1, confidence: 1.5); };

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Theory]
    [InlineData(0.6, 0.1, 0, 1)]
    [InlineData(0, 1, 0.9, 0.2)]
    public void Box_EdgesReversed_ThrowsArgument(
        double top,
        double bottom,
        double left,
        double right)
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Box(top, bottom, left, right); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Choice_Probabilities_PicksHighestAndKeepsOrder()
    {
        // Arrange & Act
        var answer = SystemOne.Factory.Choice(
            ("grocery", 0.1),
            ("convenience", 0.7),
            ("pharmacy", 0.2));

        // Assert
        Assert.Equal("convenience", answer.Option);
        Assert.Equal(0.7, answer.Confidence);
        Assert.Equal(["grocery", "convenience", "pharmacy"], answer.Probabilities.Keys);
    }

    [Fact]
    public void Choice_Tie_PicksFirst()
    {
        // Arrange & Act
        var answer = SystemOne.Factory.Choice(("a", 0.5), ("b", 0.5));

        // Assert
        Assert.Equal("a", answer.Option);
    }

    [Fact]
    public void Choice_NoOptions_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Choice(); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Choice_ProbabilityAboveOne_ThrowsArgumentOutOfRange()
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Choice(("a", 1.5)); };

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Response_NamedAnswers_DictionaryAndZeroUsage()
    {
        // Arrange & Act
        var response = SystemOne.Factory.Response(
            "handler",
            ("big", SystemOne.Factory.Noul(0.9)),
            ("where", SystemOne.Factory.Box(top: 0, bottom: 1, left: 0, right: 1)));

        // Assert
        Assert.Equal("handler", response.Model);
        Assert.Equal(0.9, response.Get<SystemOne.NoulAnswer>("big").Probability);
        Assert.Equal(1, response.Get<SystemOne.BoxAnswer>("where").Bounds.Bottom);
        Assert.Equal(0, response.Usage.InputTokens);
    }

    [Fact]
    public void Response_DuplicateNames_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = delegate
        {
            SystemOne.Factory.Response(
                "m",
                ("big", SystemOne.Factory.Noul(0.1)),
                ("big", SystemOne.Factory.Noul(0.2)));
        };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Image_BytesAndFormat_Image()
    {
        // Arrange & Act
        var image = SystemOne.Factory.Image((byte[])[1, 2], "png");

        // Assert
        Assert.Equal([1, 2], image.Bytes.ToArray());
        Assert.Equal("png", image.Format);
    }

    [Fact]
    public void Image_Path_ReadsBytesAndFormatFromExtension()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".JPG");
        File.WriteAllBytes(path, [7, 8, 9]);

        try
        {
            // Act
            var image = SystemOne.Factory.Image(path);

            // Assert
            Assert.Equal([7, 8, 9], image.Bytes.ToArray());
            Assert.Equal("jpg", image.Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Image_PathWithoutExtension_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = delegate { SystemOne.Factory.Image(Path.Combine(Path.GetTempPath(), "no-extension")); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
}