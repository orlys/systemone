# SystemOne: agent guide for provider and extension authors

[繁體中文](extending-systemone.zh-TW.md) · Human guide: [writing-providers.md](../writing-providers.md)

Purpose: let a coding agent add a provider, request template, factory helper or weight source to
SystemOne correctly on the first try. Every statement is a fact about the current version or a
rule. Consumer-side usage is in [using-systemone.md](using-systemone.md); read it first if you
have not.

This file ships inside every SystemOne NuGet package at `docs/agents/`.

## Decide what to write

| Situation | Write | Reference |
|---|---|---|
| Service speaks standard `/v1/systemone` + Bearer key + `images` data URLs | a `Use*` extension method that returns `new HttpSystemOneProvider(http, endpoint)` | `SystemOne` |
| Service differs in request shape, headers or response envelope | `sealed class XSystemOneProvider : HttpSystemOneProvider`, override `PrepareRequest` and/or `ReadResponse`, plus a `Use*` method | `SystemOne` |
| In-process model, non-HTTP transport, anything else | `sealed class XSystemOneProvider : SystemOneProvider`, implement `DecideCoreAsync`, plus a `Use*` method | `SystemOne.Abstractions` |
| Reusable question set | `static` extension method on `SystemOneRequests` | `SystemOne.Abstractions` |
| Answer-building helper | extension method on `SystemOneFactory` | `SystemOne.Abstractions` |
| New place to load weights from | `sealed class X : SystemOne.ModelSource`, plus `From*` extension method on `SystemOneModelSources` | `SystemOne.Abstractions` |

Never implement `ISystemOne` directly; derive from `SystemOneProvider` so validation and fallback
behavior are shared.

## Types you build on

All types are in the global namespace.

| Type | Kind | Members that matter |
|---|---|---|
| `ISystemOne` | interface | `Task<SystemOne.Response> DecideAsync(SystemOne.Request request, CancellationToken cancellationToken = default)` |
| `SystemOneProvider` | abstract class | public non-virtual `DecideAsync` (validates, then calls) `protected abstract Task<SystemOne.Response> DecideCoreAsync(SystemOne.Request request, CancellationToken cancellationToken)`; `static HttpClient DefaultHttpClient` (the default client, shared by every provider and weight source) |
| `HttpSystemOneProvider` | public, not sealed | `public HttpSystemOneProvider(HttpClient http, SystemOneEndpoint endpoint)`; `SystemOneEndpoint Endpoint`; `HttpClient HttpClient`; `protected virtual void PrepareRequest(SystemOne.Request request, JsonObject body, HttpRequestMessage message)`; `protected virtual SystemOne.Response ReadResponse(string body, HttpStatusCode statusCode)` |
| `SystemOneEndpoint` | sealed record | `required Uri Uri` (full URL with path); `required LocalityKind Locality` (`SelfHosted` / `Hosted`); `string? DefaultModel`; `string? BearerToken` (masked in `ToString()`) |
| `SystemOneException` | sealed exception | `SystemOneException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, Exception? innerException = null)`; `StatusCode`; `ResponseBody` |
| `SystemOne.Request` | sealed record | `string? Model`; `JsonNode State`; `IReadOnlyDictionary<string, SystemOne.Question> Questions`; `IReadOnlyDictionary<string, SystemOne.Image>? Images` (not serialized; providers decide the wire shape) |
| `SystemOne.Image` | sealed record | `ReadOnlyMemory<byte> Bytes`; `string Format` (`png`, `jpg`, `jpeg`, `webp`) |
| `SystemOne.NoulQuestion` | question | `Instructions` |
| `SystemOne.ChoiceQuestion` | question | `Instructions`; `IReadOnlyDictionary<string, string?> Criteria` (option → description, insertion order) |
| `SystemOne.ScoreQuestion` | question | `Instructions`; `IReadOnlyList<string> Criteria` (levels, lowest first) |
| `SystemOne.BoxQuestion` | question | `Instructions` |
| `SystemOne.NoulAnswer` | answer | `double Probability` (JSON `noul`) |
| `SystemOne.ChoiceAnswer` | answer | `string Option` (JSON `choice`); `double Confidence`; `IReadOnlyDictionary<string, double> Probabilities` |
| `SystemOne.ScoreAnswer` | answer | `double Level` (JSON `score`); `double Confidence`; `IReadOnlyDictionary<string, string> Legend`; `IReadOnlyDictionary<string, double> Probabilities` |
| `SystemOne.BoxAnswer` | answer | `BoxCoordinates Bounds` (JSON `box`: `Top`, `Bottom`, `Left`, `Right`); `double? Confidence` |
| `SystemOne.Response` | sealed record | `required string Model`; `required IReadOnlyDictionary<string, Answer> Answers` (null → empty); `Usage Usage` (null → zeros) |
| `SystemOne.Usage` | sealed record | `long InputTokens` (JSON `input_tokens`); `long OutputTokens` |
| `SystemOne.ModelSource` | abstract class | `public abstract Task<string> ResolveAsync(IProgress<SystemOne.ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)` |
| `SystemOne.ModelDownloadProgress` | sealed record | `required string File`; `long BytesReceived`; `long TotalBytes` (0 = unknown) |
| `SystemOneProviders`, `SystemOneRequests`, `SystemOneModelSources`, `SystemOneFactory` | sealed classes, internal constructors | types of the mount points; extend them with `this` extension methods |

