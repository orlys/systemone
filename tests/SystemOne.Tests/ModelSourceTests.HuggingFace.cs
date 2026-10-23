namespace SystemOneTests;

using System.Net;
using System.Security.Cryptography;
using System.Text;

public sealed partial class ModelSourceTests
{
    private static readonly byte[] s_weights = Encoding.UTF8.GetBytes("fake weights");

    private static string Sha256Hex(byte[] bytes)
    {
        // 期望的 LFS oid（規則 076 的 stated 例外：比照受測端，以 BCL 的 SHA-256 驗證下載完整性）
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string Tree(string sha256)
    {
        return FormattableString.Invariant($$$"""
            [{"type":"file","path":"multilingual/model.safetensors","size":{{{s_weights.Length}}},
              "lfs":{"oid":"{{{sha256}}}","size":{{{s_weights.Length}}},"pointerSize":134} },
             {"type":"file","path":"multilingual/config.json","size":2},
             {"type":"directory","path":"multilingual/tokenizer"},
             {"type":"file","path":"multilingual/tokenizer/vocab.json","size":2}]
            """);
    }

    private (SystemOne.ModelSource Source, StubHandler Stub, string Cache) HuggingFace(string sha256)
    {
        var cache = Directory.CreateTempSubdirectory("systemone-hf-").FullName;
        var stub = new StubHandler(request =>
        {
            Assert.NotNull(request.RequestUri);
            var path = request.RequestUri.AbsolutePath;

            if (path.Contains("/api/models/", StringComparison.Ordinal))
            {
                return StubHandler.Json(HttpStatusCode.OK, Tree(sha256));
            }

            if (path.EndsWith(".json", StringComparison.Ordinal))
            {
                return StubHandler.Bytes("{}"u8.ToArray());
            }

            return StubHandler.Bytes(s_weights);
        });
        var http = new HttpClient(stub);
        _clients.Add(http);
        var source = SystemOne.ModelSources.FromHuggingFace(
            repository: "convaiinnovations/laya",
            subfolder: "multilingual",
            cacheDirectory: cache,
            http: http);

        return (source, stub, cache);
    }

    [Fact]
    public async Task FromHuggingFace_FreshCache_DownloadsAndChecksEveryFile()
    {
        // Arrange
        var (source, stub, cache) = HuggingFace(Sha256Hex(s_weights));

        try
        {
            // Act
            var directory = await source.ResolveAsync();

            // Assert
            Assert.Equal(
                expected: s_weights,
                actual: await File.ReadAllBytesAsync(Path.Combine(directory, "model.safetensors")));
            Assert.True(File.Exists(Path.Combine(directory, "config.json")));
            Assert.True(File.Exists(Path.Combine(directory, "tokenizer", "vocab.json")));
            Assert.Equal(4, stub.Calls);
            Assert.StartsWith(cache, directory, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FromHuggingFace_SecondResolve_NoNetwork()
    {
        // Arrange
        var (source, stub, cache) = HuggingFace(Sha256Hex(s_weights));

        try
        {
            await source.ResolveAsync();
            var callsAfterFirst = stub.Calls;

            // Act
            var directory = await source.ResolveAsync();

            // Assert
            Assert.Equal(callsAfterFirst, stub.Calls);
            Assert.True(File.Exists(Path.Combine(directory, "model.safetensors")));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FromHuggingFace_Sha256Mismatch_ThrowsAndLeavesNoFile()
    {
        // Arrange
        var (source, _, cache) = HuggingFace(Sha256Hex(Encoding.UTF8.GetBytes("other")));

        try
        {
            // Act
            Func<Task> act = async delegate { await source.ResolveAsync(); };

            // Assert
            var exception = await Assert.ThrowsAsync<SystemOneException>(act);
            Assert.Contains("sha256", exception.Message, StringComparison.OrdinalIgnoreCase);
            var leftovers = Directory.GetFiles(
                path: cache,
                searchPattern: "model.safetensors",
                searchOption: SearchOption.AllDirectories);
            Assert.Empty(leftovers);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FromHuggingFace_ListingFails_ThrowsWithStatus()
    {
        // Arrange
        var cache = Directory.CreateTempSubdirectory("systemone-hf-").FullName;
        var stub = new StubHandler(delegate
        {
            return StubHandler.Json(HttpStatusCode.NotFound, """{"error":"Repository not found"}""");
        });
        using var http = new HttpClient(stub);
        var source = SystemOne.ModelSources.FromHuggingFace(
            repository: "nope/nope",
            cacheDirectory: cache,
            http: http);

        try
        {
            // Act
            Func<Task> act = async delegate { await source.ResolveAsync(); };

            // Assert
            var exception = await Assert.ThrowsAsync<SystemOneException>(act);
            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }
}