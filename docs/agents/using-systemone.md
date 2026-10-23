# SystemOne: agent guide for consumers

[繁體中文](using-systemone.zh-TW.md) · Human guide: [usage.md](../usage.md)

Purpose: let a coding agent write correct code that **uses** SystemOne, without reading the
source. Every statement below is a fact about the current version or a rule to follow. To write a
provider or helper type instead, read [extending-systemone.md](extending-systemone.md).

This file ships inside every SystemOne NuGet package at `docs/agents/`, so the copy under
`~/.nuget/packages/<package-id>/<version>/docs/agents/` matches the installed version.

## Read this first

1. SystemOne calls **decision models**: they answer named questions about a state with
   probabilities and never generate text.
2. Four question types: `noul` (yes/no probability), `choice` (one of 1–255 named options), `score`
   (an ordered scale of 1–10 levels), `box` (a region of an attached image; TypeSafe `jev-preview`
   only).
3. **No namespace.** Every type is in the global namespace. `SystemOne` is a static class. Never
   write `using SystemOne;` (compile error CS0138).
4. Create everything from four mount points: `SystemOne.Providers`, `SystemOne.Requests`,
   `SystemOne.ModelSources`, `SystemOne.Factory`. Never `new` a request, question, answer,
   provider or response.
5. A request chain starts from `SystemOne.Requests` with its first question and ends with
   `.For(state)`. Without `.For` it is a reusable template that `DecideAsync` refuses. Requests
   and mocks are **immutable**: every chain method returns a new instance; reassign in loops.
6. Read answers with `GetNoul`, `GetChoice`, `GetScore`, `GetBox`. Do not use `Get<T>`.
7. Answer properties: `NoulAnswer.Probability`, `ChoiceAnswer.Option`, `ScoreAnswer.Level`,
   `BoxAnswer.Bounds`. There is no `.Noul`, `.Choice`, `.Score` or `.Box` property on answers.
8. `ScoreAnswer.Level` is a 0-based **fractional expected index**, not a label.
9. All server, transport, timeout and parse failures are `SystemOneException`. Invalid requests
   are `ArgumentException`. The caller's cancellation is `OperationCanceledException`.
10. Hosted providers send data to third parties. Never send personal data to a provider whose
    `Locality` is `Hosted`.

## Minimal program

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

## Packages

| Package | Contains | Depends on |
|---|---|---|
| `SystemOne.Abstractions` | `ISystemOne`, `SystemOneProvider`, `SystemOneException`, all request/answer types, the four mount points, `Factory`, `ModelSources.FromDirectory` | — |
| `SystemOne` | `HttpSystemOneProvider`, `SystemOneEndpoint`, `FallbackSystemOneProvider`, `UseSelfHosted`, `UseFallback` | Abstractions |
| `SystemOne.Providers.Clef` | `UseClef`, `ClefProviders.ClefFlashModel` | SystemOne |
| `SystemOne.Providers.TypeSafe` | `UseTypeSafeAi`, `TypeSafeSystemOneProvider` | SystemOne |
| `SystemOne.Providers.Cloudflare` | `UseCloudflareWorkersAi`, `CloudflareSystemOneProvider` | SystemOne.Providers.Clef |
| `SystemOne.Providers.OpenRouter` | `UseOpenRouter` | SystemOne |
| `SystemOne.Providers.Vercel` | `UseVercelAiGateway` | SystemOne |
| `SystemOne.Testing` | `UseMock`, `MockSystemOneProvider` | Abstractions |
| `SystemOne.ModelSources.HuggingFace` | `ModelSources.FromHuggingFace`, `HuggingFaceModelSource` | Abstractions |

A `Use*` method only exists after its package is referenced. If `UseClef` does not resolve, add
`SystemOne.Providers.Clef`.

## Providers

All return `ISystemOne`, except `UseMock`, which returns `MockSystemOneProvider`.

