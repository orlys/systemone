# SystemOne：給提供者與擴充作者 agent 的指南

[English](extending-systemone.md) · 給人看的指南：[writing-providers.zh-TW.md](../writing-providers.zh-TW.md)

目的：讓撰寫程式的 agent 一次就正確地替 SystemOne 加入提供者、請求範本、工廠輔助方法或權重來源。
以下每一句都是目前版本的事實或規則。使用端的寫法在 [using-systemone.zh-TW.md](using-systemone.zh-TW.md)，
還沒讀過的話請先讀。

這份文件隨每個 SystemOne NuGet 套件一起發佈，位於套件的 `docs/agents/`。

## 決定要寫什麼

| 情況 | 撰寫 | 參考 |
|---|---|---|
| 服務說標準 `/v1/systemone`、Bearer 金鑰、`images` data URL | 一個 `Use*` 擴充方法，回傳 `new HttpSystemOneProvider(http, endpoint)` | `SystemOne` |
| 服務的請求形狀、標頭或回應信封有差異 | `sealed class XSystemOneProvider : HttpSystemOneProvider`，覆寫 `PrepareRequest` 和／或 `ReadResponse`，再加一個 `Use*` 方法 | `SystemOne` |
| 行程內模型、非 HTTP 傳輸，或其他任何東西 | `sealed class XSystemOneProvider : SystemOneProvider`，實作 `DecideCoreAsync`，再加一個 `Use*` 方法 | `SystemOne.Abstractions` |
| 可重複使用的一組題目 | `SystemOneRequests` 上的 `static` 擴充方法 | `SystemOne.Abstractions` |
| 建立答案的輔助方法 | `SystemOneFactory` 的擴充方法 | `SystemOne.Abstractions` |
| 新的權重載入來源 | `sealed class X : SystemOne.ModelSource`，再加一個 `SystemOneModelSources` 上的 `From*` 擴充方法 | `SystemOne.Abstractions` |

絕不要直接實作 `ISystemOne`；請繼承 `SystemOneProvider`，共用驗證與後援鏈的行為。

## 可以建構在其上的型別

所有型別都在全域命名空間。

