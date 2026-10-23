# Using SystemOne

[繁體中文](usage.zh-TW.md)

This guide covers everything a consumer of the library needs: building requests, reading
answers, choosing providers, handling errors, testing, and loading local model weights. To add
your own provider or helper types, read [writing-providers.md](writing-providers.md) instead.

- [Concepts](#concepts)
- [Install](#install)
- [First call](#first-call)
- [Building requests](#building-requests)
- [Reading responses](#reading-responses)
- [Providers](#providers)
- [Errors and cancellation](#errors-and-cancellation)
- [Testing with the mock](#testing-with-the-mock)
- [Local model weights](#local-model-weights)

## Concepts

A System One decision model reads a **state** (any text or JSON) and answers **named questions**
with probabilities. It never generates text, so an answer is a number or a label, not prose.

| Question type | Ask | Answer |
|---|---|---|
| `noul` | a yes/no question | `Probability` of "yes", 0–1 |
| `choice` | pick one of 1–255 named options | the chosen `Option`, its `Confidence`, every option's probability |
| `score` | place the state on an ordered scale of 1–10 levels | the expected `Level` (0-based, fractional), `Confidence`, the level names |
| `box` | locate something in an attached image (TypeSafe `jev-preview` only) | `Bounds` as fractions of the image |

Three kinds of object are involved:

- **Request**: the state, the questions, and optionally a model name and images.
- **Provider** (`ISystemOne`): something that answers a request: a self-hosted server, a hosted
  service, a fallback chain, or a mock.
- **Response**: the model that answered, one answer per question name, and token usage.

Every type lives in the **global namespace**. `SystemOne` is a static class, not a namespace, so
you never write `using SystemOne;` (it does not compile). You start everything from one of four
mount points on it:

| Mount point | Creates | Example |
|---|---|---|
| `SystemOne.Providers` | providers | `SystemOne.Providers.UseClef(uri)` |
| `SystemOne.Requests` | requests | `SystemOne.Requests.Noul(...).For(state)` |
| `SystemOne.ModelSources` | local weight sources | `SystemOne.ModelSources.FromDirectory(path)` |
| `SystemOne.Factory` | answers, responses, images | `SystemOne.Factory.Noul(0.9)` |

Request and answer records have public setters only so they can be serialized. Do not build them
with `new`; use the mount points (see [ADR-0001](adr/0001-object-creation.md)).

## Install

Install only the packages you call. All packages share one version.

| Package | Install it when you | Gives you |
|---|---|---|
| `SystemOne` | call any self-hosted `/v1/systemone` server, or chain providers | `UseSelfHosted`, `UseFallback` |
| `SystemOne.Providers.Clef` | run clef (for example clef-mlx) yourself | `UseClef` |
| `SystemOne.Providers.TypeSafe` | call TypeSafe's hosted Jev | `UseTypeSafeAi` |
| `SystemOne.Providers.Cloudflare` | call clef on Cloudflare Workers AI | `UseCloudflareWorkersAi` |
| `SystemOne.Providers.OpenRouter` | call any model through OpenRouter | `UseOpenRouter` |
| `SystemOne.Providers.Vercel` | call through Vercel AI Gateway | `UseVercelAiGateway` |
| `SystemOne.Testing` | write tests or work offline | `UseMock` |
| `SystemOne.ModelSources.HuggingFace` | download model weights for an in-process provider | `FromHuggingFace` |
| `SystemOne.Abstractions` | only reference the contracts (it comes with every package above) | types only |

```bash
dotnet add package SystemOne.Providers.Clef
dotnet add package SystemOne.Testing
```

## First call

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

Switching to another provider changes only the first line. The rest of the code is identical for
every provider.

## Building requests

### State

A chain starts from `SystemOne.Requests` with its first question and ends with `.For(...)`, which
adds the state: what the questions are about. Until then the request is a template, and
`DecideAsync` refuses it:

```csharp
var questions = SystemOne.Requests.Noul("refund", "Does the customer ask for a refund?");

// an anonymous object, POCO or dictionary is serialized with System.Text.Json
var fromObject = questions.For(new { subject = "Refund", body = "Charged twice" });

// plain text
var fromText = questions.For("Customer says the parcel never arrived.");

// a JsonNode you already have (JsonObject, JsonArray, JsonValue)
var fromJson = questions.For(new JsonObject { ["amount"] = 12000 });
```

A POCO keeps its C# property names (for example `Amount`), because `For(object)` uses the default
serializer options. Pass a `JsonObject` or an anonymous object if you need exact field names. The
model reads names and values, so choose descriptive names.

### Questions

Each question has a **name** (the key you read the answer by) and optional **instructions** for
the model:

```csharp
var request = SystemOne.Requests
    // yes/no
    .Noul("refund", "Does the customer explicitly ask for a refund?")
    // one of several options, each with a description for the model
    .Choice("department", "Which department should handle this?", options => options
        .Option("billing", "invoices, payments, refunds")
        .Option("technical", "bugs, outages")
        .Option("sales"))
    // option names only
    .Choice("language", "Language of the message", "zh-TW", "en", "ja")
    // an ordered scale, lowest first
    .Score("urgency", "How urgent is this?", "not urgent", "soon", "blocking")
    .For(ticket);
```

- Option and level names may be in any language, including Chinese. They come back unchanged as
  answer keys and labels.
- Option descriptions are optional (`.Option("sales")`).
- Adding a question with a name that is already used replaces the earlier question.
- `.Box(name, instructions)` asks for a bounding box in an attached image. Only TypeSafe's
  `jev-preview` model answers it.

### Images

```csharp
var request = SystemOne.Requests
    // reads the file; the format comes from the extension
    .Image("photo", "id.png")
    // or bytes plus a format: png, jpg, jpeg or webp
    .Image("thumbnail", bytes, "png")
    .Box("face", "the face on the ID card")
    .WithModel("jev-preview")
    .For("photo");
```

The image name is a placeholder. TypeSafe replaces every occurrence of that string in the state
or instructions with the image; other providers send the images alongside the state. Adding an
image with an existing name replaces it. Only bytes are sent; remote URLs are never passed.

Provider limits that the library does not check for you:

- TypeSafe rejects images smaller than 512 px and accepts png, jpg and jpeg.
- Cloudflare Workers AI accepts at most 4 images.

### Model

The provider's default model is used unless the request names one:

```csharp
var preview = request.WithModel("jev-preview");
```

### Requests are immutable

Every chain method returns a **new** request and leaves the original unchanged. To add questions
in a loop, start the chain with a fixed question and reassign:

```csharp
var template = SystemOne.Requests.Noul("escalate", "Should a person review this?");

foreach (var flag in flags)
{
    template = template.Noul(flag.Name, flag.Question);
}

var request = template.For(signals);
```

A chain without `.For(...)` is a template: keep it and call `.For(state)` once per state; the
template itself never changes. To share a template, put it on the `SystemOne.Requests` mount point
and call it like a built-in method, for example `SystemOne.Requests.Moderation(message)`. See
[Request templates](writing-providers.md#request-templates).

### Validation

Requests are checked before anything is sent. A violation throws `ArgumentException`:

| Rule | Checked when |
|---|---|
| question, option and image names are not blank | the chain method is called |
| the state was added with `.For(...)` | `DecideAsync` is called |
| option names within one question are unique | `.Option(...)` is called |
| at least one question | `DecideAsync` is called |
| a choice has 1–255 options | `DecideAsync` is called |
| a score has 1–10 levels | `DecideAsync` is called |
| every image has bytes and a format | `DecideAsync` is called |
| the request or the provider names a model (HTTP providers) | `DecideAsync` is called |

## Reading responses

```csharp
var response = await systemOne.DecideAsync(request);

var refund = response.GetNoul("refund");
var department = response.GetChoice("department");
var urgency = response.GetScore("urgency");
```

| Method | Returns | Properties |
|---|---|---|
| `GetNoul(name)` | `SystemOne.NoulAnswer` | `Probability`: probability of "yes", 0–1 |
| `GetChoice(name)` | `SystemOne.ChoiceAnswer` | `Option`: chosen option name; `Confidence`: its probability; `Probabilities`: option name → probability |
| `GetScore(name)` | `SystemOne.ScoreAnswer` | `Level`: expected level index, 0-based and fractional; `Confidence`: probability of the most likely level; `Legend`: `"0"`, `"1"`, … → level name; `Probabilities`: `"0"`, `"1"`, … → probability |
| `GetBox(name)` | `SystemOne.BoxAnswer` | `Bounds.Top`, `Bounds.Bottom`, `Bounds.Left`, `Bounds.Right`: fractions of the image height and width; `Confidence` (may be null) |

Each `Get*` method throws `SystemOneException` if there is no answer with that name, or if the
answer is a different type. The message lists the names that are available.

A score `Level` is a weighted average, not a label. To show the nearest level name:

```csharp
var urgency = response.GetScore("urgency");
var nearest = (int)Math.Round(urgency.Level);
var label = urgency.Legend[nearest.ToString(CultureInfo.InvariantCulture)];
```

`Confidence` on a choice or score is the probability of the top option or level. It is not the
probability that the answer is correct.

The rest of the response:

- `response.Model`: the model that actually answered. With a fallback chain this tells you which
  provider answered.
- `response.Usage.InputTokens` and `response.Usage.OutputTokens`: token counts. They are 0 when the
  server does not report them; `Usage` is never null.
- `response.Answers`: every answer by name, for generic handling:

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

## Providers

Every provider is created by one `Use*` call and returns `ISystemOne`.

| Call | Answers with | Data leaves your network | Package |
|---|---|---|---|
| `UseClef(baseUri)` | your own clef server, model `clef-flash` | no | `SystemOne.Providers.Clef` |
| `UseSelfHosted(baseUri, model)` | any self-hosted `/v1/systemone` server: Kev, laya-serve, d3-mlx, Ollama | no | `SystemOne` |
| `UseTypeSafeAi(apiKey, model = "jev-latest")` | TypeSafe's hosted Jev | yes | `SystemOne.Providers.TypeSafe` |
| `UseCloudflareWorkersAi(accountId, apiToken, model = "clef-flash")` | clef on Cloudflare Workers AI | yes | `SystemOne.Providers.Cloudflare` |
| `UseOpenRouter(apiKey, model)` | any OpenRouter model, for example `cloudflare/clef-flash` | yes | `SystemOne.Providers.OpenRouter` |
| `UseVercelAiGateway(apiKey, model)` | Vercel AI Gateway, for example `typesafe-ai/jev` | yes | `SystemOne.Providers.Vercel` |
| `UseFallback(first, second, …)` | the first provider that succeeds | depends on the chain | `SystemOne` |
| `UseMock()` | fixed answers, no network | no | `SystemOne.Testing` |

`UseSelfHosted` and `UseClef` replace any path in `baseUri` with `/v1/systemone`.

### HttpClient

Every HTTP `Use*` takes an optional `HttpClient` as its last argument. Without one, all providers
share `SystemOneProvider.DefaultHttpClient`: a 60-second timeout and pooled connections that
are recycled every 2 minutes so DNS changes are picked up.

Pass your own client for a proxy, custom handlers or a different timeout. With
`IHttpClientFactory`, register one named client per service with its timeout, and create the
provider per use rather than as a singleton, so the factory can rotate handlers. Providers are
cheap to create:

```csharp
// one named client per service, named after it, with its timeout
const string CLEF_HTTP_CLIENT = "clef";

services.AddHttpClient(CLEF_HTTP_CLIENT, client => client.Timeout = TimeSpan.FromSeconds(10));

services.AddTransient<ISystemOne>(provider => SystemOne.Providers.UseClef(
    baseUri: new Uri("http://localhost:8000"),
    http: provider.GetRequiredService<IHttpClientFactory>().CreateClient(CLEF_HTTP_CLIENT)));
```

### Fallback chains

`UseFallback` tries each provider in order:

```csharp
var systemOne = SystemOne.Providers.UseFallback(
    SystemOne.Providers.UseClef(new Uri("http://localhost:8000")),
    SystemOne.Providers.UseTypeSafeAi(apiKey));
```

- A `SystemOneException` (server error, unreachable server, timeout, unparsable response) moves
  to the next provider.
- Any other exception stops the chain at once: an invalid request (`ArgumentException`), your own
  cancellation (`OperationCanceledException`), a mock configuration error.
- If every provider fails, the chain throws one `SystemOneException` that lists each failure;
  `InnerException` is the last one.

**Order decides where your data can go.** Put self-hosted providers first, and leave hosted
providers out of the chain entirely when the state contains personal data.

## Errors and cancellation

| Exception | When | What to do |
|---|---|---|
| `ArgumentException` (and `ArgumentNullException`) | the request is invalid, before anything is sent; or a `Use*` or `FromHuggingFace` argument is invalid (a blank API key, a repository that is not `name` or `owner/name`) | fix the request or the argument |
| `SystemOneException` | the server rejected the request; the response could not be parsed; the server was unreachable; `HttpClient.Timeout` expired; an answer is missing or has another type (`Get*`) | inspect `StatusCode`, `ResponseBody` and `InnerException`, or let a fallback chain move on |
| `OperationCanceledException` | your `CancellationToken` was cancelled | nothing; the call was cancelled on purpose |
| `InvalidOperationException` | a mock was configured with an answer that does not fit the request | fix the test setup |

```csharp
try
{
    var response = await systemOne.DecideAsync(request, cancellationToken);
}
catch (SystemOneException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized)
{
    // the server rejected the API key
    Console.WriteLine($"Check the API key: {exception.ResponseBody}");
}
```

The library extracts a readable message from the error formats of clef-mlx, Ollama, llama-server,
OpenRouter, Vercel, TypeSafe and Cloudflare; otherwise the message is the raw response body.

## Testing with the mock

`SystemOne.Testing` provides a provider that answers without a network and records every request.

```csharp
var happy = SystemOne.Providers
    .UseMock()
    .Noul("refund", 0.99)
    // the chosen option gets probability 1, the others 0
    .Choice("department", "billing")
    // the nearest level gets probability 1; legend follows the question's levels
    .Score("urgency", 2)
    // edges are fractions of the image height and width
    .Box("face", top: 0.1, bottom: 0.6, left: 0.2, right: 0.9);

var response = await happy.DecideAsync(request);
```

A mock never changes once it is built. Every configuration method returns a **new** mock, so one
baseline can produce variants:

```csharp
var calm = happy.Score("urgency", 0);
var named = happy.WithModel("fake-model");
```

Questions you do not configure get a default answer: noul 0.5; choice the first option with
probabilities split evenly; score the middle level with probabilities split evenly; box the whole
image. The response's model is `mock` unless you call `WithModel` or the request names one.

Two more kinds of mock cover failures and full control:

```csharp
// every call throws this exception
var down = SystemOne.Providers.UseMock(new SystemOneException("outage"));

// every call is answered by your function; here: succeed once, then fail
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

Per-question methods such as `.Noul(...)` throw `InvalidOperationException` on these two kinds,
because their answers would never be used.

`mock.Requests` lists, in order, the requests that this particular mock instance received. Read
it from the instance you gave to the code under test.

A configured answer that does not fit the request makes `DecideAsync` throw
`InvalidOperationException`: a different question type, an option that the question does not
have, or a score above the top level. A stale test setup therefore fails loudly instead of
returning an impossible answer.

## Local model weights

Providers that run a model inside your process need its weights on disk. A `SystemOne.ModelSource`
says where they come from; `ResolveAsync` returns the local directory:

```csharp
// already on disk, for example on an air-gapped machine
var local = SystemOne.ModelSources.FromDirectory(@"D:\models\laya");

// downloaded once from Hugging Face (package SystemOne.ModelSources.HuggingFace)
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

`FromDirectory` only checks that the directory exists (`DirectoryNotFoundException` otherwise).

`FromHuggingFace`:

- touches the network only inside `ResolveAsync`, never when it is created;
- rejects, with `ArgumentException`, a repository that is not `name` or `owner/name` and a revision
  or subfolder that is absolute or contains `..`, so the cache path cannot leave `cacheDirectory`;
- downloads into `{cacheDirectory}/{repository with '/' replaced by '_'}/{revision}/{subfolder}`.
  The default cache directory is `systemone/models` under the local application data folder
  (`%LOCALAPPDATA%` on Windows);
- verifies every large (LFS) file against the SHA-256 published by Hugging Face, and the size of
  every small file. A file is only moved into place after it passes, so the directory never holds
  half-written files;
- writes a manifest when the download is complete. Later calls return the directory without any
  network access as long as every listed file is present with the right size;
- reports failures (listing, download, hash mismatch) as `SystemOneException` and disk failures as
  `IOException`.

Pass `revision:` to pin a branch, tag or commit, `cacheDirectory:` to choose another cache folder,
and `http:` for your own `HttpClient`.