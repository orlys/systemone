# SystemOne：給使用端 agent 的指南

[English](using-systemone.md) · 給人看的指南：[usage.zh-TW.md](../usage.zh-TW.md)

目的：讓撰寫程式的 agent 不讀原始碼，也能寫出正確**使用** SystemOne 的程式碼。以下每一句都是目前版本的事實，
或是要遵守的規則。若要撰寫提供者或輔助型別，請改讀 [extending-systemone.zh-TW.md](extending-systemone.zh-TW.md)。

這份文件隨每個 SystemOne NuGet 套件一起發佈，位於套件的 `docs/agents/`，所以
`~/.nuget/packages/<package-id>/<version>/docs/agents/` 底下的版本與安裝的版本一致。

## 先讀這些

1. SystemOne 呼叫的是**決策模型**：它們以機率回答關於狀態的具名題目，從不產生文字。
2. 四種題型：`noul`（是非題機率）、`choice`（從 1–255 個具名選項中選一個）、`score`（1–10 級的有序刻度）、
   `box`（附加影像中的區域；只有 TypeSafe 的 `jev-preview`）。
3. **沒有命名空間。** 所有型別都在全域命名空間，`SystemOne` 是靜態類別。絕不要寫 `using SystemOne;`
   （編譯錯誤 CS0138）。
4. 一切從四個掛載點建立：`SystemOne.Providers`、`SystemOne.Requests`、`SystemOne.ModelSources`、
   `SystemOne.Factory`。絕不要 `new` 請求、題目、答案、提供者或回應。
5. 請求鏈從 `SystemOne.Requests` 的第一題開始，以 `.For(state)` 結尾。沒有 `.For` 時是可重複使用的範本，
   `DecideAsync` 會拒絕它。請求與模擬提供者都**不可變**：每個串接方法都回傳新的執行個體；在迴圈裡要重新指派。
6. 用 `GetNoul`、`GetChoice`、`GetScore`、`GetBox` 讀取答案。不要用 `Get<T>`。
7. 答案屬性：`NoulAnswer.Probability`、`ChoiceAnswer.Option`、`ScoreAnswer.Level`、`BoxAnswer.Bounds`。
   答案上**沒有** `.Noul`、`.Choice`、`.Score` 或 `.Box` 屬性。
8. `ScoreAnswer.Level` 是從 0 起算、**可有小數的期望索引**，不是標籤。
9. 伺服器、傳輸、逾時與解析的失敗一律是 `SystemOneException`。不合法的請求是 `ArgumentException`。
   呼叫端的取消是 `OperationCanceledException`。
10. 託管提供者會把資料送給第三方。絕不要把個人資料送給 `Locality` 為 `Hosted` 的提供者。

## 最小程式

```csharp
var systemOne = SystemOne.Providers.UseClef(new Uri("http://localhost:8000"));

var request = SystemOne.Requests
    .Noul("refund", "Does the customer ask for a refund?")
    .Choice("department", "Which department handles this?", options => options
        .Option("billing", "invoices, payments, refunds")
        .Option("technical", "bugs, outages"))
    .Score("urgency", "How urgent is it?", "low", "medium", "high")
    .For(new { subject = "Duplicate charge", body = "Refund the second payment today." });

var response = await systemOne.DecideAsync(request, cancellationToken);

var refund = response.GetNoul("refund").Probability;
var department = response.GetChoice("department").Option;
var urgency = response.GetScore("urgency").Level;
```

## 套件

| 套件 | 內容 | 相依於 |
|---|---|---|
| `SystemOne.Abstractions` | `ISystemOne`、`SystemOneProvider`、`SystemOneException`、所有請求與答案型別、四個掛載點、`Factory`、`ModelSources.FromDirectory` | — |
| `SystemOne` | `HttpSystemOneProvider`、`SystemOneEndpoint`、`FallbackSystemOneProvider`、`UseSelfHosted`、`UseFallback` | Abstractions |
| `SystemOne.Providers.Clef` | `UseClef`、`ClefProviders.ClefFlashModel` | SystemOne |
| `SystemOne.Providers.TypeSafe` | `UseTypeSafeAi`、`TypeSafeSystemOneProvider` | SystemOne |
| `SystemOne.Providers.Cloudflare` | `UseCloudflareWorkersAi`、`CloudflareSystemOneProvider` | SystemOne.Providers.Clef |
| `SystemOne.Providers.OpenRouter` | `UseOpenRouter` | SystemOne |
| `SystemOne.Providers.Vercel` | `UseVercelAiGateway` | SystemOne |
| `SystemOne.Testing` | `UseMock`、`MockSystemOneProvider` | Abstractions |
| `SystemOne.ModelSources.HuggingFace` | `ModelSources.FromHuggingFace`、`HuggingFaceModelSource` | Abstractions |