| Call | Target | Locality | Model |
|---|---|---|---|
| `UseClef(Uri baseUri, HttpClient? http = null)` | self-hosted clef server at `{baseUri}/v1/systemone` | SelfHosted | `clef-flash` |
| `UseSelfHosted(Uri baseUri, string model, HttpClient? http = null)` | any self-hosted `/v1/systemone` (Kev, laya-serve, d3-mlx, Ollama) | SelfHosted | `model` |
| `UseTypeSafeAi(string apiKey, string model = "jev-latest", HttpClient? http = null)` | `https://api.typesafe.ai/v1/systemone` | Hosted | `model` |
| `UseCloudflareWorkersAi(string accountId, string apiToken, string model = "clef-flash", HttpClient? http = null)` | Workers AI | Hosted | `model` |
| `UseOpenRouter(string apiKey, string model, HttpClient? http = null)` | `https://openrouter.ai/api/v1/systemone` | Hosted | e.g. `cloudflare/clef-flash` |
| `UseVercelAiGateway(string apiKey, string model, HttpClient? http = null)` | `https://ai-gateway.vercel.sh/typesafe/v1/systemone` | Hosted | e.g. `typesafe-ai/jev` |
| `UseFallback(params ISystemOne[] providers)` | first provider that does not throw `SystemOneException` | of each provider | of each provider |
| `UseMock()`, `UseMock(Exception)`, `UseMock(Func<Request, Response>)` | in memory | — | `mock` |

- `UseClef` and `UseSelfHosted` replace any path in `baseUri` with `/v1/systemone`.
- `http` defaults to `SystemOneProvider.DefaultHttpClient` (shared, 60 s timeout, connections
  recycled every 2 minutes).
- With `IHttpClientFactory`, register one named client per service, named after it and with a
  timeout; register the provider as **transient** and pass `factory.CreateClient(name)`; do not
  hold a factory client in a singleton.
- `UseFallback`: moves on only for `SystemOneException`; stops on any other exception; if all fail,
  throws one `SystemOneException` listing every failure (`InnerException` = last). Order decides
  where data goes: self-hosted first; omit hosted providers for personal data.

## Request chain

Start: any method below except `.For`, called on `SystemOne.Requests` (for example
`SystemOne.Requests.Noul(...)`). End: `.For(object state)` or `.For(JsonNode state)`; it replaces any
earlier state. A string becomes a JSON string; an object is serialized with default
`System.Text.Json` options (a POCO keeps PascalCase names). Every method below is an extension on
`SystemOne.Request` and returns a **new** request.

| Method | Adds | Notes |
|---|---|---|
| `.Noul(string name, string instructions)` | yes/no question | |
| `.Choice(string name, string? instructions, Func<SystemOneChoiceOptions, SystemOneChoiceOptions> options)` | choice with descriptions | inside: `options => options.Option("a", "desc").Option("b")`; names non-blank, unique (ordinal) |
| `.Choice(string name, string? instructions, params string[] options)` | choice, names only | |
| `.Score(string name, string? instructions, params string[] levels)` | ordered scale, lowest first | 1–10 levels |
| `.Box(string name, string? instructions)` | bounding-box question | needs an image; TypeSafe `jev-preview` only |
| `.Image(string name, string path)` | image read from a file | format from the extension, lower-cased |
| `.Image(string name, ReadOnlyMemory<byte> bytes, string format)` | image from bytes | `png`, `jpg`, `jpeg`, `webp` |
| `.WithModel(string model)` | — | overrides the provider's default model |
| `.For(object state)`, `.For(JsonNode state)` | the state | last in the chain; copies the state; null throws `ArgumentNullException` |

- A question or image with an existing name **replaces** the old one.
- Option and level names may be any language (Chinese works); they come back unchanged.
- The image name is a placeholder; TypeSafe substitutes the image wherever the name appears in
  the state or instructions.
- Validation in `DecideAsync`, before sending, as `ArgumentException`: a state; ≥ 1 question; choice
  1–255 options; score 1–10 levels; every image has bytes and a format; the request or provider
  has a model. Blank names fail immediately in the chain method.
- Provider limits **not** checked by the library: TypeSafe images ≥ 512 px (png/jpg/jpeg only);
  Cloudflare ≤ 4 images.

## Response

