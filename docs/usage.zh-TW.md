# 使用 SystemOne

[English](usage.md)

這份指南涵蓋使用這個函式庫所需的一切：建立請求、讀取答案、選擇提供者、處理錯誤、撰寫測試，以及載入本機模型權重。
如果要加入自己的提供者或輔助型別，請改讀 [writing-providers.zh-TW.md](writing-providers.zh-TW.md)。

- [基本概念](#基本概念)
- [安裝](#安裝)
- [第一次呼叫](#第一次呼叫)
- [建立請求](#建立請求)
- [讀取回應](#讀取回應)
- [提供者](#提供者)
- [錯誤與取消](#錯誤與取消)
- [用模擬提供者寫測試](#用模擬提供者寫測試)
- [本機模型權重](#本機模型權重)

## 基本概念

System One 決策模型讀取一份**狀態**（任何文字或 JSON），以機率回答**具名的題目**。
它不產生文字，所以答案是數值或標籤，不是一段話。

| 題型 | 問什麼 | 答案 |
|---|---|---|
| `noul` | 是非題 | 「是」的機率 `Probability`，0–1 |
| `choice` | 從 1–255 個具名選項中選一個 | 選中的 `Option`、它的 `Confidence`、每個選項的機率 |
| `score` | 把狀態放到 1–10 級的有序刻度上 | 期望刻度 `Level`（從 0 起算、可有小數）、`Confidence`、刻度名稱 |
| `box` | 在附加的影像中找出某個東西（只有 TypeSafe 的 `jev-preview`） | 以影像比例表示的 `Bounds` |

會用到三種物件：

- **請求**：狀態、題目，以及選用的模型名稱與影像。
- **提供者**（`ISystemOne`）：負責回答請求，可能是自架伺服器、託管服務、後援鏈或模擬提供者。
- **回應**：實際回答的模型、每個題目名稱各一個答案，以及權杖用量。

所有型別都在**全域命名空間**。`SystemOne` 是靜態類別而不是命名空間，所以不要寫 `using SystemOne;`
（無法編譯）。一切都從它身上的四個掛載點開始：

| 掛載點 | 建立什麼 | 範例 |
|---|---|---|
| `SystemOne.Providers` | 提供者 | `SystemOne.Providers.UseClef(uri)` |
| `SystemOne.Requests` | 請求 | `SystemOne.Requests.Noul(...).For(state)` |
| `SystemOne.ModelSources` | 本機權重來源 | `SystemOne.ModelSources.FromDirectory(path)` |
| `SystemOne.Factory` | 答案、回應、影像 | `SystemOne.Factory.Noul(0.9)` |

請求與答案的 record 有公開的 setter，只是為了序列化。不要用 `new` 建立它們，請透過掛載點建立
（理由見 [ADR-0001](adr/0001-object-creation.md)）。

## 安裝

只安裝會呼叫到的套件。所有套件共用同一個版號。

| 套件 | 什麼時候安裝 | 提供 |
|---|---|---|
| `SystemOne` | 呼叫任何自架的 `/v1/systemone` 伺服器，或串接多個提供者 | `UseSelfHosted`、`UseFallback` |
| `SystemOne.Providers.Clef` | 自己架設 clef（例如 clef-mlx） | `UseClef` |
| `SystemOne.Providers.TypeSafe` | 呼叫 TypeSafe 託管的 Jev | `UseTypeSafeAi` |
| `SystemOne.Providers.Cloudflare` | 呼叫 Cloudflare Workers AI 上的 clef | `UseCloudflareWorkersAi` |
| `SystemOne.Providers.OpenRouter` | 透過 OpenRouter 呼叫任何模型 | `UseOpenRouter` |
| `SystemOne.Providers.Vercel` | 透過 Vercel AI Gateway 呼叫 | `UseVercelAiGateway` |
| `SystemOne.Testing` | 撰寫測試或離線開發 | `UseMock` |
| `SystemOne.ModelSources.HuggingFace` | 替行程內提供者下載模型權重 | `FromHuggingFace` |
| `SystemOne.Abstractions` | 只需要契約（上面每個套件都會帶進來） | 只有型別 |

```bash
dotnet add package SystemOne.Providers.Clef
dotnet add package SystemOne.Testing
```

## 第一次呼叫

```csharp
var systemOne = SystemOne.Providers.UseClef(new Uri("http://localhost:8000"));

var request = SystemOne.Requests
    .Noul("large", "Is this a large purchase?")
    .Choice("category", "Merchant category", options => options
        .Option("grocery", "supermarkets")
        .Option("convenience", "convenience stores")
        .Option("electronics"))
    .Score("risk", "Fraud risk", "low", "medium", "high")
    .For(new { amount = 12000, merchant = "7-Eleven", time = "02:13" });

var response = await systemOne.DecideAsync(request);

var large = response.GetNoul("large").Probability;
var category = response.GetChoice("category").Option;
var risk = response.GetScore("risk").Level;

Console.WriteLine(string.Create(
    provider: CultureInfo.CurrentCulture,
    handler: $"{large:0.00} {category} {risk:0.00} (answered by {response.Model})"));
```

換成別的提供者時，只要改第一行；其餘程式碼對每個提供者都一樣。

## 建立請求

### 狀態

請求鏈從 `SystemOne.Requests` 的第一題開始，以 `.For(...)` 結尾，帶入狀態，也就是題目所指涉的內容。
在那之前請求只是範本，`DecideAsync` 會拒絕它：

```csharp
var questions = SystemOne.Requests.Noul("refund", "Does the customer ask for a refund?");

// 匿名物件、POCO 或字典會以 System.Text.Json 序列化
var fromObject = questions.For(new { subject = "Refund", body = "Charged twice" });

// 純文字
var fromText = questions.For("Customer says the parcel never arrived.");

// 手上已有的 JsonNode（JsonObject、JsonArray、JsonValue）
var fromJson = questions.For(new JsonObject { ["amount"] = 12000 });
```

POCO 會保留 C# 的屬性名稱（例如 `Amount`），因為 `For(object)` 使用預設的序列化選項。
需要精確的欄位名稱時，請傳 `JsonObject` 或匿名物件。模型會讀欄位名稱與值，所以名稱要取得有意義。

### 題目

每一題都有**名稱**（之後用它讀取答案）和選用的**指示**（給模型看的說明）：

```csharp
var request = SystemOne.Requests
    // 是非題
    .Noul("refund", "Does the customer explicitly ask for a refund?")
    // 單選題，每個選項附上給模型的說明
    .Choice("department", "Which department should handle this?", options => options
        .Option("billing", "invoices, payments, refunds")
        .Option("technical", "bugs, outages")
        .Option("sales"))
    // 只有選項名稱
    .Choice("language", "Language of the message", "zh-TW", "en", "ja")
    // 有序刻度，由低到高
    .Score("urgency", "How urgent is this?", "not urgent", "soon", "blocking")
    .For(ticket);
```

- 選項與刻度名稱可以用任何語言，包括中文。回答時會原樣當作答案的鍵與標籤回傳。
- 選項說明可以省略（`.Option("sales")`）。
- 新增的題目若與既有題目同名，會取代先前那一題。
- `.Box(name, instructions)` 要求在附加的影像中框出範圍，只有 TypeSafe 的 `jev-preview` 模型會回答。

### 影像

```csharp
var request = SystemOne.Requests
    // 讀檔，格式取自副檔名
    .Image("photo", "id.png")
    // 或是位元組加格式：png、jpg、jpeg 或 webp
    .Image("thumbnail", bytes, "png")
    .Box("face", "the face on the ID card")
    .WithModel("jev-preview")
    .For("photo");
```

影像名稱是預留位置字串。TypeSafe 會把狀態或指示裡出現這個字串的地方換成影像；其他提供者則把影像與狀態一起送出。
新增同名的影像會取代先前那張。函式庫只送出位元組，從不傳遠端網址。

以下是函式庫**不會**替你檢查的提供者限制：

- TypeSafe 拒收小於 512 px 的影像，只接受 png、jpg 和 jpeg。
- Cloudflare Workers AI 最多接受 4 張影像。

### 模型

請求沒有指定模型時，使用提供者的預設模型：

```csharp
var preview = request.WithModel("jev-preview");
```

### 請求不可變

每個串接方法都回傳**新的**請求，原本的不變。要在迴圈裡加題時，從一題固定的題目開始，再重新指派：

```csharp
var template = SystemOne.Requests.Noul("escalate", "Should a person review this?");

foreach (var flag in flags)
{
    template = template.Noul(flag.Name, flag.Question);
}

var request = template.For(signals);
```

沒有 `.For(...)` 的請求鏈就是範本：留著它，每份狀態各呼叫一次 `.For(state)`，範本本身不會變。
要共用範本時，把它放到 `SystemOne.Requests` 掛載點上，像內建方法一樣呼叫，
例如 `SystemOne.Requests.Moderation(message)`。做法見[請求範本](writing-providers.zh-TW.md#請求範本)。

### 驗證

送出前會先檢查請求，違反時丟出 `ArgumentException`：

| 規則 | 檢查時機 |
|---|---|
| 題目、選項與影像的名稱不可空白 | 呼叫串接方法時 |
| 已用 `.For(...)` 帶入狀態 | 呼叫 `DecideAsync` 時 |
| 同一題的選項名稱不可重複 | 呼叫 `.Option(...)` 時 |
| 至少一題 | 呼叫 `DecideAsync` 時 |
| 單選題有 1–255 個選項 | 呼叫 `DecideAsync` 時 |
| 刻度題有 1–10 級 | 呼叫 `DecideAsync` 時 |
| 每張影像都有位元組和格式 | 呼叫 `DecideAsync` 時 |
| 請求或提供者有指定模型（HTTP 提供者） | 呼叫 `DecideAsync` 時 |

## 讀取回應

```csharp
var response = await systemOne.DecideAsync(request);

var refund = response.GetNoul("refund");
var department = response.GetChoice("department");
var urgency = response.GetScore("urgency");
```

| 方法 | 回傳 | 屬性 |
|---|---|---|
| `GetNoul(name)` | `SystemOne.NoulAnswer` | `Probability`：「是」的機率，0–1 |
| `GetChoice(name)` | `SystemOne.ChoiceAnswer` | `Option`：選中的選項名稱；`Confidence`：它的機率；`Probabilities`：選項名稱 → 機率 |
| `GetScore(name)` | `SystemOne.ScoreAnswer` | `Level`：期望刻度索引，從 0 起算、可有小數；`Confidence`：機率最高那一級的機率；`Legend`：`"0"`、`"1"`… → 刻度名稱；`Probabilities`：`"0"`、`"1"`… → 機率 |
| `GetBox(name)` | `SystemOne.BoxAnswer` | `Bounds.Top`、`Bounds.Bottom`、`Bounds.Left`、`Bounds.Right`：佔影像高度與寬度的比例；`Confidence`（可能為 null） |

找不到該名稱的答案，或答案是別的題型時，每個 `Get*` 方法都會丟出 `SystemOneException`，訊息會列出有哪些名稱。

刻度題的 `Level` 是加權平均，不是標籤。要顯示最接近的刻度名稱：

```csharp
var urgency = response.GetScore("urgency");
var nearest = (int)Math.Round(urgency.Level);
var label = urgency.Legend[nearest.ToString(CultureInfo.InvariantCulture)];
```

單選題與刻度題的 `Confidence` 是最高選項或刻度的機率，**不是**答案正確的機率。

回應的其他部分：

- `response.Model`：實際回答的模型。用後援鏈時，看它就知道是哪個提供者回答的。
- `response.Usage.InputTokens` 與 `response.Usage.OutputTokens`：權杖數。伺服器沒有回報時為 0；
  `Usage` 永遠不會是 null。
- `response.Answers`：以名稱為鍵的所有答案，適合通用處理：

```csharp
foreach (var (name, answer) in response.Answers)
{
    switch (answer)
    {
        case SystemOne.NoulAnswer noul:
            {
                Console.WriteLine(string.Create(
                    provider: CultureInfo.CurrentCulture,
                    handler: $"{name}: {noul.Probability:0.00}"));
                break;
            }

        case SystemOne.ChoiceAnswer choice:
            {
                Console.WriteLine($"{name}: {choice.Option}");
                break;
            }
    }
}
```

## 提供者

每個提供者都由一個 `Use*` 呼叫建立，回傳 `ISystemOne`。

| 呼叫 | 由誰回答 | 資料是否離開你的網路 | 套件 |
|---|---|---|---|
| `UseClef(baseUri)` | 自己的 clef 伺服器，模型 `clef-flash` | 否 | `SystemOne.Providers.Clef` |
| `UseSelfHosted(baseUri, model)` | 任何自架的 `/v1/systemone` 伺服器：Kev、laya-serve、d3-mlx、Ollama | 否 | `SystemOne` |
| `UseTypeSafeAi(apiKey, model = "jev-latest")` | TypeSafe 託管的 Jev | 是 | `SystemOne.Providers.TypeSafe` |
| `UseCloudflareWorkersAi(accountId, apiToken, model = "clef-flash")` | Cloudflare Workers AI 上的 clef | 是 | `SystemOne.Providers.Cloudflare` |
| `UseOpenRouter(apiKey, model)` | OpenRouter 上的任何模型，例如 `cloudflare/clef-flash` | 是 | `SystemOne.Providers.OpenRouter` |
| `UseVercelAiGateway(apiKey, model)` | Vercel AI Gateway，例如 `typesafe-ai/jev` | 是 | `SystemOne.Providers.Vercel` |
| `UseFallback(first, second, …)` | 第一個成功的提供者 | 視鏈中的提供者而定 | `SystemOne` |
| `UseMock()` | 固定答案，不連網 | 否 | `SystemOne.Testing` |

`UseSelfHosted` 和 `UseClef` 會把 `baseUri` 的路徑換成 `/v1/systemone`。

### HttpClient

每個 HTTP 的 `Use*` 最後都有一個選用的 `HttpClient` 參數。沒有傳入時，所有提供者共用
`SystemOneProvider.DefaultHttpClient`：逾時 60 秒，連線每 2 分鐘回收一次，所以會跟上 DNS 的變更。

需要 Proxy 伺服器、自訂處理常式或不同的逾時時，傳入自己的用戶端。搭配 `IHttpClientFactory` 時，
每個服務註冊一個設好逾時的具名用戶端；提供者則在每次使用時建立，不要註冊成單一執行個體，讓工廠能輪替處理常式。
提供者的建立成本很低：

```csharp
// 每個服務一個具名用戶端，以服務命名並設定逾時
const string CLEF_HTTP_CLIENT = "clef";

services.AddHttpClient(CLEF_HTTP_CLIENT, client => client.Timeout = TimeSpan.FromSeconds(10));

services.AddTransient<ISystemOne>(provider => SystemOne.Providers.UseClef(
    baseUri: new Uri("http://localhost:8000"),
    http: provider.GetRequiredService<IHttpClientFactory>().CreateClient(CLEF_HTTP_CLIENT)));
```

### 後援鏈

`UseFallback` 依序嘗試每個提供者：

```csharp
var systemOne = SystemOne.Providers.UseFallback(
    SystemOne.Providers.UseClef(new Uri("http://localhost:8000")),
    SystemOne.Providers.UseTypeSafeAi(apiKey));
```

- 遇到 `SystemOneException`（伺服器錯誤、連不上、逾時、無法解析的回應）就換下一個提供者。
- 其他例外會立刻中止整條鏈：不合法的請求（`ArgumentException`）、呼叫端自己取消
  （`OperationCanceledException`）、模擬提供者的設定錯誤。
- 全部失敗時，後援鏈丟出一個列出每次失敗的 `SystemOneException`，`InnerException` 是最後一次失敗。

**順序決定資料可能流向哪裡。** 把自架的提供者放在前面；狀態含有個人資料時，託管提供者完全不要放進鏈裡。

## 錯誤與取消

| 例外 | 什麼時候發生 | 怎麼處理 |
|---|---|---|
| `ArgumentException`（以及 `ArgumentNullException`） | 請求不合法，還沒送出任何東西；或 `Use*`、`FromHuggingFace` 的引數不合法（API 金鑰空白、存放庫不是 `name` 或 `owner/name`） | 修正請求或引數 |
| `SystemOneException` | 伺服器拒絕請求；回應無法解析；伺服器連不上；`HttpClient.Timeout` 到期；答案不存在或題型不符（`Get*`） | 檢查 `StatusCode`、`ResponseBody` 和 `InnerException`，或交給後援鏈換下一個 |
| `OperationCanceledException` | 你的 `CancellationToken` 被取消 | 不用處理，這是刻意取消的 |
| `InvalidOperationException` | 模擬提供者設定的答案與請求不符 | 修正測試設定 |

```csharp
try
{
    var response = await systemOne.DecideAsync(request, cancellationToken);
}
catch (SystemOneException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized)
{
    // 伺服器拒絕了 API 金鑰
    Console.WriteLine($"Check the API key: {exception.ResponseBody}");
}
```

函式庫會從 clef-mlx、Ollama、llama-server、OpenRouter、Vercel、TypeSafe 和 Cloudflare 的錯誤格式中取出可讀的訊息；
都不符合時，訊息就是原始的回應本文。

## 用模擬提供者寫測試

`SystemOne.Testing` 提供一個不連網就能回答、並記錄每個請求的提供者。

```csharp
var happy = SystemOne.Providers
    .UseMock()
    .Noul("refund", 0.99)
    // 選中的選項機率為 1，其餘為 0
    .Choice("department", "billing")
    // 最接近的刻度機率為 1；legend 依題目的刻度展開
    .Score("urgency", 2)
    // 四邊是佔影像高度與寬度的比例
    .Box("face", top: 0.1, bottom: 0.6, left: 0.2, right: 0.9);

var response = await happy.DecideAsync(request);
```

模擬提供者建好之後就不會再變。每個設定方法都回傳**新的**模擬提供者，所以可以從同一份基礎設定衍生出多個變體：

```csharp
var calm = happy.Score("urgency", 0);
var named = happy.WithModel("fake-model");
```

沒有設定的題目會得到預設答案：是非題 0.5；單選題選第一個選項、機率均分；刻度題選中間的刻度、機率均分；
框選題框住整張影像。回應的模型名稱是 `mock`，除非呼叫了 `WithModel` 或請求自己指定了模型。

另外兩種模擬提供者用來模擬失敗和完全自訂：

```csharp
// 每次呼叫都丟出這個例外
var down = SystemOne.Providers.UseMock(new SystemOneException("outage"));

// 每次呼叫都交給你的函式回答；這裡示範成功一次後失敗
var calls = default(int);
var flaky = SystemOne.Providers.UseMock(delegate
{
    calls++;

    if (calls > 1)
    {
        throw new SystemOneException("outage");
    }

    return SystemOne.Factory.Response("custom", ("refund", SystemOne.Factory.Noul(0.1)));
});
```

在這兩種模擬提供者上呼叫 `.Noul(...)` 這類逐題設定方法，會丟出 `InvalidOperationException`，因為那些答案永遠不會被用到。

`mock.Requests` 依序列出**這一個**模擬提供者執行個體收到的請求。請從交給受測程式碼的那個執行個體讀取。

設定的答案與請求不符時，`DecideAsync` 會丟出 `InvalidOperationException`：題型不同、題目沒有該選項，
或分數超過最高刻度。這樣過時的測試設定會直接失敗，而不是回傳不可能出現的答案。

## 本機模型權重

在行程內執行模型的提供者需要權重放在磁碟上。`SystemOne.ModelSource` 描述權重從哪裡來，
`ResolveAsync` 回傳本機目錄：

```csharp
// 已經放在磁碟上，例如離線的機器
var local = SystemOne.ModelSources.FromDirectory(@"D:\models\laya");

// 從 Hugging Face 下載一次（套件 SystemOne.ModelSources.HuggingFace）
var hub = SystemOne.ModelSources.FromHuggingFace(
    repository: "convaiinnovations/laya",
    subfolder: "multilingual");

var progress = new Progress<SystemOne.ModelDownloadProgress>(p =>
{
    Console.WriteLine(string.Create(
        provider: CultureInfo.CurrentCulture,
        handler: $"{p.File}: {p.BytesReceived} / {p.TotalBytes}"));
});

var directory = await hub.ResolveAsync(progress, cancellationToken);
```

`FromDirectory` 只檢查目錄是否存在，不存在時丟出 `DirectoryNotFoundException`。

`FromHuggingFace`：

- 只有在 `ResolveAsync` 裡才會連網，建立時不會；
- 存放庫不是 `name` 或 `owner/name`，或修訂、子資料夾是絕對路徑或含 `..` 時，丟出 `ArgumentException`，
  所以快取路徑不會離開 `cacheDirectory`；
- 下載到 `{cacheDirectory}/{repository 以 '_' 取代 '/'}/{revision}/{subfolder}`。預設的快取目錄是本機應用程式資料夾下的
  `systemone/models`（Windows 上是 `%LOCALAPPDATA%`）；
- 每個大型（LFS）檔案都對照 Hugging Face 公布的 SHA-256 驗證，小檔案則檢查大小。檔案通過驗證後才移到定位，
  所以目錄裡永遠不會有寫到一半的檔案；
- 下載完成後寫入資訊清單。之後只要資訊清單裡的每個檔案都存在且大小正確，就直接回傳目錄，完全不連網；
- 列檔、下載或雜湊不符等失敗以 `SystemOneException` 回報，磁碟失敗則是 `IOException`。

用 `revision:` 固定分支、標籤或認可，用 `cacheDirectory:` 改用別的快取資料夾，用 `http:` 傳入自己的 `HttpClient`。