# SystemOne

[![NuGet][nuget-badge]][nuget]
[![CI][ci-badge]][ci]
[![License: MIT][license-badge]][license]

[English][readme-en] | 繁體中文

[System One][system-one] 決策模型的型別化 .NET 用戶端。對任何文字或 JSON 提出具名的題目，取回校準過的機率，
而不是產生的文字：

| 題型 | 問的是 | 答案 |
|---|---|---|
| `noul` | 是非題 | 「是」的機率 `Probability` |
| `choice` | 從最多 255 個選項中選一個 | 選中的 `Option`、它的 `Confidence`，以及每個選項的機率 |
| `score` | 在有序刻度上落在哪裡 | 期望刻度 `Level`（從 0 起算），以及每個刻度的機率 |
| `box` | 附加的影像中某個東西在哪裡 | 以影像比例表示的範圍 `Bounds` |

同一份程式碼可以對自己硬體上的模型、託管服務或記憶體內的模擬提供者執行，只有建立提供者的那一行不同。

## 特色

- **型別化的請求與答案。** 以串接方法建立請求，再用 `GetNoul`、`GetChoice`、`GetScore`、`GetBox` 讀取型別化的答案。
- **一套 API，多個提供者。** 自架伺服器（clef、Kev、laya-serve、Ollama）與託管服務
  （TypeSafe Jev、Cloudflare Workers AI、OpenRouter、Vercel AI Gateway）。
- **後援鏈。** 先試自己的伺服器，失敗再改用託管服務。
- **可重複使用的請求範本。** 題目只寫一次，每次送出不同的狀態。
- **測試用的模擬提供者。** 依題目形狀回傳固定答案，不連網。
- **沒有第三方相依。** 只用 .NET 10 的基底類別庫。
- **給程式碼 agent 的文件。** 每個套件都附有寫給 AI 程式碼 agent 的使用指南。

## 安裝

套件依用途拆分，只安裝會呼叫到的。

```bash
dotnet add package SystemOne.Providers.Clef
dotnet add package SystemOne.Testing
```

| 套件 | 內容 |
|---|---|
| [`SystemOne.Abstractions`][pkg-abstractions] | `ISystemOne`、`SystemOneProvider` 基底類別、請求與答案型別、`SystemOneException` |
| [`SystemOne`][pkg-core] | 給任何自架 `/v1/systemone` 伺服器的 `UseSelfHosted`、`UseFallback` |
| [`SystemOne.Providers.Clef`][pkg-clef] | 給自己的 clef 伺服器的 `UseClef` |
| [`SystemOne.Providers.TypeSafe`][pkg-typesafe] | 給 TypeSafe 託管 Jev 的 `UseTypeSafeAi` |
| [`SystemOne.Providers.Cloudflare`][pkg-cloudflare] | 給 Cloudflare Workers AI 上 clef 的 `UseCloudflareWorkersAi` |
| [`SystemOne.Providers.OpenRouter`][pkg-openrouter] | `UseOpenRouter` |
| [`SystemOne.Providers.Vercel`][pkg-vercel] | `UseVercelAiGateway` |
| [`SystemOne.Testing`][pkg-testing] | `UseMock` |
| [`SystemOne.ModelSources.HuggingFace`][pkg-huggingface] | `FromHuggingFace(...)`：給行程內提供者用、會快取並驗證的模型下載 |

## 快速開始

```csharp
// 不需要 using：所有型別都在全域命名空間，SystemOne 是根
var systemOne = SystemOne.Providers.UseClef(new Uri("http://localhost:8000"));

var request = SystemOne.Requests
    .Noul("large", "Is this a large purchase?")
    .Choice("category", "Merchant category", options => options
        .Option("grocery", "supermarkets")
        .Option("convenience", "convenience stores"))
    .Score("risk", "Fraud risk", "low", "medium", "high")
    .For(new { amount = 12000, merchant = "7-Eleven" });

var response = await systemOne.DecideAsync(request);

// 0.79
var large = response.GetNoul("large").Probability;
// "convenience"
var category = response.GetChoice("category").Option;
// 1.17：期望刻度，從 0 起算
var risk = response.GetScore("risk").Level;
```

先列題目，最後以 `.For(state)` 帶入狀態。狀態可以是任何物件、字串或 `JsonNode`。

## 提供者

| 方法 | 呼叫 | 套件 |
|---|---|---|
| `UseClef(baseUri)` | 自己的 clef 伺服器，模型 `clef-flash` | `SystemOne.Providers.Clef` |
| `UseSelfHosted(baseUri, model)` | 任何自架的 `/v1/systemone` 伺服器：Kev、laya-serve、Ollama | `SystemOne` |
| `UseTypeSafeAi(apiKey)` | TypeSafe 託管的 Jev | `SystemOne.Providers.TypeSafe` |
| `UseCloudflareWorkersAi(accountId, apiToken)` | Cloudflare Workers AI 上的 clef | `SystemOne.Providers.Cloudflare` |
| `UseOpenRouter(apiKey, model)` | 任何 OpenRouter 模型 | `SystemOne.Providers.OpenRouter` |
| `UseVercelAiGateway(apiKey, model)` | Vercel AI Gateway | `SystemOne.Providers.Vercel` |
| `UseFallback(first, second, …)` | 第一個成功的提供者 | `SystemOne` |
| `UseMock()` | 固定答案，不連網 | `SystemOne.Testing` |