`Use*` 方法要參考對應的套件後才存在。`UseClef` 解析不到時，請加入 `SystemOne.Providers.Clef`。

## 提供者

除了 `UseMock` 回傳 `MockSystemOneProvider`，其餘都回傳 `ISystemOne`。

| 呼叫 | 目標 | Locality | 模型 |
|---|---|---|---|
| `UseClef(Uri baseUri, HttpClient? http = null)` | 位於 `{baseUri}/v1/systemone` 的自架 clef 伺服器 | SelfHosted | `clef-flash` |
| `UseSelfHosted(Uri baseUri, string model, HttpClient? http = null)` | 任何自架的 `/v1/systemone`（Kev、laya-serve、d3-mlx、Ollama） | SelfHosted | `model` |
| `UseTypeSafeAi(string apiKey, string model = "jev-latest", HttpClient? http = null)` | `https://api.typesafe.ai/v1/systemone` | Hosted | `model` |
| `UseCloudflareWorkersAi(string accountId, string apiToken, string model = "clef-flash", HttpClient? http = null)` | Workers AI | Hosted | `model` |
| `UseOpenRouter(string apiKey, string model, HttpClient? http = null)` | `https://openrouter.ai/api/v1/systemone` | Hosted | 例如 `cloudflare/clef-flash` |
| `UseVercelAiGateway(string apiKey, string model, HttpClient? http = null)` | `https://ai-gateway.vercel.sh/typesafe/v1/systemone` | Hosted | 例如 `typesafe-ai/jev` |
| `UseFallback(params ISystemOne[] providers)` | 第一個沒有丟出 `SystemOneException` 的提供者 | 依各提供者 | 依各提供者 |
| `UseMock()`、`UseMock(Exception)`、`UseMock(Func<Request, Response>)` | 記憶體內 | — | `mock` |

- `UseClef` 與 `UseSelfHosted` 會把 `baseUri` 的路徑換成 `/v1/systemone`。
- `http` 預設為 `SystemOneProvider.DefaultHttpClient`（共用、逾時 60 秒、連線每 2 分鐘回收）。
- 搭配 `IHttpClientFactory` 時，每個服務註冊一個以服務命名、設好逾時的具名用戶端；把提供者註冊為**暫時性**
  （`AddTransient`）並傳入 `factory.CreateClient(name)`；不要在單一執行個體裡長期持有工廠建立的用戶端。
- `UseFallback`：只有 `SystemOneException` 才換下一個；其他例外直接停止；全部失敗時丟出一個列出所有失敗的
  `SystemOneException`（`InnerException` 是最後一個）。順序決定資料的去向：自架放前面；個人資料不要放託管提供者。

## 請求串接

起點：在 `SystemOne.Requests` 上呼叫下列 `.For` 以外的任一方法（例如 `SystemOne.Requests.Noul(...)`）。
結尾：`.For(object state)` 或 `.For(JsonNode state)`，會取代先前的狀態。
字串會變成 JSON 字串；物件以 `System.Text.Json` 的預設選項序列化（POCO 會保留 PascalCase 名稱）。
下列方法都是 `SystemOne.Request` 的擴充方法，並回傳**新的**請求。

