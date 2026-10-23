namespace SystemOneTests;

using System.Net;

public sealed partial class ModelSourceTests
{
    private (SystemOne.ModelSource Source, string Cache) FailingHuggingFace(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var cache = Directory.CreateTempSubdirectory("systemone-hf-").FullName;
        var http = new HttpClient(new StubHandler(respond));
        _clients.Add(http);
        var source = SystemOne.ModelSources.FromHuggingFace(
            repository: "convaiinnovations/laya",
            cacheDirectory: cache,
            http: http);

        return (source, cache);
    }

    [Fact]
    public async Task FromHuggingFace_HttpClientTimeout_ThrowsSystemOneException()
    {
        // Arrange：HttpClient.Timeout 以 TaskCanceledException 浮出，但呼叫端的權杖沒有取消
        var (source, cache) = FailingHuggingFace(delegate
        {
            throw new TaskCanceledException("HttpClient.Timeout elapsed");
        });

        try
        {
            // Act
            Func<Task> act = async delegate { await source.ResolveAsync(); };

            // Assert
            var exception = await Assert.ThrowsAsync<SystemOneException>(act);
            Assert.IsType<TaskCanceledException>(exception.InnerException);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FromHuggingFace_ListingIsNotJson_ThrowsSystemOneException()
    {
        // Arrange：Proxy 伺服器或登入頁回了 200 的 HTML
        var (source, cache) = FailingHuggingFace(delegate
        {
            return StubHandler.Html(HttpStatusCode.OK, "<html></html>");
        });

        try
        {
            // Act
            Func<Task> act = async delegate { await source.ResolveAsync(); };

            // Assert
            await Assert.ThrowsAsync<SystemOneException>(act);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1e30")]
    public async Task FromHuggingFace_ListingSizeNotInteger_ThrowsSystemOneException(string size)
    {
        // Arrange：檔案清單的 size 不是 long 範圍內的整數
        var (source, cache) = FailingHuggingFace(delegate
        {
            return StubHandler.Json(
                status: HttpStatusCode.OK,
                body: $$"""[{"type":"file","path":"a.bin","size":{{size}}}]""");
        });

        try
        {
            // Act
            Func<Task> act = async delegate { await source.ResolveAsync(); };

            // Assert
            var exception = await Assert.ThrowsAsync<SystemOneException>(act);
            Assert.IsType<FormatException>(exception.InnerException);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Theory]
    [InlineData("convaiinnovations/laya", "../../escaped", null)]
    [InlineData("convaiinnovations/laya", "main", "../outside")]
    [InlineData("convaiinnovations/laya", "main", "C:/rooted")]
    [InlineData("../laya", "main", null)]
    [InlineData("owner/name/extra", "main", null)]
    public void FromHuggingFace_PathOutsideCache_ThrowsArgumentException(
        string repository,
        string revision,
        string? subfolder)
    {
        // Arrange & Act
        Action act = delegate
        {
            SystemOne.ModelSources.FromHuggingFace(
                repository,
                subfolder,
                revision,
                cacheDirectory: "cache");
        };

        // Assert
        Assert.ThrowsAny<ArgumentException>(act);
    }
}