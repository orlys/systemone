# ADR-0001：物件建立方式的統一

## 狀態

已採用（2026-10-23）

## 背景

函式庫在幾輪重構後，同一種物件有多種建立方式：

| 物件 | 重構前的建立方式 |
|---|---|
| 提供者 | `SystemOne.Providers.UseX(...)`，也能直接 `new XSystemOneProvider(...)` |
| 端點 | `SystemOneEndpoint.SelfHosted(...)` 靜態方法，也能 `new SystemOneEndpoint { ... }` |
| 請求 | `SystemOne.Requests.For(...)` 的鏈；模型與影像卻要用 `with { ... }`；README 還示範物件初始設定式 |
| 題目 | 由請求鏈建立，也能 `new NoulQuestion { ... }` |
| 答案、回應、影像 | `SystemOne.Factory.X(...)`，也能 `new XAnswer { ... }` |
| 權重來源 | `SystemOne.ModelSources.FromX(...)`，也能 `new HuggingFaceModelSource(...)` |

使用者要記得每種物件該走哪一條路，擴充套件也沒有可以照著做的慣例。

## 決策

### 1. 一般使用者只經由 `SystemOne.<掛載點>` 建立物件

| 要建立的東西 | 唯一建議的方式 |
|---|---|
| 提供者 | `SystemOne.Providers.UseX(...)` |
| 請求、題目、影像 | `SystemOne.Requests.Noul(...).Image(...).WithModel(...).For(state)` |
| 權重來源 | `SystemOne.ModelSources.FromX(...)` |
| 答案、回應、獨立的影像 | `SystemOne.Factory.X(...)` |

建構函式只保留給三個擴充點與例外：

- `new HttpSystemOneProvider(http, new SystemOneEndpoint { ... })`：包裝另一個 `/v1/systemone` 伺服器。
- 繼承 `SystemOneProvider` 或 `SystemOne.ModelSource`。
- `new SystemOneException(...)`：照 .NET 慣例。

已經有 `Use*` 或 `From*` 的型別，建構函式改成 `internal`：`MockSystemOneProvider`、
`FallbackSystemOneProvider`、`TypeSafeSystemOneProvider`、`CloudflareSystemOneProvider`、
`HuggingFaceModelSource`。`SystemOneEndpoint.SelfHosted` 也改成 `internal`。

### 2. 可以串接的值都不可變，而且鏈本身要完整

- `SystemOne.Request` 與 `MockSystemOneProvider` 的每個串接方法都回傳新物件，原本的不變。
- 請求鏈補上 `.Image(...)` 與 `.WithModel(...)`，建立請求不再需要 `with` 或 `new`。
- 執行期才知道的題目，就是在迴圈裡繼續串鏈。

### 3. DTO 保留公開的 `required { get; set; }`，不隱藏 `new`

請求、題目、答案、回應、影像都是要序列化的 record。公開的 setter 留著，因為：

- System.Text.Json 的來源產生器需要它們，規則 032 也要求這個形狀。
- 改成 `internal` setter 加 `[JsonInclude]` 行不通：`required` 成員的存取層級不能低於型別；
  `SystemOne.Providers.Cloudflare` 在另一個組件裡以來源產生器反序列化回應，讀不到 `internal` setter。

這些型別的 XML 文件註明了應該用哪個掛載點建立。

## 命名慣例

新增 API 時照這個慣例：

- 掛載點的入口用動詞：`Use*` 回傳可以呼叫的提供者，`From*` 指名來源。請求鏈直接從第一題開始，
  例如 `SystemOne.Requests.Noul(...)`。
- 新增一個項目用名詞：題型名稱（`Noul`、`Choice`、`Score`、`Box`）、`Image`、`Answer`。
  同一個名字的意思由所在的物件決定：在請求上是加一題，在模擬提供者上是設定答案，在 `Factory` 上是建立答案。
- 替換一個設定用 `With*`，例如 `WithModel`。
  請求本身有 `Model` 屬性，C# 不允許同名的方法，`With*` 也是 .NET 不可變型別的慣用寫法。
- 帶入每次呼叫的輸入用 `For`，放在請求鏈的最後：`.For(state)`。題目是可重複使用的部分，
  狀態是每次都會變的輸入，所以還沒呼叫 `.For` 的請求就是範本，送出時會被拒絕。
- 讀取答案用 `Get*`：`GetNoul`、`GetChoice`、`GetScore`、`GetBox`。
- 已有具名版本的泛型底層方法（例如 `Get<T>`）以 `[EditorBrowsable(EditorBrowsableState.Never)]`
  從 IntelliSense 隱藏；仍可呼叫，給依型別參數處理的程式碼用。
- 題目裡的清單以 lambda 設定，例如 `.Choice(name, instructions, options => options.Option("a", "說明"))`；
  lambda 收到的清單同樣不可變。刻度沒有說明，所以 `.Score` 直接收刻度名稱。
- 擴充套件以 `this SystemOneProviders`、`this SystemOneRequests`、`this SystemOneModelSources`、
  `this SystemOneFactory` 擴充方法加入新的建立方式。

## 考慮過的替代方案

- **全部集中到一個 `SystemOne.Factory`**：提供者、請求、權重來源混在同一個清單裡，
  IntelliSense 更難找，也失去依用途分組的意義。
- **回到純建構函式與物件初始設定式**：與已定案的掛載點方向相反，也無法讓擴充套件加入新的建立方式。
- **每種物件各有獨立的 Builder 型別再 `Build()`**：多出一倍型別，
  而不可變的串接鏈已經提供同樣的「以基礎設定衍生變體」能力。

## 後果

- 正面：IntelliSense 從 `SystemOne.` 一路點下去就能找到所有要建立的東西；
  擴充套件與內建功能長得一樣；串接的語意只有一種。
- 負面：五個建構函式改成 `internal` 是中斷性變更。
  原本以 `new` 建立廠商提供者的 DI 寫法，改用 `UseX(..., http: client)`。
- 殘留：DTO 的物件初始設定式仍然出現在 IntelliSense，只能靠文件引導。