| 方法 | 加入 | 備註 |
|---|---|---|
| `.Noul(string name, string instructions)` | 是非題 | |
| `.Choice(string name, string? instructions, Func<SystemOneChoiceOptions, SystemOneChoiceOptions> options)` | 附說明的單選題 | 寫法：`options => options.Option("a", "desc").Option("b")`；名稱不可空白、不可重複（序數比較） |
| `.Choice(string name, string? instructions, params string[] options)` | 只有名稱的單選題 | |
| `.Score(string name, string? instructions, params string[] levels)` | 有序刻度，由低到高 | 1–10 級 |
| `.Box(string name, string? instructions)` | 框選題 | 需要影像；只有 TypeSafe `jev-preview` |
| `.Image(string name, string path)` | 從檔案讀取的影像 | 格式取自副檔名並轉小寫 |
| `.Image(string name, ReadOnlyMemory<byte> bytes, string format)` | 從位元組建立的影像 | `png`、`jpg`、`jpeg`、`webp` |
| `.WithModel(string model)` | — | 覆寫提供者的預設模型 |
| `.For(object state)`、`.For(JsonNode state)` | 狀態 | 放在請求鏈最後；會複製狀態；null 會丟 `ArgumentNullException` |

- 名稱已存在的題目或影像會**取代**舊的。
- 選項與刻度名稱可以是任何語言（中文可用），回答時原樣回傳。
- 影像名稱是預留位置字串；TypeSafe 會把狀態或指示中出現該名稱的地方換成影像。
- `DecideAsync` 在送出前驗證，違反時丟 `ArgumentException`：有狀態；至少一題；單選題 1–255 個選項；
  刻度題 1–10 級；每張影像有位元組與格式；請求或提供者有指定模型。空白名稱會在串接方法當下就失敗。
- 函式庫**不檢查**的提供者限制：TypeSafe 影像至少 512 px（只收 png/jpg/jpeg）；Cloudflare 最多 4 張影像。

## 回應

| 成員 | 型別 | 意義 |
|---|---|---|
| `response.Model` | `string` | 實際回答的模型（用 `UseFallback` 時可看出是哪個提供者） |
| `response.Answers` | `IReadOnlyDictionary<string, SystemOne.Answer>` | 以題目名稱為鍵的所有答案；不會是 null |
| `response.Usage.InputTokens` / `.OutputTokens` | `long` | 伺服器沒回報時為 0；`Usage` 不會是 null |
| `response.GetNoul(name)` | `SystemOne.NoulAnswer` | `.Probability`（0–1） |
| `response.GetChoice(name)` | `SystemOne.ChoiceAnswer` | `.Option`（字串）、`.Confidence`（`.Option` 的機率）、`.Probabilities`（選項 → 機率） |
| `response.GetScore(name)` | `SystemOne.ScoreAnswer` | `.Level`（從 0 起算的期望索引，可有小數）、`.Confidence`（最高那一級的機率）、`.Legend`（`"0"`… → 刻度名稱）、`.Probabilities`（`"0"`… → 機率） |
| `response.GetBox(name)` | `SystemOne.BoxAnswer` | `.Bounds.Top/.Bottom/.Left/.Right`（佔高度／寬度的 0–1 比例）、`.Confidence`（`double?`） |

名稱不存在（訊息會列出有的名稱）或答案是別的題型時，`Get*` 丟出 `SystemOneException`。
`Confidence` 不是答案正確的機率。

刻度轉成標籤：

```csharp
var score = response.GetScore("urgency");
var label = score.Legend[((int)Math.Round(score.Level)).ToString(CultureInfo.InvariantCulture)];
```

## 例外

| 例外 | 由誰丟出 | 原因 |
|---|---|---|
| `ArgumentNullException` | 串接方法、`DecideAsync` | 引數為 null |
| `ArgumentException` | 串接方法、`DecideAsync`、`Use*`、`FromHuggingFace` | 不合法的請求（見驗證）；請求與端點都沒有模型；API 金鑰或帳號識別碼空白；存放庫不是 `name` 或 `owner/name`；修訂或子資料夾會離開快取目錄 |
| `SystemOneException` | `DecideAsync` | 非 2xx 回應；伺服器連不上；`HttpClient.Timeout`；無法解析的本文；Cloudflare `success:false`；後援鏈全部失敗 |
| `SystemOneException` | `Get*` | 答案名稱不存在，或答案型別不符 |
| `OperationCanceledException` | `DecideAsync` | 呼叫端的權杖被取消（從不包裝） |
| `InvalidOperationException` | 模擬提供者的 `DecideAsync` | 設定的模擬答案與請求不符 |
| `InvalidOperationException` | 模擬提供者的 `.Noul`／`.Choice`／`.Score`／`.Box`／`.WithModel` | 在每次失敗或自訂處理常式的模擬提供者上呼叫 |

