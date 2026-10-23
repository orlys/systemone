# 撰寫提供者與擴充

[English](writing-providers.md)

這份指南寫給擴充 SystemOne 的人：新的託管服務、有些差異的自架伺服器、在行程內執行的模型，
或請求範本、權重來源這類輔助型別。只是要使用函式庫的話，請先讀 [usage.zh-TW.md](usage.zh-TW.md)。

- [選擇擴充點](#選擇擴充點)
- [提供者契約](#提供者契約)
- [標準的 /v1/systemone 服務](#標準的-v1systemone-服務)
- [有差異的 HTTP 服務](#有差異的-http-服務)
- [其他提供者](#其他提供者)
- [建立答案](#建立答案)
- [請求範本](#請求範本)
- [工廠擴充](#工廠擴充)
- [權重來源](#權重來源)
- [測試提供者](#測試提供者)
- [命名與封裝](#命名與封裝)
- [檢查清單](#檢查清單)

## 選擇擴充點

| 要加入 | 撰寫 | 相依於 |
|---|---|---|
| 說標準 `/v1/systemone`、用 Bearer 金鑰的服務 | 一個 `Use*` 方法，不用新類別 | `SystemOne` |
| 影像、標頭或回應信封有差異的服務 | `HttpSystemOneProvider` 的子類別加一個 `Use*` 方法 | `SystemOne` |
| 行程內模型、非 HTTP 傳輸，或其他任何東西 | `SystemOneProvider` 的子類別加一個 `Use*` 方法 | `SystemOne.Abstractions` |
| 可重複使用的一組題目 | `SystemOneRequests` 的擴充方法 | `SystemOne.Abstractions` |
| 建立答案的輔助方法 | `SystemOneFactory` 的擴充方法 | `SystemOne.Abstractions` |
| 新的權重載入來源 | `SystemOne.ModelSource` 的子類別加一個 `From*` 方法 | `SystemOne.Abstractions` |

每種擴充都是四個掛載點之一的擴充方法，所以使用者輸入 `SystemOne.Providers.`、`SystemOne.Requests.`、
`SystemOne.Factory.` 或 `SystemOne.ModelSources.` 就找得到。

## 提供者契約

使用者只看得到 `ISystemOne`：

```csharp
public interface ISystemOne
{
    Task<SystemOne.Response> DecideAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken = default);
}
```

不要直接實作它，請繼承 `SystemOneProvider`。它實作了 `DecideAsync`，並在下列檢查都通過後才呼叫你的
`DecideCoreAsync`：

- 請求不是 null；
- 已有狀態（呼叫過 `.For(...)`），所以 `request.State` 一定不是 null；
- 至少有一題；
- 每個單選題有 1–255 個選項，每個刻度題有 1–10 級；
- 每張影像都有位元組和格式；
- 取消權杖還沒被取消。

你的 `DecideCoreAsync` 必須：

1. **回答 `request.Questions` 裡的每一題**，用同樣的名稱，答案型別要對應：`NoulQuestion` → `NoulAnswer`、
   `ChoiceQuestion` → `ChoiceAnswer`（選項必須來自它的 `Criteria`）、`ScoreQuestion` → `ScoreAnswer`、
   `BoxQuestion` → `BoxAnswer`。
2. **有 `request.Model` 時就用它**，否則用你的預設模型。把實際回答的模型放進 `Response.Model`。
3. **以 `SystemOneException` 回報失敗**；來自 HTTP 時一併帶上 `StatusCode` 與 `ResponseBody`。
   後援鏈只有遇到這個例外才會換下一個提供者。傳輸錯誤和你自己的逾時也要包成它。
4. **讓呼叫端的取消原樣傳出。** `cancellationToken` 被取消時，丟出（或放行）`OperationCanceledException`，不要包裝。
5. **只在請求本身有問題時丟 `ArgumentException`**，例如不支援的題型。後援鏈遇到它會直接停止，不會試下一個提供者。
6. **能安全地同時呼叫。** 使用者通常整個應用程式只保留一個提供者，並從多個執行緒呼叫它。
7. **不修改請求。** 請求會被共用，一律視為不可變。

## 標準的 /v1/systemone 服務

服務如果接受標準的請求本文、`images` data URL 陣列和 Bearer 金鑰，就不需要新類別。描述端點，
回傳一個 `HttpSystemOneProvider` 即可：

```csharp
public static class ExampleDecisionsProviders
{
    public const string DefaultModel = "example-1";

    public static ISystemOne UseExampleDecisions(
        this SystemOneProviders anchor,
        string apiKey,
        string model = DefaultModel,
        HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var endpoint = new SystemOneEndpoint
        {
            Uri = new Uri("https://api.example.com/v1/systemone"),
            Locality = SystemOneEndpoint.LocalityKind.Hosted,
            DefaultModel = model,
            BearerToken = apiKey,
        };

        return new HttpSystemOneProvider(http ?? SystemOneProvider.DefaultHttpClient, endpoint);
    }
}
```

`SystemOneEndpoint` 的欄位：

| 欄位 | 意義 |
|---|---|
| `Uri` | 完整的請求網址，包含路徑 |
| `Locality` | 資料留在使用者自己的網路就是 `SelfHosted`，送到第三方就是 `Hosted`。函式庫只記錄，不據此路由 |
| `DefaultModel` | 請求沒有指定模型時使用。兩者都沒有時，`DecideAsync` 丟出 `ArgumentException` |
| `BearerToken` | 設定時每個請求都帶 `Authorization: Bearer …`。`ToString()` 會遮蔽它 |

其餘的事 `HttpSystemOneProvider` 都做好了：

- 決定模型、加上 Bearer 標頭，並序列化 `model`、`state` 與 `questions`；
- 把影像以 `data:image/…;base64,…` 網址組成 `images` 陣列；
- 把非 2xx 回應轉成 `SystemOneException`，並從 clef-mlx、Ollama、llama-server、OpenRouter、Vercel、TypeSafe
  和 Cloudflare 的錯誤格式中取出訊息；
- 把傳輸失敗與 `HttpClient.Timeout` 包成 `SystemOneException`，呼叫端的取消則原樣傳出；
- 解析回應，格式錯誤的 JSON 包成 `SystemOneException`；模型名稱或答案為 null 時同樣視為失敗；
  缺少 `usage` 時視為 0。

## 有差異的 HTTP 服務

服務與標準不同時，繼承 `HttpSystemOneProvider`，覆寫其中一個或兩個勾點：

| 勾點 | 呼叫時機 | 預設行為 |
|---|---|---|
| `PrepareRequest(request, body, message)` | 送出前；本文已有 `model`、`state`、`questions`，訊息已帶 Bearer 標頭 | 以 data URL 加上 `images` |
| `ReadResponse(body, statusCode)` | 收到 2xx 回應時 | 解析標準的回應本文 |

範例：一個要求影像放在 `attachments`、需要版本標頭、而且把回應包在 `{"data": …}` 裡的服務：

```csharp
public sealed class AcmeSystemOneProvider : HttpSystemOneProvider
{
    internal AcmeSystemOneProvider(HttpClient http, string apiKey, string model)
        : base(http, CreateEndpoint(apiKey, model))
    {
    }

    protected override void PrepareRequest(
        SystemOne.Request request,
        JsonObject body,
        HttpRequestMessage message)
    {
        // 預設會加上 "images"；這個服務要的是放在 "attachments" 的 base64 字串
        if (request.Images is { Count: > 0 } images)
        {
            var attachments = new JsonArray();

            foreach (var image in images.Values)
            {
                attachments.Add(Convert.ToBase64String(image.Bytes.Span));
            }

            body["attachments"] = attachments;
        }

        message.Headers.Add("Acme-Version", "2");
    }

    protected override SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            // 2xx 卻沒有信封是伺服器的錯，不能當成空答案
            throw new SystemOneException("Response has no data field.", statusCode, body);
        }

        // 內層物件是標準回應，交給基底類別解析
        return base.ReadResponse(data.GetRawText(), statusCode);
    }

    private static SystemOneEndpoint CreateEndpoint(string apiKey, string model)
    {
        return new SystemOneEndpoint
        {
            Uri = new Uri("https://api.acme.example/v2/decide"),
            Locality = SystemOneEndpoint.LocalityKind.Hosted,
            DefaultModel = model,
            BearerToken = apiKey,
        };
    }
}

public static class AcmeProviders
{
    public static ISystemOne UseAcme(
        this SystemOneProviders anchor,
        string apiKey,
        string model = "acme-decide",
        HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new AcmeSystemOneProvider(
            http: http ?? SystemOneProvider.DefaultHttpClient,
            apiKey,
            model);
    }
}
```

- 覆寫 `PrepareRequest` 會取代預設行為。仍然需要 `images` 陣列時，呼叫 `base.PrepareRequest(...)`。
- 在 `ReadResponse` 裡丟出的例外，除了 `SystemOneException` 以外都會自動包成 `SystemOneException`，
  後援鏈因此能換下一個。想自己決定訊息時，直接丟 `SystemOneException`。
- `SystemOne.Providers.TypeSafe`（影像以 `assets` 加標頭送出）和
  `SystemOne.Providers.Cloudflare`（`{"success","result","errors"}` 信封）就是這兩個勾點的實際範例。

## 其他提供者

行程內模型、gRPC 服務或任何不是 HTTP 的東西，請繼承 `SystemOneProvider` 並實作 `DecideCoreAsync`。
下面這個玩具提供者用關鍵字回答，但真正的提供者需要的每個部分它都有：

```csharp
public sealed class KeywordSystemOneProvider : SystemOneProvider
{
    private readonly string _keyword;

    internal KeywordSystemOneProvider(string keyword)
    {
        _keyword = keyword;
    }

    protected override Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken)
    {
        // 代替模型：狀態裡有沒有提到關鍵字
        var state = request.State.ToJsonString();
        var hit = state.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

        // 每一題都以同樣的名稱、對應的答案型別回答
        List<(string Name, SystemOne.Answer Answer)> answers = [];

        foreach (var (name, question) in request.Questions)
        {
            SystemOne.Answer answer = question switch
            {
                SystemOne.NoulQuestion => SystemOne.Factory.Noul(hit ? 0.9 : 0.1),
                SystemOne.ChoiceQuestion choice => PickFirst(choice),
                SystemOne.ScoreQuestion { Criteria: var levels } =>
                    SystemOne.Factory.ScoreFromLevels(
                        levels,
                        probabilities: [.. Enumerable.Repeat(1.0 / levels.Count, levels.Count)]),
                _ => throw new ArgumentException(
                    message: $"Question '{name}' has a type this provider does not support.",
                    paramName: nameof(request)),
            };

            answers.Add((name, answer));
        }

        // 請求自己指定的模型優先於這個提供者的預設模型
        var response = SystemOne.Factory.Response(request.Model ?? "keyword", [.. answers]);

        return Task.FromResult(response);
    }

    private static SystemOne.ChoiceAnswer PickFirst(SystemOne.ChoiceQuestion choice)
    {
        var picked = choice.Criteria.Keys.Select((option, i) => (option, i == 0 ? 1.0 : 0.0));

        return SystemOne.Factory.Choice([.. picked]);
    }
}

public static class KeywordProviders
{
    public static ISystemOne UseKeyword(this SystemOneProviders anchor, string keyword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        return new KeywordSystemOneProvider(keyword);
    }
}
```

在行程內執行的模型還要注意：

- **接收 `SystemOne.ModelSource`，而不是路徑**，讓使用者在 `FromDirectory`、`FromHuggingFace` 或自己的來源之間選擇。
- **非同步載入。** 載入權重很慢，所以 `Use*` 方法回傳 `Task<…>`，名稱以 `Async` 結尾（`UseLocalModelAsync`）。
- **持有資源時回傳具體型別。** `ISystemOne` 不是 `IDisposable`；提供者若持有原生工作階段，請實作 `IDisposable`
  並回傳自己的型別，讓呼叫端能釋放它。
- **在呼叫端的執行緒之外執行 CPU 密集的推論**（例如用 `Task.Run`），並在耗時的步驟之間檢查 `cancellationToken`。

```csharp
public sealed class LocalModelSystemOneProvider : SystemOneProvider, IDisposable
{
    private readonly string _directory;

    private LocalModelSystemOneProvider(string directory)
    {
        _directory = directory;
    }

    internal static async Task<LocalModelSystemOneProvider> LoadAsync(
        SystemOne.ModelSource source,
        CancellationToken cancellationToken)
    {
        var directory = await source
            .ResolveAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // 在這裡從 directory 載入模型檔案
        return new LocalModelSystemOneProvider(directory);
    }

    public void Dispose()
    {
        // 在這裡釋放原生工作階段
    }

    protected override Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            function: delegate
            {
                List<(string Name, SystemOne.Answer Answer)> answers = [];

                foreach (var (name, question) in request.Questions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    answers.Add((name, Infer(request.State, question)));
                }

                return SystemOne.Factory.Response(request.Model ?? "local", [.. answers]);
            },
            cancellationToken);
    }

    private SystemOne.Answer Infer(JsonNode state, SystemOne.Question question)
    {
        // 在這裡用 _directory 裡的模型，對狀態與這一題執行推論
        throw new NotImplementedException();
    }
}

public static class LocalModelProviders
{
    public static Task<LocalModelSystemOneProvider> UseLocalModelAsync(
        this SystemOneProviders anchor,
        SystemOne.ModelSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return LocalModelSystemOneProvider.LoadAsync(source, cancellationToken);
    }
}
```

## 建立答案

用 `SystemOne.Factory` 建立答案與回應，它會替你檢查範圍：

| 呼叫 | 建立 | 檢查 |
|---|---|---|
| `Factory.Noul(probability)` | `NoulAnswer` | 機率在 0–1 之間 |
| `Factory.Choice((option, probability), …)` | `ChoiceAnswer`，以機率最高者為 `Option` 與 `Confidence`（同分取先出現的） | 至少一個選項、名稱不重複、每個機率在 0–1 之間 |
| `Factory.Box(top, bottom, left, right, confidence)` | `BoxAnswer` | 四邊與信心都在 0–1 之間；上緣 ≤ 下緣、左緣 ≤ 右緣 |
| `Factory.Response(model, (name, answer), …)` | 用量為 0 的 `Response` | 名稱不重複 |
| `Factory.Image(bytes, format)`、`Factory.Image(path)` | `Image` | 格式不可空白；路徑要有副檔名 |

`ScoreAnswer` 目前還沒有工廠。請照它屬性文件寫的規則建立，或寫一個像下一節那樣的工廠擴充：

- `Level`：期望刻度索引 `Σ i × p(i)`，從 0 起算；
- `Confidence`：機率最高那一級的機率；
- `Legend` 與 `Probabilities`：以索引的不變文化特性字串 `"0"`、`"1"`… 為鍵。

`ChoiceAnswer` 的 `Probabilities` 應該列出題目的每個選項，並使用同樣的名稱，讓使用者能查得到。

`Response.Usage` 預設為 0。模型有回報權杖數時再設定：

```csharp
var response = SystemOne.Factory.Response("my-model", ("spam", SystemOne.Factory.Noul(0.8))) with
{
    Usage = new SystemOne.Usage { InputTokens = 120 },
};
```

## 請求範本

請求範本是許多呼叫端會對不同狀態詢問的一組題目。只建立一次題目，再以 `SystemOneRequests` 的擴充方法公開：

```csharp
public static class ModerationRequests
{
    // 題目只建立一次，是沒有狀態的範本；每次呼叫各自帶入狀態
    private static readonly SystemOne.Request s_moderation = SystemOne.Requests
        .Noul("spam", "Is this message spam?")
        .Choice("topic", "Main topic", "billing", "technical", "other")
        .Score("toxicity", "How toxic is the message?", "none", "mild", "severe");

    public static SystemOne.Request Moderation(this SystemOneRequests anchor, string message)
    {
        return s_moderation.For(message);
    }
}

// 用法：var request = SystemOne.Requests.Moderation("Buy cheap watches now!!!");
```

範本沒有狀態，每次 `.For(...)` 都回傳新的請求，共用的範本不會被改變。呼叫端仍可以在回傳的請求後面繼續串接更多題目。

## 工廠擴充

提供者常建立的答案，可以寫成 `SystemOneFactory` 的擴充方法。這個例子從每一級的機率建立 `ScoreAnswer`：

```csharp
public static class ScoreFactoryExtensions
{
    public static SystemOne.ScoreAnswer ScoreFromLevels(
        this SystemOneFactory factory,
        IReadOnlyList<string> levels,
        IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(probabilities);

        if ((levels.Count is 0) || (levels.Count != probabilities.Count))
        {
            // 沒有刻度，或兩份清單長度不同
            throw new ArgumentException("Need one probability per level.", nameof(probabilities));
        }

        // 期望刻度 Σ i × p(i)，以及機率最高的刻度
        var expected = default(double);
        var best = default(int);

        for (var i = 0; i < levels.Count; i++)
        {
            expected += i * probabilities[i];

            if (probabilities[i] > probabilities[best])
            {
                best = i;
            }
        }

        // 兩個字典都以刻度索引的不變文化字串為鍵
        return new SystemOne.ScoreAnswer
        {
            Level = expected,
            Confidence = probabilities[best],
            Legend = levels
                .Select((level, i) => (Key: i.ToString(CultureInfo.InvariantCulture), level))
                .ToDictionary(entry => entry.Key, entry => entry.level),
            Probabilities = probabilities
                .Select((p, i) => (Key: i.ToString(CultureInfo.InvariantCulture), p))
                .ToDictionary(entry => entry.Key, entry => entry.p),
        };
    }
}
```

避免使用函式庫之後可能加入的名稱（例如 `Score`）：`SystemOneFactory` 上的執行個體方法會不聲不響地蓋過你的擴充方法。

## 權重來源

要從新的地方（內部成品庫、S3…）載入權重，請繼承 `SystemOne.ModelSource`，並在 `SystemOneModelSources` 上加一個
`From*` 方法：

```csharp
public sealed class ArtifactStoreModelSource : SystemOne.ModelSource
{
    private readonly string _name;

    internal ArtifactStoreModelSource(string name)
    {
        _name = name;
    }

    public override async Task<string> ResolveAsync(
        IProgress<SystemOne.ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), "artifacts", _name);

        if (Directory.Exists(directory))
        {
            // 已經下載過，不連網
            return directory;
        }

        var download = DownloadAndVerifyAsync(directory, progress, cancellationToken);
        await download.ConfigureAwait(false);

        return directory;
    }

    private Task DownloadAndVerifyAsync(
        string directory,
        IProgress<SystemOne.ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        // 先下載到暫存目錄、逐檔驗證，再移到 directory
        throw new NotImplementedException();
    }
}

public static class ArtifactStoreModelSources
{
    public static SystemOne.ModelSource FromArtifactStore(this SystemOneModelSources anchor, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ArtifactStoreModelSource(name);
    }
}
```

`ResolveAsync` 必須：

- 只在 `ResolveAsync` 裡連網，建構函式和 `From*` 方法都不連網；
- 回傳一個存放完整、已驗證權重的目錄，絕不在下一次呼叫會信任的位置留下寫到一半的檔案；
- 權重已經存在時，不連網直接回傳；
- 遠端失敗以 `SystemOneException` 回報，磁碟失敗以 `IOException` 回報，取消則以 `OperationCanceledException` 原樣傳出；
- `progress` 不是 null 時回報進度。

## 測試提供者

用一個替身 `HttpMessageHandler` 測試 HTTP 提供者：它記下送出的請求並回傳固定的回應，所以測試完全不連網。

```csharp
public sealed class AcmeProviderTests
{
    [Fact]
    public async Task DecideAsync_EnvelopedAnswer_ReturnsInnerAnswer()
    {
        // Arrange
        var handler = new StubHandler("""
            {"data":{"model":"acme-decide","answers":{"spam":{"type":"noul","noul":0.9}}}}
            """);
        using var http = new HttpClient(handler);
        var systemOne = SystemOne.Providers.UseAcme("key", http: http);
        var request = SystemOne.Requests.Noul("spam", "Is it spam?").For("hello");

        // Act
        var response = await systemOne.DecideAsync(request);

        // Assert
        Assert.Equal(0.9, response.GetNoul("spam").Probability);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.True(handler.LastRequest.Headers.Contains("Acme-Version"));
    }
}

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly string _body;

    public StubHandler(string body)
    {
        _body = body;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_body),
        });
    }
}
```

至少要涵蓋：

- 你的勾點產生的請求本文與標頭；
- 成功的回應，包含你支援的每一種題型；
- 服務回傳的每一種錯誤形狀（`SystemOneException` 上的狀態碼與 `ResponseBody`）；
- 不支援的題型（`ArgumentException`）；
- 取消（`OperationCanceledException`，不是 `SystemOneException`）。

## 命名與封裝

沿用內建套件的命名，讓使用者猜得到：

| 項目 | 模式 | 範例 |
|---|---|---|
| 提供者套件 | `SystemOne.Providers.<Name>` | `SystemOne.Providers.Acme` |
| 權重來源套件 | `SystemOne.ModelSources.<Name>` | `SystemOne.ModelSources.ArtifactStore` |
| 提供者類別 | `<Name>SystemOneProvider`，`sealed`，建構函式 `internal` | `AcmeSystemOneProvider` |
| 進入點類別 | `<Name>Providers`，`static` | `AcmeProviders` |
| 進入點方法 | `Use<Name>(this SystemOneProviders anchor, …, HttpClient? http = null)` | `UseAcme` |
| 模型名稱常數 | 放在進入點類別上的 `public const string` | `ClefProviders.ClefFlashModel` |
| 請求範本 | `SystemOneRequests` 的擴充方法，以決策內容命名 | `Moderation` |
| 權重來源 | `From<Name>(this SystemOneModelSources anchor, …)` | `FromArtifactStore` |

其他慣例，出自 [ADR-0001](adr/0001-object-creation.md)：

- 使用者透過掛載點建立一切，所以提供者與來源的建構函式要維持 `internal`。只有 `HttpSystemOneProvider` 和
  `SystemOneEndpoint` 是公開的建構元件。
- 掛載點參數一律命名為 `anchor`。
- 新增項目的方法用名詞（`.Noul`、`.Image`）；取代設定的方法以 `With` 開頭（`.WithModel`）；讀取的方法以 `Get` 開頭。
- 泛型的底層方法若已有具名的對應方法，就用 `[EditorBrowsable(EditorBrowsableState.Never)]` 從 IntelliSense 隱藏。
- `HttpClient? http = null` 預設使用 `SystemOneProvider.DefaultHttpClient`。

## 檢查清單

- [ ] 提供者繼承 `SystemOneProvider`（或 `HttpSystemOneProvider`）。
- [ ] 每一題都以同樣的名稱得到對應型別的答案。
- [ ] `request.Model` 會覆寫預設模型；`Response.Model` 寫的是實際回答的模型。
- [ ] 失敗一律是 `SystemOneException`；呼叫端的取消維持 `OperationCanceledException`。
- [ ] 提供者可以安全地同時呼叫，而且不修改請求。
- [ ] 建構函式是 `internal`；使用者透過 `SystemOneProviders` 上的 `Use*` 方法建立。
- [ ] `SystemOneEndpoint.Locality` 如實標示資料是否離開網路。
- [ ] `Use*` 方法以 `ArgumentException.ThrowIfNullOrWhiteSpace` 拒收空白的 API 金鑰。
- [ ] 測試使用替身處理常式或假模型，從不連網。