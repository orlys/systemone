using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>從 Hugging Face 下載權重到快取目錄</summary>
/// <remarks>LFS 檔以 API 提供的 sha256 驗證，小檔與已在快取的檔案只比對大小；快取完整時不連網</remarks>
public sealed class HuggingFaceModelSource : SystemOne.ModelSource
{
    // 完整下載後寫入目標目錄；存在且每個檔案大小相符就直接視為快取命中
    private const string MANIFEST_NAME = ".systemone-manifest.json";

    private readonly HttpClient _http;
    private readonly string _repository;
    private readonly string? _subfolder;
    private readonly string _revision;
    private readonly string _cacheDirectory;

    /// <summary>建立來源</summary>
    /// <param name="http">下載用的 <see cref="HttpClient"/></param>
    /// <param name="repository">例如 <c>convaiinnovations/laya</c></param>
    /// <param name="subfolder">存放庫內的子目錄；省略為根目錄</param>
    /// <param name="revision">分支、標籤或認可</param>
    /// <param name="cacheDirectory">快取根目錄；省略為 <c>%LOCALAPPDATA%/systemone/models</c></param>
    /// <exception cref="ArgumentNullException"><paramref name="http"/> 為 <see langword="null"/></exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> 不是 <c>name</c> 或 <c>owner/name</c>、<paramref name="revision"/> 為空白，
    /// 或 <paramref name="revision"/>、<paramref name="subfolder"/> 是絕對路徑或含 <c>..</c> 等會逃出快取目錄的片段
    /// </exception>
    internal HuggingFaceModelSource(
        HttpClient http,
        string repository,
        string? subfolder,
        string revision,
        string? cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        EnsureRepository(repository);
        EnsureRelativePath(revision, nameof(revision));

        if (subfolder?.Trim('/') is { Length: > 0 } relative)
        {
            EnsureRelativePath(relative, nameof(subfolder));
        }

        _http = http;
        _repository = repository;
        _subfolder = subfolder?.Trim('/') is { Length: > 0 } trimmed ? trimmed : null;
        _revision = revision;
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "systemone",
            "models");
    }

    /// <inheritdoc/>
    /// <exception cref="SystemOneException">列檔或下載失敗、sha256 或大小不符、路徑逃出快取目錄</exception>
    /// <exception cref="HttpRequestException">下載失敗後讀取錯誤本文時連線中斷</exception>
    /// <exception cref="IOException">讀取下載內容或寫入快取目錄失敗</exception>
    /// <exception cref="UnauthorizedAccessException">沒有讀寫快取目錄的權限</exception>
    /// <exception cref="InvalidOperationException">無法判斷下載目的地所在的目錄</exception>
    /// <exception cref="OperationCanceledException">已取消</exception>
    public override async Task<string> ResolveAsync(
        IProgress<SystemOne.ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 目標目錄：{cache}/{repository 以 '/' 換 '_'}/{revision}/{subfolder}
        var target = Path.GetFullPath(Path.Combine(
            _cacheDirectory,
            _repository.Replace('/', '_'),
            _revision,
            _subfolder ?? string.Empty));

        if (IsComplete(target))
        {
            return target;
        }

        Directory.CreateDirectory(target);

        // 列檔（遞迴）：缺的或大小不符的才下載
        var entries = await ListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            var destination = Destination(target, entry);

            if (File.Exists(destination) && (new FileInfo(destination).Length == entry.Size))
            {
                continue;
            }

            var download = DownloadAsync(entry, destination, progress, cancellationToken);
            await download.ConfigureAwait(false);
        }

        await WriteManifestAsync(target, entries, cancellationToken).ConfigureAwait(false);

        return target;
    }

    private static bool IsComplete(string target)
    {
        var manifestPath = Path.Combine(target, MANIFEST_NAME);

        if (!File.Exists(manifestPath))
        {
            // 沒有資訊清單：上次沒有完整下載
            return false;
        }

        try
        {
            var json = File.ReadAllBytes(manifestPath);

            if (JsonSerializer.Deserialize<List<ManifestEntry>>(json) is not { } manifest)
            {
                // 資訊清單的內容是 JSON null
                return false;
            }

            foreach (var item in manifest)
            {
                if ((item is not { Path: { } relative }) ||
                    (new FileInfo(Path.Combine(target, relative)) is not { Exists: true } file) ||
                    (file.Length != item.Size))
                {
                    // 項目或路徑是 JSON null（會繞過 required），或列出的檔案不見了、大小不符
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            // 資訊清單壞了就重新列檔
            return false;
        }
    }

    private static async Task WriteManifestAsync(
        string target,
        List<Entry> entries,
        CancellationToken cancellationToken)
    {
        var manifest = entries.Select(entry => new ManifestEntry
        {
            Path = entry.RelativePath,
            Size = entry.Size,
        });
        var file = File.Create(Path.Combine(target, MANIFEST_NAME));

        await using (file.ConfigureAwait(false))
        {
            await JsonSerializer
                .SerializeAsync(file, manifest, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string Destination(string target, Entry entry)
    {
        var destination = Path.GetFullPath(Path.Combine(target, entry.RelativePath));

        if (!destination.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            // 伺服器給的路徑逃出了目標目錄
            throw new SystemOneException($"Refusing path outside the cache directory: {entry.Path}");
        }

        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException($"Cannot determine the directory of {destination}.");
        Directory.CreateDirectory(directory);

        return destination;
    }

    // 存放庫名稱會成為快取路徑的一部分：只接受 name 或 owner/name，每段以英數開頭
    private static void EnsureRepository(string repository)
    {
        foreach (var segment in repository.Split("/", StringSplitOptions.None))
        {
            if ((segment is not { Length: > 0 }) || !char.IsAsciiLetterOrDigit(segment[0]) ||
                !segment.All(c => char.IsAsciiLetterOrDigit(c) || (c is '.' or '_' or '-')))
            {
                // 例如 "../x"：組進快取路徑後會寫到快取目錄外
                throw new ArgumentException(
                    message: $"Repository must be 'name' or 'owner/name': {repository}",
                    paramName: nameof(repository));
            }
        }

        if (repository.Count(c => c is '/') > 1)
        {
            // Hugging Face 的存放庫最多兩段
            throw new ArgumentException(
                message: $"Repository must be 'name' or 'owner/name': {repository}",
                paramName: nameof(repository));
        }
    }

    // revision 與 subfolder 也會組進快取路徑：拒絕絕對路徑、磁碟代號、反斜線與 "."、".." 片段
    private static void EnsureRelativePath(string value, string paramName)
    {
        var segments = value.Split("/", StringSplitOptions.None);

        if (Path.IsPathRooted(value) || value.Contains('\\') || value.Contains(':') ||
            segments.Any(segment => segment is "" or "." or ".."))
        {
            // 組進快取路徑後會指向快取目錄外，或根本不是相對路徑
            throw new ArgumentException(
                message: $"'{value}' must be a relative path that stays inside the cache directory.",
                paramName: paramName);
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> GetAsync(
        Uri uri,
        HttpCompletionOption completion,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.GetAsync(uri, completion, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // 傳輸層失敗與決策路徑一致，包成 SystemOneException
            throw new SystemOneException(
                message: $"Request to {uri} failed: {exception.Message}",
                innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // 不是呼叫端取消，而是 HttpClient.Timeout 到了：與決策路徑一致，當成遠端失敗
            throw new SystemOneException(
                message: FormattableString.Invariant($"Request to {uri} timed out after {_http.Timeout}."),
                innerException: exception);
        }
    }

    private async Task<List<Entry>> ListAsync(CancellationToken cancellationToken)
    {
        // 取樹狀列表：GET /api/models/{repository}/tree/{revision}/{subfolder}?recursive=true
        var path = _subfolder is null ? string.Empty : $"/{_subfolder}";
        var uri = new Uri($"https://huggingface.co/api/models/{_repository}/tree/{_revision}{path}?recursive=true");
        var responseTask = GetAsync(
            uri,
            completion: HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        using var response = await responseTask.ConfigureAwait(false);
        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 存放庫或修訂不存在、私有未授權
            throw new SystemOneException(
                message: $"Cannot list {_repository}@{_revision}: {ErrorText(body)}",
                statusCode: response.StatusCode,
                responseBody: body);
        }

        try
        {
            return ParseEntries(body);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            // 200 卻不是預期的檔案清單，例如 Proxy 伺服器或登入頁回了 HTML，或 size 不是整數
            throw new SystemOneException(
                message: $"Cannot read the file list of {_repository}@{_revision}: {exception.Message}",
                statusCode: response.StatusCode,
                responseBody: body,
                innerException: exception);
        }
    }

    private List<Entry> ParseEntries(string body)
    {
        // 只收 file 項目；LFS 檔的 sha256 在 lfs.oid，小檔沒有
        var prefix = _subfolder is null ? string.Empty : _subfolder + "/";
        List<Entry> entries = [];
        using var document = JsonDocument.Parse(body);

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.GetProperty("type").GetString() is not "file")
            {
                continue;
            }

            var repositoryPath = item.GetProperty("path").GetString() ?? string.Empty;
            var sha256 = item.TryGetProperty("lfs", out var lfs) && lfs.TryGetProperty("oid", out var oid)
                ? oid.GetString()
                : null;
            entries.Add(new Entry
            {
                Path = repositoryPath,
                RelativePath = repositoryPath.StartsWith(prefix, StringComparison.Ordinal)
                    ? repositoryPath[prefix.Length..]
                    : repositoryPath,
                Size = item.GetProperty("size").GetInt64(),
                Sha256 = sha256,
            });
        }

        return entries;
    }

    private async Task DownloadAsync(
        Entry entry,
        string destination,
        IProgress<SystemOne.ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        // 取檔：GET /{repository}/resolve/{revision}/{path}，串流寫入 .part
        var uri = new Uri($"https://huggingface.co/{_repository}/resolve/{_revision}/{entry.Path}");
        var partial = destination + ".part";
        var responseTask = GetAsync(
            uri,
            completion: HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        using var response = await responseTask.ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 檔案在列表裡卻抓不到：多半是權限或修訂變動
            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            throw new SystemOneException(
                message: $"Cannot download {entry.Path}: {ErrorText(body)}",
                statusCode: response.StatusCode,
                responseBody: body);
        }

        // 邊寫 .part 邊算 SHA256（規則 076 的 stated 例外：零相依函式庫用 BCL 的 SHA-256 做完整性驗證）
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (source.ConfigureAwait(false))
        {
            var file = File.Create(partial);

            await using (file.ConfigureAwait(false))
            {
                var buffer = new byte[81920];
                var received = default(long);

                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                    if (read is 0)
                    {
                        break;
                    }

                    hasher.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress?.Report(new SystemOne.ModelDownloadProgress
                    {
                        File = entry.Path,
                        BytesReceived = received,
                        TotalBytes = entry.Size,
                    });
                }
            }
        }

        if ((entry.Sha256 is { } expected) &&
            (Convert.ToHexStringLower(hasher.GetHashAndReset()) is { } actual) &&
            !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            // 下載內容的 sha256 與 API 提供的不符：刪掉半成品
            File.Delete(partial);

            throw new SystemOneException($"sha256 mismatch for {entry.Path}: expected {expected}, got {actual}.");
        }

        if ((entry.Sha256 is null) &&
            (new FileInfo(partial) is { Length: var length }) &&
            (length != entry.Size))
        {
            // 小檔沒有 sha256 可比，至少比大小，而大小不符：刪掉半成品
            File.Delete(partial);

            throw new SystemOneException(
                FormattableString.Invariant(
                    $"Size mismatch for {entry.Path}: expected {entry.Size} bytes, got {length}."));
        }

        // 驗證相符才改名，所以目錄裡永遠沒有半成品
        File.Move(partial, destination, overwrite: true);
    }

    private sealed record Entry
    {
        public required string Path { get; set; } = default!;

        public required string RelativePath { get; set; } = default!;

        public long Size { get; set; }

        public string? Sha256 { get; set; }
    }

    // 資訊清單的一列：相對於目標目錄的路徑與檔案大小
    private sealed record ManifestEntry
    {
        [JsonPropertyName("path")]
        public required string Path { get; set; } = default!;

        [JsonPropertyName("size")]
        public required long Size { get; set; } = default!;
    }

    // Hugging Face 的錯誤本文是 {"error":"…"}；不是的話就原樣回傳
    private static string ErrorText(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if ((document.RootElement is { ValueKind: JsonValueKind.Object } root) &&
                root.TryGetProperty("error", out var error) &&
                (error.ValueKind is JsonValueKind.String))
            {
                return error.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // 不是 JSON，原樣回傳
        }

        return body;
    }
}