| 型別 | 種類 | 重要成員 |
|---|---|---|
| `ISystemOne` | 介面 | `Task<SystemOne.Response> DecideAsync(SystemOne.Request request, CancellationToken cancellationToken = default)` |
| `SystemOneProvider` | 抽象類別 | 公開、非 virtual 的 `DecideAsync`（先驗證，再呼叫）`protected abstract Task<SystemOne.Response> DecideCoreAsync(SystemOne.Request request, CancellationToken cancellationToken)`；`static HttpClient DefaultHttpClient`（所有提供者與權重來源共用的預設用戶端） |
| `HttpSystemOneProvider` | 公開、非 sealed | `public HttpSystemOneProvider(HttpClient http, SystemOneEndpoint endpoint)`；`SystemOneEndpoint Endpoint`；`HttpClient HttpClient`；`protected virtual void PrepareRequest(SystemOne.Request request, JsonObject body, HttpRequestMessage message)`；`protected virtual SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)` |
| `SystemOneEndpoint` | sealed record | `required Uri Uri`（含路徑的完整網址）；`required LocalityKind Locality`（`SelfHosted`／`Hosted`）；`string? DefaultModel`；`string? BearerToken`（`ToString()` 會遮蔽） |
| `SystemOneException` | sealed 例外 | `SystemOneException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, Exception? innerException = null)`；`StatusCode`；`ResponseBody` |
| `SystemOne.Request` | sealed record | `string? Model`；`JsonNode State`；`IReadOnlyDictionary<string, SystemOne.Question> Questions`；`IReadOnlyDictionary<string, SystemOne.Image>? Images`（不序列化；傳輸形狀由提供者決定） |
| `SystemOne.Image` | sealed record | `ReadOnlyMemory<byte> Bytes`；`string Format`（`png`、`jpg`、`jpeg`、`webp`） |
| `SystemOne.NoulQuestion` | 題目 | `Instructions` |
| `SystemOne.ChoiceQuestion` | 題目 | `Instructions`；`IReadOnlyDictionary<string, string?> Criteria`（選項 → 說明，依加入順序） |
| `SystemOne.ScoreQuestion` | 題目 | `Instructions`；`IReadOnlyList<string> Criteria`（刻度，由低到高） |
| `SystemOne.BoxQuestion` | 題目 | `Instructions` |
| `SystemOne.NoulAnswer` | 答案 | `double Probability`（JSON `noul`） |
| `SystemOne.ChoiceAnswer` | 答案 | `string Option`（JSON `choice`）；`double Confidence`；`IReadOnlyDictionary<string, double> Probabilities` |
| `SystemOne.ScoreAnswer` | 答案 | `double Level`（JSON `score`）；`double Confidence`；`IReadOnlyDictionary<string, string> Legend`；`IReadOnlyDictionary<string, double> Probabilities` |
| `SystemOne.BoxAnswer` | 答案 | `BoxCoordinates Bounds`（JSON `box`：`Top`、`Bottom`、`Left`、`Right`）；`double? Confidence` |
| `SystemOne.Response` | sealed record | `required string Model`；`required IReadOnlyDictionary<string, Answer> Answers`（null → 空）；`Usage Usage`（null → 0） |
| `SystemOne.Usage` | sealed record | `long InputTokens`（JSON `input_tokens`）；`long OutputTokens` |
| `SystemOne.ModelSource` | 抽象類別 | `public abstract Task<string> ResolveAsync(IProgress<SystemOne.ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)` |
| `SystemOne.ModelDownloadProgress` | sealed record | `required string File`；`long BytesReceived`；`long TotalBytes`（0 = 未知） |
| `SystemOneProviders`、`SystemOneRequests`、`SystemOneModelSources`、`SystemOneFactory` | sealed 類別，建構函式為 internal | 四個掛載點的型別；以 `this` 擴充方法擴充 |

`Question` 與 `Answer` 的建構函式是 `private protected`：四種題目與答案型別是封閉的集合，無法新增題型。

## 提供者契約

`SystemOneProvider.DecideAsync` 呼叫 `DecideCoreAsync` 前保證：請求不是 null；`State` 不是 null；至少一題；
每個單選題 1–255 個選項；每個刻度題 1–10 級；每張影像有位元組與格式；權杖還沒被取消。

`DecideCoreAsync` 必須：

1. 對 `request.Questions` 的每一項回傳一個答案，鍵相同、型別對應
   （`Noul→NoulAnswer`、`Choice→ChoiceAnswer`、`Score→ScoreAnswer`、`Box→BoxAnswer`）。
2. 單選題的 `Option` 必須是 `ChoiceQuestion.Criteria` 的某個鍵，`Probabilities` 也以同樣的選項名稱為鍵。
3. 使用 `request.Model ?? defaultModel`；`Response.Model` 填實際回答的模型。
4. 服務、傳輸、逾時或解析的失敗一律丟 `SystemOneException`。這是唯一會讓 `UseFallback` 改試下一個提供者的例外。
5. 不支援的題型或請求本身的其他問題，丟 `ArgumentException`。
6. 呼叫端權杖造成的 `OperationCanceledException` 原樣傳出，不要包裝。
7. 同一個執行個體要能安全地同時呼叫；絕不修改請求。

## HttpSystemOneProvider 的處理流程

`HttpSystemOneProvider` 的 `DecideCoreAsync` 依序：

1. `model = request.Model ?? Endpoint.DefaultModel`；兩者都沒有 → `ArgumentException`。
2. 建立 `POST Endpoint.Uri`；有 `BearerToken` 時加上 `Authorization: Bearer <token>`。
3. 把 `request with { Model = model }` 序列化成 `JsonObject`：`model`、`state`、`questions`（不含影像）。
4. 呼叫 `PrepareRequest(request, body, message)`。預設：`request.Images` 不為空時，把
   `body["images"]` 設為 `data:image/<format>;base64,…` 陣列（`jpg` → `jpeg`）。