上面每個方法都從 `SystemOne.Providers` 呼叫，回傳 `ISystemOne`。後援鏈讓個人資料留在自己的硬體上，
只有那台伺服器失敗時才改用下一個：

```csharp
var local = SystemOne.Providers.UseClef(new Uri("http://localhost:8000"));
var hosted = SystemOne.Providers.UseTypeSafeAi(apiKey);
var resilient = SystemOne.Providers.UseFallback(local, hosted);
```

不能離開內部網路的資料，後援鏈裡就不要放託管提供者。

## 請求範本

沒有 `.For(...)` 的請求鏈就是範本。留著它，每次送出不同的狀態：

```csharp
var triage = SystemOne.Requests
    .Choice("team", "Which team should handle this ticket?", "billing", "technical", "sales")
    .Score("urgency", "How urgent is it?", "low", "normal", "high");

var answer = await systemOne.DecideAsync(triage.For(ticket));
```

## 測試

模擬提供者會依每個題目的形狀回傳固定答案，並記錄每一次請求。只要設定你在意的答案：

```csharp
var mock = SystemOne.Providers.UseMock()
    .Choice("team", "billing")
    .Score("urgency", 2);
```

## 錯誤

提供者那一端的失敗（HTTP 錯誤、逾時、讀不懂的回應）會丟出 `SystemOneException`，`UseFallback` 遇到它就改用下一個提供者。
不合法的請求會在送出前丟出 `ArgumentException`。取消自己的語彙基元會丟出 `OperationCanceledException`。

## 文件

| 文件 | 給誰 |
|---|---|
| [使用指南][docs-usage] | 請求、回應、提供者、錯誤、測試、模型權重 |
| [撰寫提供者][docs-providers] | 撰寫提供者、請求範本與模型來源的人 |
| [Agent 指南：使用 SystemOne][docs-agent-using] | 撰寫使用 SystemOne 程式碼的 AI 程式碼 agent |
| [Agent 指南：擴充 SystemOne][docs-agent-extending] | 撰寫提供者與擴充功能的 AI 程式碼 agent |
| [ADR-0001：物件建立方式][docs-adr] | 為什麼物件要從 `SystemOne.*` 進入點建立 |

每個套件也在 `docs/agents/` 附上 agent 指南，agent 可以在
`~/.nuget/packages/<套件 id>/<版本>/docs/agents/` 讀到與安裝版本一致的內容。

## 範例

[存放庫][repo]裡有兩個可以執行的範例。連不上 clef 伺服器時都會退到模擬提供者；設定 `SYSTEMONE_CLEF_URL` 就會改用你的伺服器。

- `samples/SystemOne.Example`：客服工單分流。
- `samples/SystemOne.ZeroTrust`：零信任存取決策。輸入存取請求的訊號，輸出端點該採取的動作、風險等級與 SOC 通報旗標，
  涵蓋從一般辦公室登入到經由 Tor 刪除管理員帳號的五個情境。

## 參與開發

以 `dotnet build -c Release -warnaserror` 建置、`dotnet test -c Release` 測試。整合測試會呼叫真正的端點，
只有設定了 `SYSTEMONE_CLEF_URL` 或 `TYPESAFE_AI_API_KEY` 才會執行；TypeSafe 的測試會產生費用。
修改程式碼或文件前請先讀 [AGENTS.md][agents]。

## 授權

[MIT][license]

[system-one]: https://docs.typesafe.ai/concepts/system-one
[repo]: https://github.com/orlys/systemone
[readme-en]: https://github.com/orlys/systemone/blob/main/README.md
[agents]: https://github.com/orlys/systemone/blob/main/AGENTS.md
[license]: https://github.com/orlys/systemone/blob/main/LICENSE
[license-badge]: https://img.shields.io/badge/license-MIT-blue.svg
[nuget]: https://www.nuget.org/packages/SystemOne
[nuget-badge]: https://img.shields.io/nuget/v/SystemOne.svg
[ci]: https://github.com/orlys/systemone/actions/workflows/ci.yml
[ci-badge]: https://img.shields.io/github/actions/workflow/status/orlys/systemone/ci.yml?branch=main&label=CI
[docs-usage]: https://github.com/orlys/systemone/blob/main/docs/usage.zh-TW.md
[docs-providers]: https://github.com/orlys/systemone/blob/main/docs/writing-providers.zh-TW.md
[docs-agent-using]: https://github.com/orlys/systemone/blob/main/docs/agents/using-systemone.zh-TW.md
[docs-agent-extending]: https://github.com/orlys/systemone/blob/main/docs/agents/extending-systemone.zh-TW.md
[docs-adr]: https://github.com/orlys/systemone/blob/main/docs/adr/0001-object-creation.md
[pkg-abstractions]: https://www.nuget.org/packages/SystemOne.Abstractions
[pkg-core]: https://www.nuget.org/packages/SystemOne
[pkg-clef]: https://www.nuget.org/packages/SystemOne.Providers.Clef
[pkg-typesafe]: https://www.nuget.org/packages/SystemOne.Providers.TypeSafe
[pkg-cloudflare]: https://www.nuget.org/packages/SystemOne.Providers.Cloudflare
[pkg-openrouter]: https://www.nuget.org/packages/SystemOne.Providers.OpenRouter
[pkg-vercel]: https://www.nuget.org/packages/SystemOne.Providers.Vercel
[pkg-testing]: https://www.nuget.org/packages/SystemOne.Testing
[pkg-huggingface]: https://www.nuget.org/packages/SystemOne.ModelSources.HuggingFace