`SystemOneException` 有 `StatusCode`（`HttpStatusCode?`）和 `ResponseBody`（`string?`）。它的訊息取自 clef-mlx、
Ollama、llama-server、OpenRouter、Vercel、TypeSafe 和 Cloudflare 的錯誤格式，都不符合時就是原始本文。

## 模擬提供者（`SystemOne.Testing`）

| 呼叫 | 結果 |
|---|---|
| `SystemOne.Providers.UseMock()` | 每一題都給預設答案 |
| `.Noul(name, probability)` | 該是非題回答 `probability`（0–1，設定當下就檢查） |
| `.Choice(name, option)` | 該單選題回答 `option`；它的機率為 1，其餘為 0 |
| `.Score(name, level)` | 該刻度題回答 `level`；最接近的刻度機率為 1；legend 取自題目 |
| `.Box(name, top, bottom, left, right, confidence = null)` | 該框選題回答這些比例（設定當下就檢查） |
| `.WithModel(model)` | `Response.Model`；請求自己指定的模型優先 |
| `SystemOne.Providers.UseMock(exception)` | 每次呼叫都丟出 `exception` |
| `SystemOne.Providers.UseMock(request => response)` | 每次呼叫都回傳你的函式的結果 |
| `mock.Requests` | **這一個**執行個體收到的請求，依序 |

- 每個設定方法都回傳**新的**模擬提供者，原本的不變。
- 預設答案：是非題 0.5；單選題第一個選項、機率均分；刻度題中間刻度（`Count / 2`）、機率均分；框選題整張影像；
  模型 `mock`。
- 設定的答案與請求不符（題型不同、選項不存在、分數超過最高刻度）時，`DecideAsync` 丟出
  `InvalidOperationException`。
- 自訂處理常式的回應用 `SystemOne.Factory.Response(model, (name, answer), …)` 建立，答案用
  `SystemOne.Factory.Noul/Choice/Box` 建立。

## 權重來源

| 呼叫 | 套件 | `ResolveAsync` 回傳 |
|---|---|---|
| `SystemOne.ModelSources.FromDirectory(string path)` | Abstractions | `path`；不存在時丟 `DirectoryNotFoundException` |
| `SystemOne.ModelSources.FromHuggingFace(string repository, string? subfolder = null, string revision = "main", string? cacheDirectory = null, HttpClient? http = null)` | HuggingFace | 存放已驗證檔案的快取目錄 |

`ResolveAsync(IProgress<SystemOne.ModelDownloadProgress>? progress = null, CancellationToken
cancellationToken = default)`。`FromHuggingFace` 只在 `ResolveAsync` 裡連網；快取路徑為
`{cacheDirectory}/{repository 以 '_' 取代 '/'}/{revision}/{subfolder}`，預設 `cacheDirectory` 為
`LocalApplicationData/systemone/models`；LFS 檔案以 SHA-256 驗證，小檔案檢查大小；資訊清單讓之後的呼叫不必連網。
失敗：`SystemOneException`（遠端、雜湊）、`IOException`（磁碟）。

## 規則

要做：

- 從 `SystemOne.Requests` 的第一題開始串接，以 `.For(state)` 結尾。
- 每一種設定只保留一個提供者執行個體並重複使用；它可以安全地同時呼叫。
- 把 `CancellationToken` 傳給 `DecideAsync`。
- 服務失敗時攔截 `SystemOneException`，檢查 `StatusCode` 與 `ResponseBody`。
- 為了可用性使用 `UseFallback(selfHosted, hosted)`，除非資料是個人資料。
- 單元測試用 `UseMock()`；單元測試絕不呼叫真正的端點。

不要做：

- `using SystemOne;` 或匯入任何 `SystemOne.*` 命名空間：它們不存在。
- 在使用端程式碼寫 `new SystemOne.Request { … }`、`new SystemOne.NoulQuestion { … }`、
  `new SystemOne.NoulAnswer { … }`、`new HttpSystemOneProvider(…)`。
- 用 `request with { Questions = … }` 加題目，或用 `with { State = … }` 設定狀態；請用串接方法與 `.For(state)`。
- 呼叫串接方法卻忽略回傳值（單獨一行 `request.Noul(...)` 什麼都不會發生）。
- 把 `ScoreAnswer.Level` 當成整數或標籤。
- 把 `Confidence` 當成正確率。
- 把 `box` 題目送給 TypeSafe `jev-preview` 以外的提供者。
- 在 `DecideAsync` 外層攔截 `Exception`，把 `OperationCanceledException` 吞掉。