5. 送出。`HttpRequestException` → `SystemOneException`（內部例外為原例外）。`HttpClient.Timeout`
   （呼叫端權杖沒被取消時的取消）→ `SystemOneException`。呼叫端取消 → `OperationCanceledException`。
6. 非 2xx → `SystemOneException(message, statusCode, body)`。訊息依序取自 `{"error":"…"}`、
   `{"error":{"message"}}`、`{"message"}`、`{"detail":{"message"}}`、`{"detail":[{"msg"}]}`、
   `{"errors":[{"message"}]}`，都不符合就是原始本文。
7. 2xx → `ReadResponse(body, statusCode)`。預設反序列化標準回應；本文為空、`model` 或某個答案為 null
   → `SystemOneException`。`ReadResponse` 丟出的其他任何例外都會被包成 `SystemOneException`。

勾點規則：

- 覆寫 `PrepareRequest` 會取代預設行為；要保留 `images` 就呼叫 `base.PrepareRequest(...)`。
- 在 `ReadResponse` 裡拆掉信封，把內層的標準 JSON 交給 `base.ReadResponse(inner, statusCode)`。
  函式庫由來源產生器產生的 JSON 內容是 internal。
- 建構函式：`internal`，簽章 `(HttpClient http, …)`，呼叫 `base(http, CreateEndpoint(…))`。

## 骨架

標準 `/v1/systemone` 服務：

```csharp
public static class ContosoProviders
{
    public const string DefaultModel = "contoso-decide";

    public static ISystemOne UseContoso(
        this SystemOneProviders anchor,
        string apiKey,
        string model = DefaultModel,
        HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new HttpSystemOneProvider(
            http: http ?? SystemOneProvider.DefaultHttpClient,
            endpoint: new SystemOneEndpoint
            {
                Uri = new Uri("https://decide.contoso.example/v1/systemone"),
                Locality = SystemOneEndpoint.LocalityKind.Hosted,
                DefaultModel = model,
                BearerToken = apiKey,
            });
    }
}
```

有信封的 HTTP 服務：

```csharp
public sealed class FabrikamSystemOneProvider : HttpSystemOneProvider
{
    internal FabrikamSystemOneProvider(HttpClient http, string apiKey)
        : base(
            http,
            endpoint: new SystemOneEndpoint
            {
                Uri = new Uri("https://api.fabrikam.example/decide"),
                Locality = SystemOneEndpoint.LocalityKind.Hosted,
                DefaultModel = "fabrikam-1",
                BearerToken = apiKey,
            })
    {
    }

    protected override SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("result", out var result))
        {
            // 2xx 卻沒有信封是伺服器的錯
            throw new SystemOneException("Response has no result.", statusCode, body);
        }

        return base.ReadResponse(result.GetRawText(), statusCode);
    }
}

public static class FabrikamProviders
{
    public static ISystemOne UseFabrikam(this SystemOneProviders anchor, string apiKey, HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new FabrikamSystemOneProvider(http ?? SystemOneProvider.DefaultHttpClient, apiKey);
    }
}
```

其他提供者：

```csharp
public sealed class EchoSystemOneProvider : SystemOneProvider
{
    protected override Task<SystemOne.Response> DecideCoreAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken)
    {
        List<(string Name, SystemOne.Answer Answer)> answers = [];

        foreach (var (name, question) in request.Questions)
        {
            if (question is not SystemOne.NoulQuestion)
            {
                // 這個提供者只回答是非題
                throw new ArgumentException($"Question '{name}' is not supported.", nameof(request));
            }

            answers.Add((name, SystemOne.Factory.Noul(0.5)));
        }

        return Task.FromResult(SystemOne.Factory.Response(request.Model ?? "echo", [.. answers]));
    }
}

public static class EchoProviders
{
    public static ISystemOne UseEcho(this SystemOneProviders anchor)
    {
        return new EchoSystemOneProvider();
    }
}
```