| Member | Type | Meaning |
|---|---|---|
| `response.Model` | `string` | model that answered (with `UseFallback`: tells which provider) |
| `response.Answers` | `IReadOnlyDictionary<string, SystemOne.Answer>` | every answer by question name; never null |
| `response.Usage.InputTokens` / `.OutputTokens` | `long` | 0 when the server does not report usage; `Usage` is never null |
| `response.GetNoul(name)` | `SystemOne.NoulAnswer` | `.Probability` (0–1) |
| `response.GetChoice(name)` | `SystemOne.ChoiceAnswer` | `.Option` (string), `.Confidence` (probability of `.Option`), `.Probabilities` (option → probability) |
| `response.GetScore(name)` | `SystemOne.ScoreAnswer` | `.Level` (expected 0-based index, fractional), `.Confidence` (probability of the top level), `.Legend` (`"0"`… → level name), `.Probabilities` (`"0"`… → probability) |
| `response.GetBox(name)` | `SystemOne.BoxAnswer` | `.Bounds.Top/.Bottom/.Left/.Right` (0–1 fractions of height/width), `.Confidence` (`double?`) |

`Get*` throws `SystemOneException` when the name is missing (message lists available names) or the
answer has another type. `Confidence` is not the probability that the answer is correct.

Score level to label:

```csharp
var score = response.GetScore("urgency");
var label = score.Legend[((int)Math.Round(score.Level)).ToString(CultureInfo.InvariantCulture)];
```

## Exceptions

| Exception | Raised by | Cause |
|---|---|---|
| `ArgumentNullException` | chain methods, `DecideAsync` | null argument |
| `ArgumentException` | chain methods, `DecideAsync`, `Use*`, `FromHuggingFace` | invalid request (see validation); no model on request or endpoint; blank API key or account ID; repository not `name` or `owner/name`; revision or subfolder that would leave the cache |
| `SystemOneException` | `DecideAsync` | non-2xx response; unreachable server; `HttpClient.Timeout`; unparsable body; Cloudflare `success:false`; all fallback providers failed |
| `SystemOneException` | `Get*` | missing answer name, or wrong answer type |
| `OperationCanceledException` | `DecideAsync` | the caller's token was cancelled (never wrapped) |
| `InvalidOperationException` | mock `DecideAsync` | a configured mock answer does not fit the request |
| `InvalidOperationException` | mock `.Noul`/`.Choice`/`.Score`/`.Box`/`.WithModel` | called on a throwing or handler mock |

`SystemOneException` has `StatusCode` (`HttpStatusCode?`) and `ResponseBody` (`string?`). Its
message is extracted from the error formats of clef-mlx, Ollama, llama-server, OpenRouter, Vercel,
TypeSafe and Cloudflare, or is the raw body.

## Mock (`SystemOne.Testing`)

| Call | Result |
|---|---|
| `SystemOne.Providers.UseMock()` | default answers for every question |
| `.Noul(name, probability)` | that noul answers `probability` (0–1, checked immediately) |
| `.Choice(name, option)` | that choice answers `option`; probability 1 for it, 0 for the others |
| `.Score(name, level)` | that score answers `level`; nearest level probability 1; legend from the question |
| `.Box(name, top, bottom, left, right, confidence = null)` | that box answers these fractions (checked immediately) |
| `.WithModel(model)` | `Response.Model`; a request's own model wins |
| `SystemOne.Providers.UseMock(exception)` | every call throws `exception` |
| `SystemOne.Providers.UseMock(request => response)` | every call returns your function's result |
| `mock.Requests` | requests received by **this** instance, in order |

- Every configuration method returns a **new** mock; the original is unchanged.
- Defaults: noul 0.5; choice first option, probabilities equal; score middle level
  (`Count / 2`), probabilities equal; box whole image; model `mock`.
- A configured answer that does not fit the request (other question type, unknown option, score
  above the top level) throws `InvalidOperationException` from `DecideAsync`.
- Build responses for handler mocks with `SystemOne.Factory.Response(model, (name, answer), …)` and
  answers with `SystemOne.Factory.Noul/Choice/Box`.

## Model sources

| Call | Package | `ResolveAsync` returns |
|---|---|---|
| `SystemOne.ModelSources.FromDirectory(string path)` | Abstractions | `path`; `DirectoryNotFoundException` if missing |
| `SystemOne.ModelSources.FromHuggingFace(string repository, string? subfolder = null, string revision = "main", string? cacheDirectory = null, HttpClient? http = null)` | HuggingFace | a cache directory with verified files |