`Question` and `Answer` have `private protected` constructors: the four question and answer types
are a closed set. You cannot add a question type.

## Provider contract

`SystemOneProvider.DecideAsync` guarantees before calling `DecideCoreAsync`: request not null;
`State` not null; ≥ 1 question; each choice 1–255 options; each score 1–10 levels; each image
has bytes and a format; token not yet cancelled.

`DecideCoreAsync` MUST:

1. Return one answer per entry in `request.Questions`, same key, matching type
   (`Noul→NoulAnswer`, `Choice→ChoiceAnswer`, `Score→ScoreAnswer`, `Box→BoxAnswer`).
2. For a choice, set `Option` to a key of `ChoiceQuestion.Criteria` and key `Probabilities` by
   the same option names.
3. Use `request.Model ?? defaultModel`; set `Response.Model` to the model that answered.
4. Throw `SystemOneException` for every service, transport, timeout or parse failure. This is the
   only exception that makes `UseFallback` try the next provider.
5. Throw `ArgumentException` for an unsupported question type or another problem in the request.
6. Let `OperationCanceledException` from the caller's token propagate unwrapped.
7. Be safe for concurrent calls on one instance; never mutate the request.

## HttpSystemOneProvider pipeline

`DecideCoreAsync` of `HttpSystemOneProvider`, in order:

1. `model = request.Model ?? Endpoint.DefaultModel`; neither → `ArgumentException`.
2. Create `POST Endpoint.Uri`; if `BearerToken` is set, add `Authorization: Bearer <token>`.
3. Serialize `request with { Model = model }` to a `JsonObject`: `model`, `state`, `questions`
   (images excluded).
4. Call `PrepareRequest(request, body, message)`. Default: if `request.Images` is non-empty, set
   `body["images"]` to an array of `data:image/<format>;base64,…` (`jpg` → `jpeg`).