行程內模型還要：

- 接收 `SystemOne.ModelSource`，`await source.ResolveAsync(...)` 後從該目錄載入；
- 載入很慢，所以 `Use*Async` 方法回傳 `Task<TProvider>`；
- 持有原生資源時實作 `IDisposable`，並回傳具體型別讓呼叫端能釋放；
- 以 `Task.Run` 執行 CPU 密集的推論，並在步驟之間檢查權杖。

## 建立答案

| 需要 | 使用 | 丟出 |
|---|---|---|
| 是非題 | `SystemOne.Factory.Noul(probability)` | 不在 0–1 或為 NaN 時丟 `ArgumentOutOfRangeException` |
| 單選題 | `SystemOne.Factory.Choice((option, probability), …)`；取最大者（同分取先出現的）為 `Option` 與 `Confidence`；保留傳入順序 | `ArgumentException`（空、重複）、`ArgumentOutOfRangeException` |
| 框選題 | `SystemOne.Factory.Box(top, bottom, left, right, confidence = null)` | 不在 0–1 時丟 `ArgumentOutOfRangeException`；上緣 > 下緣或左緣 > 右緣時丟 `ArgumentException` |
| 回應 | `SystemOne.Factory.Response(model, (name, answer), …)`；用量為 0 | 名稱重複時丟 `ArgumentException` |
| 影像 | `SystemOne.Factory.Image(bytes, format)`、`SystemOne.Factory.Image(path)` | `ArgumentException`、`IOException` |
| 刻度題 | 沒有工廠：`new SystemOne.ScoreAnswer { … }` | — |
| 用量 | `response with { Usage = new SystemOne.Usage { InputTokens = n } }` | — |

`ScoreAnswer` 的規則：`Level = Σ i·p(i)`（從 0 起算）；`Confidence = max p(i)`；`Legend` 與
`Probabilities` 以 `i.ToString(CultureInfo.InvariantCulture)` 為鍵；`Legend[i]` 等於題目的 `Criteria[i]`。

## 請求範本

```csharp
public static class TriageRequests
{
    private static readonly SystemOne.Request s_triage = SystemOne.Requests
        .Choice("queue", "Which queue?", "billing", "support", "sales")
        .Score("priority", "Priority", "low", "normal", "high");

    public static SystemOne.Request Triage(this SystemOneRequests anchor, JsonNode ticket)
    {
        return s_triage.For(ticket);
    }
}
```

題目只在 `static readonly` 欄位裡建立一次，不呼叫 `.For(...)`。每次呼叫回傳 `template.For(state)` 這個新請求，
共用的範本不會被改變。狀態為 null 時 `.For` 會丟 `ArgumentNullException`。

## 權重來源

- 繼承 `SystemOne.ModelSource`；建構函式 `internal`；公開
  `public static SystemOne.ModelSource FromX(this SystemOneModelSources anchor, …)`。
- `ResolveAsync`：只在它裡面連網；回傳一個檔案完整且已驗證的目錄；先下載到暫存名稱，驗證後再移到定位；
  檔案已存在時不連網直接回傳；遠端失敗 → `SystemOneException`；磁碟失敗 → `IOException`；取消原樣傳出；
  `progress` 不是 null 時回報進度。

## 命名

| 項目 | 模式 |
|---|---|
| 提供者套件 | `SystemOne.Providers.<Name>` |
| 權重來源套件 | `SystemOne.ModelSources.<Name>` |
| 提供者 | `<Name>SystemOneProvider`，`sealed`，`internal` 建構函式 |
| 進入點類別 | `public static class <Name>Providers` |
| 進入點方法 | `Use<Name>(this SystemOneProviders anchor, …, HttpClient? http = null)` |
| 權重來源 | `From<Name>(this SystemOneModelSources anchor, …)` |
| 掛載點參數 | 一律 `anchor` |
| 模型常數 | 進入點類別上的 `public const string <Model>Model` |
| 新增／取代／讀取 | 名詞代表新增（`.Noul`、`.Image`）；`With*` 代表取代（`.WithModel`）；`Get*` 代表讀取 |
| 已有具名對應方法的泛型輔助方法 | `[EditorBrowsable(EditorBrowsableState.Never)]` |