## 常見寫法

動態題目：

```csharp
var template = SystemOne.Requests.Noul("escalate", "Should a person review this?");

foreach (var flag in flags)
{
    template = template.Noul(flag.Name, flag.Question);
}

var request = template.For(signals);
```

影像與框選：

```csharp
var typeSafe = SystemOne.Providers.UseTypeSafeAi(apiKey);
var request = SystemOne.Requests
    .Image("photo", "id-card.png")
    .Box("face", "the face")
    .WithModel("jev-preview")
    .For("Locate the face on photo");
var bounds = (await typeSafe.DecideAsync(request, cancellationToken)).GetBox("face").Bounds;
```

自架優先、託管服務作為後援（僅限非個人資料）：

```csharp
var systemOne = SystemOne.Providers.UseFallback(
    SystemOne.Providers.UseClef(new Uri("http://localhost:8000")),
    SystemOne.Providers.UseTypeSafeAi(apiKey));
```

搭配 `IHttpClientFactory` 的相依性插入：

```csharp
// 每個服務一個具名用戶端，以服務命名並設定逾時
const string CLEF_HTTP_CLIENT = "clef";

services.AddHttpClient(CLEF_HTTP_CLIENT, client => client.Timeout = TimeSpan.FromSeconds(10));

services.AddTransient<ISystemOne>(provider => SystemOne.Providers.UseClef(
    baseUri: new Uri("http://localhost:8000"),
    http: provider.GetRequiredService<IHttpClientFactory>().CreateClient(CLEF_HTTP_CLIENT)));
```

用模擬提供者寫單元測試：

```csharp
var mock = SystemOne.Providers
    .UseMock()
    .Noul("refund", 0.95)
    .Choice("department", "billing");

var response = await mock.DecideAsync(request);

var sent = mock.Requests[0];
```

通用處理每個答案：

```csharp
foreach (var (name, answer) in response.Answers)
{
    var text = answer switch
    {
        SystemOne.NoulAnswer noul => noul.Probability.ToString("0.00", CultureInfo.CurrentCulture),
        SystemOne.ChoiceAnswer choice => choice.Option,
        SystemOne.ScoreAnswer score => score.Level.ToString("0.00", CultureInfo.CurrentCulture),
        SystemOne.BoxAnswer box => box.Bounds.ToString(),
        _ => answer.GetType().Name,
    };

    Console.WriteLine($"{name}: {text}");
}
```

## 傳輸格式

上面的最小程式，`HttpSystemOneProvider` 送到 `POST /v1/systemone` 的內容：

```json
{
  "model": "clef-flash",
  "state": { "subject": "Duplicate charge", "body": "Refund the second payment today." },
  "questions": {
    "refund": { "type": "noul", "instructions": "Does the customer ask for a refund?" },
    "department": {
      "type": "choice",
      "criteria": { "billing": "invoices, payments, refunds", "technical": "bugs, outages" },
      "instructions": "Which department handles this?"
    },
    "urgency": {
      "type": "score",
      "criteria": ["low", "medium", "high"],
      "instructions": "How urgent is it?"
    }
  }
}
```

典型的回應：

```json
{
  "model": "clef-flash",
  "answers": {
    "refund": { "type": "noul", "noul": 0.97 },
    "department": {
      "type": "choice",
      "choice": "billing",
      "confidence": 0.99,
      "probabilities": { "billing": 0.99, "technical": 0.01 }
    },
    "urgency": {
      "type": "score",
      "score": 1.62,
      "confidence": 0.66,
      "legend": { "0": "low", "1": "medium", "2": "high" },
      "probabilities": { "0": 0.04, "1": 0.30, "2": 0.66 }
    }
  },
  "usage": { "input_tokens": 312, "output_tokens": 0 }
}
```

影像以 data URL 組成 `images` 陣列送出；TypeSafe 改收 `assets` 並帶 `TypeSafe-Danger-Zone-Images: true` 標頭，
Cloudflare 則把回應包在 `{"success", "result", "errors"}` 裡。這些都由函式庫處理。