5. Send. `HttpRequestException` → `SystemOneException` (inner = original). `HttpClient.Timeout`
   (cancellation while the caller's token is not cancelled) → `SystemOneException`. Caller
   cancellation → `OperationCanceledException`.
6. Non-2xx → `SystemOneException(message, statusCode, body)`. The message is taken, in order, from
   `{"error":"…"}`, `{"error":{"message"}}`, `{"message"}`, `{"detail":{"message"}}`,
   `{"detail":[{"msg"}]}`, `{"errors":[{"message"}]}`, else the raw body.
7. 2xx → `ReadResponse(body, statusCode)`. Default deserializes the standard response; an empty
   body, a null `model` or a null answer → `SystemOneException`. Any other exception thrown from
   `ReadResponse` is wrapped in `SystemOneException`.

Hook rules:

- `PrepareRequest` overrides replace the default; call `base.PrepareRequest(...)` to keep
  `images`.
- In `ReadResponse`, unwrap any envelope and pass the inner standard JSON to
  `base.ReadResponse(inner, statusCode)`. The library's source-generated JSON context is internal.
- Constructor: `internal`, signature `(HttpClient http, …)`, calling
  `base(http, CreateEndpoint(…))`.

## Skeletons

Plain `/v1/systemone` service:

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

HTTP service with an envelope:

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
            // 2xx without the envelope is a server fault
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

Any other provider:

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
                // this provider only answers yes/no questions
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

In-process models additionally:

- take a `SystemOne.ModelSource`, `await source.ResolveAsync(...)`, then load from the directory;
- return `Task<TProvider>` from a `Use*Async` method because loading is slow;
- implement `IDisposable` when holding native resources and return the concrete type so the
  caller can dispose it;
- run CPU-bound inference with `Task.Run` and check the token between steps.

## Building answers

| Need | Use | Throws |
|---|---|---|
| noul | `SystemOne.Factory.Noul(probability)` | `ArgumentOutOfRangeException` outside 0–1 or NaN |
| choice | `SystemOne.Factory.Choice((option, probability), …)`; picks the max (first on ties) as `Option` and `Confidence`; keeps input order | `ArgumentException` (empty, duplicate), `ArgumentOutOfRangeException` |
| box | `SystemOne.Factory.Box(top, bottom, left, right, confidence = null)` | `ArgumentOutOfRangeException` outside 0–1; `ArgumentException` if top > bottom or left > right |
| response | `SystemOne.Factory.Response(model, (name, answer), …)`; usage zero | `ArgumentException` on duplicate names |
| image | `SystemOne.Factory.Image(bytes, format)`, `SystemOne.Factory.Image(path)` | `ArgumentException`, `IOException` |
| score | no factory: `new SystemOne.ScoreAnswer { … }` | — |
| usage | `response with { Usage = new SystemOne.Usage { InputTokens = n } }` | — |

`ScoreAnswer` rules: `Level = Σ i·p(i)` (0-based); `Confidence = max p(i)`; `Legend` and
`Probabilities` keyed by `i.ToString(CultureInfo.InvariantCulture)`; `Legend[i]` = the question's
`Criteria[i]`.

## Request templates

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

Build the questions once in a `static readonly` field, without `.For(...)`. Each call returns
`template.For(state)`, a new request, so the shared template never changes. `.For` throws
`ArgumentNullException` for a null state.

## Weight sources

- Derive from `SystemOne.ModelSource`; constructor `internal`; expose
  `public static SystemOne.ModelSource FromX(this SystemOneModelSources anchor, …)`.
- `ResolveAsync`: network only inside it; return a directory with complete, verified files;
  download to a temporary name and move into place after verification; return without network
  when the files are already present; remote failure → `SystemOneException`; disk failure →
  `IOException`; let cancellation through; report `progress` when not null.

## Naming

| Item | Pattern |
|---|---|
| provider package | `SystemOne.Providers.<Name>` |
| weight source package | `SystemOne.ModelSources.<Name>` |
| provider | `<Name>SystemOneProvider`, `sealed`, `internal` constructor |
| entry class | `public static class <Name>Providers` |
| entry method | `Use<Name>(this SystemOneProviders anchor, …, HttpClient? http = null)` |
| weight source | `From<Name>(this SystemOneModelSources anchor, …)` |
| mount point parameter | always `anchor` |
| model constant | `public const string <Model>Model` on the entry class |
| add / replace / read | nouns add (`.Noul`, `.Image`); `With*` replaces (`.WithModel`); `Get*` reads |
| generic helper with named equivalents | `[EditorBrowsable(EditorBrowsableState.Never)]` |

Source: [ADR-0001](../adr/0001-object-creation.md).

## Testing

- HTTP providers: a stub `HttpMessageHandler` that records the request and returns fixture JSON;
  build the provider with `Use*(…, http: new HttpClient(stub))`.
- Assert: body and headers from your hooks; parsing of each question type; each error shape
  (`StatusCode`, `ResponseBody`); unsupported question → `ArgumentException`; cancellation →
  `OperationCanceledException`.
- Never call a real endpoint from unit tests. Gate live tests behind environment variables.
- Full example: [writing-providers.md#testing-a-provider](../writing-providers.md#testing-a-provider).

## Adding a package to this repository

Files to touch:

1. `src/SystemOne.Providers.<Name>/SystemOne.Providers.<Name>.csproj` (for a weight source,
   `SystemOne.ModelSources.<Name>` instead): copy
   `src/SystemOne.Providers.OpenRouter/SystemOne.Providers.OpenRouter.csproj`; set `PackageId`,
   `Description`, `PackageTags`; reference `../SystemOne/SystemOne.csproj` (HTTP) or
   `../SystemOne.Abstractions/SystemOne.Abstractions.csproj` (other). For tests of internals, add
   `<InternalsVisibleTo Include="SystemOne.Tests" />`.
2. `src/SystemOne.Providers.<Name>/<Name>Providers.cs` and, if needed, `<Name>SystemOneProvider.cs`.
3. `SystemOne.slnx`: add the project.
4. `tests/SystemOne.Tests/SystemOne.Tests.csproj`: add a project reference; add
   `<Name>SystemOneProviderTests.cs`.
5. Package tables in `README.md`, `README.zh-TW.md`, `docs/usage.md`, `docs/usage.zh-TW.md`,
   `docs/agents/using-systemone.md` and `docs/agents/using-systemone.zh-TW.md`.

CI packs every folder under `src/` with one version, so no workflow change is needed.

House rules for code in this repository: <https://github.com/orlys/rules>. The ones that bite most
often: XML doc comments in Traditional Chinese; braces on every block; no expression-bodied
members; comments on their own line above the code; named arguments when a call wraps; lines ≤ 120
characters (96 preferred); `sealed` classes.

Build and test:

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release
dotnet format --verify-no-changes
bash tools/check-docs.sh
```

## Pitfalls

- `SystemOne` is a class: no `using SystemOne;`.
- Forgetting `this SystemOneProviders anchor` makes the method invisible on `SystemOne.Providers`.
- A public provider constructor breaks the one-way-to-create rule; keep it `internal`.
- Throwing anything but `SystemOneException` for a service failure stops fallback chains.
- Wrapping the caller's `OperationCanceledException` in `SystemOneException` makes a fallback
  chain keep calling providers after cancellation.
- Returning a choice answer whose `Option` is not one of the question's options breaks consumers.
- Overriding `PrepareRequest` without `base.PrepareRequest` drops images.
- Not checking the API key: a missing environment variable sends the state to a third party
  unauthenticated, and the 401 quietly moves a fallback chain on.
- Setting headers on `HttpClient.DefaultRequestHeaders`: the client is shared; set them on
  `message` in `PrepareRequest`.