出處：[ADR-0001](../adr/0001-object-creation.md)。

## 測試

- HTTP 提供者：用替身 `HttpMessageHandler` 記錄請求並回傳固定的 JSON；以 `Use*(…, http: new HttpClient(stub))`
  建立提供者。
- 斷言：勾點產生的本文與標頭；每種題型的解析；每種錯誤形狀（`StatusCode`、`ResponseBody`）；
  不支援的題型 → `ArgumentException`；取消 → `OperationCanceledException`。
- 單元測試絕不呼叫真正的端點。實際連線的測試要用環境變數控制。
- 完整範例：[writing-providers.zh-TW.md#測試提供者](../writing-providers.zh-TW.md#測試提供者)。

## 在這個存放庫加入套件

要動到的檔案：

1. `src/SystemOne.Providers.<Name>/SystemOne.Providers.<Name>.csproj`（權重來源則用 `SystemOne.ModelSources.<Name>`）：
   複製 `src/SystemOne.Providers.OpenRouter/SystemOne.Providers.OpenRouter.csproj`；
   設定 `PackageId`、`Description`、`PackageTags`；HTTP 類參考 `../SystemOne/SystemOne.csproj`，
   其他參考 `../SystemOne.Abstractions/SystemOne.Abstractions.csproj`。要測試 internal 成員時，
   加上 `<InternalsVisibleTo Include="SystemOne.Tests" />`。
2. `src/SystemOne.Providers.<Name>/<Name>Providers.cs`，需要時再加 `<Name>SystemOneProvider.cs`。
3. `SystemOne.slnx`：加入專案。
4. `tests/SystemOne.Tests/SystemOne.Tests.csproj`：加入專案參考；新增 `<Name>SystemOneProviderTests.cs`。
5. `README.md`、`README.zh-TW.md`、`docs/usage.md`、`docs/usage.zh-TW.md`、`docs/agents/using-systemone.md`
   與 `docs/agents/using-systemone.zh-TW.md` 裡的套件表。

CI 會以同一個版號封裝 `src/` 底下的每個資料夾，不需要改工作流程。

這個存放庫的程式碼規範：<https://github.com/orlys/rules>。最常踩到的幾條：XML 文件註解用繁體中文；
每個區塊都要大括號；不用運算式主體成員；註解獨立一行放在程式碼上方；呼叫換行時用具名引數；
每行 ≤ 120 字元（建議 96）；類別加 `sealed`。

建置與測試：

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release
dotnet format --verify-no-changes
bash tools/check-docs.sh
```

## 常見陷阱

- `SystemOne` 是類別：不要寫 `using SystemOne;`。
- 漏了 `this SystemOneProviders anchor`，方法就不會出現在 `SystemOne.Providers` 上。
- 公開的提供者建構函式會破壞「只有一種建立方式」的規則；請維持 `internal`。
- 服務失敗時丟出 `SystemOneException` 以外的例外，會讓後援鏈停住。
- 把呼叫端的 `OperationCanceledException` 包成 `SystemOneException`，後援鏈會在取消之後繼續呼叫其他提供者。
- 單選題答案的 `Option` 不是題目的選項之一，會讓使用端出錯。
- 覆寫 `PrepareRequest` 卻沒呼叫 `base.PrepareRequest`，影像就會被丟掉。
- 沒檢查 API 金鑰：環境變數沒設時，狀態會以未經驗證的請求送給第三方，而 401 會讓後援鏈不聲不響地換下一個。
- 在 `HttpClient.DefaultRequestHeaders` 設定標頭：用戶端是共用的；請在 `PrepareRequest` 裡設在 `message` 上。