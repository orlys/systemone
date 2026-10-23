namespace SystemOneTests;

public sealed partial class ModelSourceTests : IDisposable
{
    // 輔助方法建立的用戶端，測試結束時由 xUnit 呼叫 Dispose 釋放
    private readonly List<HttpClient> _clients = [];

    public void Dispose()
    {
        foreach (var http in _clients)
        {
            http.Dispose();
        }
    }

    [Fact]
    public async Task FromDirectory_Missing_ThrowsDirectoryNotFound()
    {
        // Arrange
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = SystemOne.ModelSources.FromDirectory(missing);

        // Act
        Func<Task> act = async delegate { await source.ResolveAsync(); };

        // Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(act);
    }

    [Fact]
    public async Task FromDirectory_Existing_ReturnsPath()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory("systemone-").FullName;
        var source = SystemOne.ModelSources.FromDirectory(directory);

        try
        {
            // Act
            var resolved = await source.ResolveAsync();

            // Assert
            Assert.Equal(directory, resolved);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void FromDirectory_BlankPath_ThrowsArgumentException()
    {
        // Arrange
        var path = " ";

        // Act
        Action act = delegate { SystemOne.ModelSources.FromDirectory(path); };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
}