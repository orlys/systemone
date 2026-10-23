# Writing providers and extensions

[繁體中文](writing-providers.zh-TW.md)

This guide is for people who extend SystemOne: a new hosted service, a self-hosted server with a
twist, an in-process model, or helper types such as request templates and weight sources. To use
the library, read [usage.md](usage.md) first.

- [Choosing an extension point](#choosing-an-extension-point)
- [The provider contract](#the-provider-contract)
- [A standard /v1/systemone service](#a-standard-v1systemone-service)
- [An HTTP service with differences](#an-http-service-with-differences)
- [Any other provider](#any-other-provider)
- [Building answers](#building-answers)
- [Request templates](#request-templates)
- [Factory extensions](#factory-extensions)
- [Weight sources](#weight-sources)
- [Testing a provider](#testing-a-provider)
- [Naming and packaging](#naming-and-packaging)
- [Checklist](#checklist)

## Choosing an extension point

| You want to add | Write | Depend on |
|---|---|---|
| a service that speaks plain `/v1/systemone` with a Bearer key | one `Use*` method, no new class | `SystemOne` |
| a service that differs in images, headers or the response envelope | a subclass of `HttpSystemOneProvider` and a `Use*` method | `SystemOne` |
| an in-process model, a non-HTTP transport, or anything else | a subclass of `SystemOneProvider` and a `Use*` method | `SystemOne.Abstractions` |
| a reusable set of questions | an extension method on `SystemOneRequests` | `SystemOne.Abstractions` |
| a helper that builds answers | an extension method on `SystemOneFactory` | `SystemOne.Abstractions` |
| a new place to load model weights from | a subclass of `SystemOne.ModelSource` and a `From*` method | `SystemOne.Abstractions` |

Every extension is an extension method on one of the four mount points, so consumers find it by
typing `SystemOne.Providers.`, `SystemOne.Requests.`, `SystemOne.Factory.` or
`SystemOne.ModelSources.`.

## The provider contract

Consumers only see `ISystemOne`:

```csharp
public interface ISystemOne
{
    Task<SystemOne.Response> DecideAsync(
        SystemOne.Request request,
        CancellationToken cancellationToken = default);
}
```

Do not implement it directly. Derive from `SystemOneProvider`, which implements `DecideAsync` and
calls your `DecideCoreAsync` only after these checks pass:

- the request is not null;
- it has a state (`.For(...)` was called), so `request.State` is never null;
- it has at least one question;
- every choice has 1–255 options and every score 1–10 levels;
- every image has bytes and a format;
- the cancellation token is not already cancelled.

Your `DecideCoreAsync` must:

1. **Answer every question** in `request.Questions`, under the same name, with the matching answer
   type: `NoulQuestion` → `NoulAnswer`, `ChoiceQuestion` → `ChoiceAnswer` (an option from its
   `Criteria`), `ScoreQuestion` → `ScoreAnswer`, `BoxQuestion` → `BoxAnswer`.
2. **Use `request.Model`** when it is set, and your default model otherwise. Put the model that
   answered in `Response.Model`.
3. **Report failures as `SystemOneException`**, with `StatusCode` and `ResponseBody` when they
   come from HTTP. Fallback chains move to the next provider only on this exception. Wrap
   transport errors and your own timeouts in it too.
4. **Let the caller's cancellation through.** When `cancellationToken` is cancelled, throw (or let
   through) `OperationCanceledException`; do not wrap it.
5. **Throw `ArgumentException`** only for problems with the request itself, such as a question
   type you do not support. A fallback chain stops on it instead of trying the next provider.
6. **Be safe for concurrent calls.** Consumers usually keep one provider for the whole
   application and call it from many threads.
7. **Not change the request.** Requests are shared and treated as immutable.

## A standard /v1/systemone service

If the service accepts the standard request body, an `images` array of data URLs and a Bearer
key, you do not need a new class. Describe the endpoint and return an `HttpSystemOneProvider`:

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

`SystemOneEndpoint` fields:

| Field | Meaning |
|---|---|
| `Uri` | the full request URL, including the path |
| `Locality` | `SelfHosted` if data stays in the consumer's network, `Hosted` if it goes to a third party. The library only records it |
| `DefaultModel` | used when the request does not name a model. If both are missing, `DecideAsync` throws `ArgumentException` |
| `BearerToken` | when set, every request carries `Authorization: Bearer …`. `ToString()` masks it |

`HttpSystemOneProvider` already does the rest:

- picks the model, adds the Bearer header, and serializes `model`, `state` and `questions`;
- adds images as an `images` array of `data:image/…;base64,…` URLs;
- turns a non-2xx response into `SystemOneException`, extracting the message from the error
  formats of clef-mlx, Ollama, llama-server, OpenRouter, Vercel, TypeSafe and Cloudflare;
- wraps transport failures and `HttpClient.Timeout` in `SystemOneException`, and lets the caller's
  cancellation through;
- parses the response, wrapping malformed JSON in `SystemOneException`. A null model or a null
  answer is rejected the same way; a missing `usage` becomes zeros.

## An HTTP service with differences

When a service differs from the standard, derive from `HttpSystemOneProvider` and override one or
both hooks:

| Hook | Called | Default behavior |
|---|---|---|
| `PrepareRequest(request, body, message)` | before sending, after the body has `model`, `state` and `questions` and the message has the Bearer header | adds `images` as data URLs |
| `ReadResponse(body, statusCode)` | for a 2xx response | parses the standard response body |

Example: a service that wants images as `attachments`, requires a version header, and wraps its
response in `{"data": …}`:

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
        // the default would add "images"; this service wants base64 strings under "attachments"
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
            // a 2xx without the envelope is a server fault, not an empty answer
            throw new SystemOneException("Response has no data field.", statusCode, body);
        }

        // the inner object is a standard response, so the base class can parse it
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

- Overriding `PrepareRequest` replaces the default. Call `base.PrepareRequest(...)` if you still
  want the `images` array.
- In `ReadResponse`, any exception other than `SystemOneException` is wrapped in
  `SystemOneException` for you, so a fallback chain moves on. Throw `SystemOneException` yourself
  when you want to choose the message.
- `SystemOne.Providers.TypeSafe` (images as `assets` plus a header) and
  `SystemOne.Providers.Cloudflare` (a `{"success","result","errors"}` envelope) are real examples of
  these two hooks.

## Any other provider

For an in-process model, a gRPC service or anything that is not HTTP, derive from
`SystemOneProvider` and implement `DecideCoreAsync`. This toy provider answers from keywords, but
it shows every piece a real one needs:

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
        // stands in for the model: does the state mention the keyword?
        var state = request.State.ToJsonString();
        var hit = state.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

        // answer every question under its own name, with the matching answer type
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

        // the request's own model wins over this provider's default
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

For a model that runs in your process:

- **Take a `SystemOne.ModelSource`**, not a path, so consumers choose between
  `FromDirectory`, `FromHuggingFace` or their own source.
- **Load asynchronously.** Loading weights is slow, so return `Task<…>` from the `Use*` method and
  end its name in `Async` (`UseLocalModelAsync`).
- **Return the concrete type when it owns resources.** `ISystemOne` is not `IDisposable`; if your
  provider holds native sessions, implement `IDisposable` and return your type so the caller can
  dispose it.
- **Run CPU-bound inference off the caller's thread** (for example with `Task.Run`), and check
  `cancellationToken` between expensive steps.

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

        // load the model files from directory here
        return new LocalModelSystemOneProvider(directory);
    }

    public void Dispose()
    {
        // release native sessions here
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
        // run the model in _directory on the state and this question here
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

## Building answers

Build answers and responses with `SystemOne.Factory`, which checks ranges for you:

| Call | Builds | Checks |
|---|---|---|
| `Factory.Noul(probability)` | `NoulAnswer` | probability is 0–1 |
| `Factory.Choice((option, probability), …)` | `ChoiceAnswer` with the highest probability as `Option` and `Confidence` (ties keep the first) | at least one option, unique names, each probability 0–1 |
| `Factory.Box(top, bottom, left, right, confidence)` | `BoxAnswer` | every edge and the confidence are 0–1; top ≤ bottom and left ≤ right |
| `Factory.Response(model, (name, answer), …)` | `Response` with zero usage | unique names |
| `Factory.Image(bytes, format)`, `Factory.Image(path)` | `Image` | format is not blank; the path has an extension |

There is no factory for `ScoreAnswer` yet. Build it with the rules its properties document, or
write a factory extension like the one in the next section:

- `Level`: the expected level index, `Σ i × p(i)`, 0-based;
- `Confidence`: the probability of the most likely level;
- `Legend` and `Probabilities`: keyed by the index as an invariant-culture string, `"0"`, `"1"`, …

A `ChoiceAnswer` should list the question's options in `Probabilities`, using the same names, so
consumers can look them up.

`Response.Usage` defaults to zero. Set it when your model reports token counts:

```csharp
var response = SystemOne.Factory.Response("my-model", ("spam", SystemOne.Factory.Noul(0.8))) with
{
    Usage = new SystemOne.Usage { InputTokens = 120 },
};
```

## Request templates

A request template is a set of questions that many callers ask about different states. Build the
questions once and expose the template as an extension method on `SystemOneRequests`:

```csharp
public static class ModerationRequests
{
    // the questions are built once, as a template without state; each call adds its own
    private static readonly SystemOne.Request s_moderation = SystemOne.Requests
        .Noul("spam", "Is this message spam?")
        .Choice("topic", "Main topic", "billing", "technical", "other")
        .Score("toxicity", "How toxic is the message?", "none", "mild", "severe");

    public static SystemOne.Request Moderation(this SystemOneRequests anchor, string message)
    {
        return s_moderation.For(message);
    }
}

// usage: var request = SystemOne.Requests.Moderation("Buy cheap watches now!!!");
```

The template has no state, so each `.For(...)` returns a new request and the shared template never
changes. Callers can still chain more questions onto the returned request.

## Factory extensions

Add helpers for answers your provider builds often as extension methods on `SystemOneFactory`.
This one builds a `ScoreAnswer` from per-level probabilities:

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
            // no levels, or the two lists differ in length
            throw new ArgumentException("Need one probability per level.", nameof(probabilities));
        }

        // expected level Σ i × p(i), and the most likely level
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

        // key both dictionaries by the level index, as an invariant-culture string
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

Avoid names the library may add later (such as `Score`): an instance method on `SystemOneFactory`
would silently win over your extension.

## Weight sources

To load weights from somewhere new (an internal artifact store, S3, …), derive from
`SystemOne.ModelSource` and add a `From*` method on `SystemOneModelSources`:

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
            // already downloaded: no network
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
        // download into a temporary directory, verify every file, then move it to directory
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

`ResolveAsync` must:

- touch the network only inside `ResolveAsync`, never in the constructor or `From*` method;
- return a directory that holds the complete, verified weights, and never leave half-written files
  where the next call would trust them;
- return quickly without network access when the weights are already there;
- report remote failures as `SystemOneException`, disk failures as `IOException`, and let
  cancellation through as `OperationCanceledException`;
- report progress through `progress` when it is not null.

## Testing a provider

Test an HTTP provider with a stub `HttpMessageHandler`: it captures the outgoing request and
returns a fixture response, so no test touches the network.

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

Cover at least:

- the request body and headers your hooks produce;
- a successful response, including every question type you support;
- each error shape the service returns (status code and `ResponseBody` on `SystemOneException`);
- an unsupported question type (`ArgumentException`);
- cancellation (`OperationCanceledException`, not `SystemOneException`).

## Naming and packaging

Follow the names the built-in packages use, so consumers can guess them:

| Item | Pattern | Example |
|---|---|---|
| provider package | `SystemOne.Providers.<Name>` | `SystemOne.Providers.Acme` |
| weight source package | `SystemOne.ModelSources.<Name>` | `SystemOne.ModelSources.ArtifactStore` |
| provider class | `<Name>SystemOneProvider`, `sealed`, constructor `internal` | `AcmeSystemOneProvider` |
| entry point class | `<Name>Providers`, `static` | `AcmeProviders` |
| entry point method | `Use<Name>(this SystemOneProviders anchor, …, HttpClient? http = null)` | `UseAcme` |
| model name constant | `public const string` on the entry point class | `ClefProviders.ClefFlashModel` |
| request template | an extension method on `SystemOneRequests` named after the decision | `Moderation` |
| weight source | `From<Name>(this SystemOneModelSources anchor, …)` | `FromArtifactStore` |

Other conventions, from [ADR-0001](adr/0001-object-creation.md):

- Consumers create everything through a mount point, so keep provider and source constructors
  `internal`. Only `HttpSystemOneProvider` and `SystemOneEndpoint` are public building blocks.
- Name the mount point parameter `anchor`.
- Methods that add an item are nouns (`.Noul`, `.Image`); methods that replace a setting start with
  `With` (`.WithModel`); methods that read start with `Get`.
- When a generic low-level method has named equivalents, hide it from IntelliSense with
  `[EditorBrowsable(EditorBrowsableState.Never)]`.
- Default `HttpClient? http = null` to `SystemOneProvider.DefaultHttpClient`.

## Checklist

- [ ] The provider derives from `SystemOneProvider` (or `HttpSystemOneProvider`).
- [ ] Every question gets an answer of the matching type under the same name.
- [ ] `request.Model` overrides the default model; `Response.Model` names the model that answered.
- [ ] Failures are `SystemOneException`; caller cancellation stays `OperationCanceledException`.
- [ ] The provider is safe to call concurrently and does not change the request.
- [ ] Constructors are `internal`; consumers use a `Use*` method on `SystemOneProviders`.
- [ ] `SystemOneEndpoint.Locality` says honestly whether data leaves the network.
- [ ] `Use*` methods reject a blank API key with `ArgumentException.ThrowIfNullOrWhiteSpace`.
- [ ] Tests use a stub handler or fake model and never touch the network.