`ResolveAsync(IProgress<SystemOne.ModelDownloadProgress>? progress = null, CancellationToken
cancellationToken = default)`. `FromHuggingFace` goes online only inside `ResolveAsync`; cache
path `{cacheDirectory}/{repository with '/'→'_'}/{revision}/{subfolder}`, default
`cacheDirectory` = `LocalApplicationData/systemone/models`; LFS files are SHA-256 verified, small
files size-checked; a manifest makes later calls offline. Failures: `SystemOneException` (remote,
hash), `IOException` (disk).

## Rules

Do:

- Start from `SystemOne.Requests` with the first question and end the chain with `.For(state)`.
- Keep one provider instance per configuration and reuse it; it is safe for concurrent calls.
- Pass a `CancellationToken` to `DecideAsync`.
- Catch `SystemOneException` for service failures; inspect `StatusCode` and `ResponseBody`.
- Use `UseFallback(selfHosted, hosted)` for availability, unless the data is personal.
- Use `UseMock()` in unit tests; never call real endpoints from unit tests.

Don't:

- `using SystemOne;` or any `SystemOne.*` namespace import: there is none.
- `new SystemOne.Request { … }`, `new SystemOne.NoulQuestion { … }`, `new SystemOne.NoulAnswer { … }`,
  `new HttpSystemOneProvider(…)` in consumer code.
- `request with { Questions = … }` to add questions, or `with { State = … }` to set the state;
  use the chain and `.For(state)`.
- Call a chain method and ignore its return value (`request.Noul(...)` alone does nothing).
- Treat `ScoreAnswer.Level` as an integer or a label.
- Treat `Confidence` as accuracy.
- Send `box` questions to providers other than TypeSafe `jev-preview`.
- Catch `Exception` around `DecideAsync` in a way that swallows `OperationCanceledException`.

## Recipes

Dynamic questions:

```csharp
var template = SystemOne.Requests.Noul("escalate", "Should a person review this?");

foreach (var flag in flags)
{
    template = template.Noul(flag.Name, flag.Question);
}

var request = template.For(signals);
```

Image and box:

```csharp
var typeSafe = SystemOne.Providers.UseTypeSafeAi(apiKey);
var request = SystemOne.Requests
    .Image("photo", "id-card.png")
    .Box("face", "the face")
    .WithModel("jev-preview")
    .For("Locate the face on photo");
var bounds = (await typeSafe.DecideAsync(request, cancellationToken)).GetBox("face").Bounds;
```

Self-hosted first, hosted as backup (non-personal data only):

```csharp
var systemOne = SystemOne.Providers.UseFallback(
    SystemOne.Providers.UseClef(new Uri("http://localhost:8000")),
    SystemOne.Providers.UseTypeSafeAi(apiKey));
```

Dependency injection with `IHttpClientFactory`:

```csharp
// one named client per service, named after it, with its timeout
const string CLEF_HTTP_CLIENT = "clef";

services.AddHttpClient(CLEF_HTTP_CLIENT, client => client.Timeout = TimeSpan.FromSeconds(10));

services.AddTransient<ISystemOne>(provider => SystemOne.Providers.UseClef(
    baseUri: new Uri("http://localhost:8000"),
    http: provider.GetRequiredService<IHttpClientFactory>().CreateClient(CLEF_HTTP_CLIENT)));
```

Unit test with a mock:

```csharp
var mock = SystemOne.Providers
    .UseMock()
    .Noul("refund", 0.95)
    .Choice("department", "billing");

var response = await mock.DecideAsync(request);

var sent = mock.Requests[0];
```

Generic handling of every answer:

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

## Wire format

What `HttpSystemOneProvider` sends to `POST /v1/systemone` for the minimal program above:

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

A typical response:

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

Images go in an `images` array of data URLs; TypeSafe receives them as `assets` with the header
`TypeSafe-Danger-Zone-Images: true`, and Cloudflare wraps the response in
`{"success", "result", "errors"}`. The library handles all of this.