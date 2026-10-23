# SystemOne

[![NuGet][nuget-badge]][nuget]
[![CI][ci-badge]][ci]
[![License: MIT][license-badge]][license]

English | [繁體中文][readme-zh]

A typed .NET client for [System One][system-one] decision models. Ask named questions about any
text or JSON and get calibrated probabilities back instead of generated text:

| Question | Asks | Answer |
|---|---|---|
| `noul` | a yes/no question | `Probability` of yes |
| `choice` | one of up to 255 options | the chosen `Option`, its `Confidence` and every option's probability |
| `score` | a place on an ordered scale | the expected `Level` (0-based) and every level's probability |
| `box` | where something is in an attached image | `Bounds` as fractions of the image |

The same code runs against a model on your own hardware, a hosted service or an in-memory mock.
Only the line that creates the provider changes.

## Features

- **Typed requests and answers.** Build a request with a fluent chain, read typed answers with
  `GetNoul`, `GetChoice`, `GetScore` and `GetBox`.
- **One API, many providers.** Self-hosted servers (clef, Kev, laya-serve, Ollama) and hosted
  services (TypeSafe Jev, Cloudflare Workers AI, OpenRouter, Vercel AI Gateway).
- **Fallback chains.** Try your own server first and a hosted service second.
- **Reusable request templates.** Write the questions once and send a new state each time.
- **A mock for tests.** Fixed answers that match the shape of your questions, with no network.
- **No third-party dependencies.** Only the .NET 10 base class library.
- **Docs for coding agents.** Every package carries usage guides written for AI coding agents.

## Install

Packages are split by what they do; install only the ones you call.

```bash
dotnet add package SystemOne.Providers.Clef
dotnet add package SystemOne.Testing
```

| Package | Contents |
|---|---|
| [`SystemOne.Abstractions`][pkg-abstractions] | `ISystemOne`, the `SystemOneProvider` base class, request and answer types, `SystemOneException` |
| [`SystemOne`][pkg-core] | `UseSelfHosted` for any self-hosted `/v1/systemone` server, `UseFallback` |
| [`SystemOne.Providers.Clef`][pkg-clef] | `UseClef` for your own clef server |
| [`SystemOne.Providers.TypeSafe`][pkg-typesafe] | `UseTypeSafeAi` for TypeSafe's hosted Jev |
| [`SystemOne.Providers.Cloudflare`][pkg-cloudflare] | `UseCloudflareWorkersAi` for clef on Cloudflare Workers AI |
| [`SystemOne.Providers.OpenRouter`][pkg-openrouter] | `UseOpenRouter` |
| [`SystemOne.Providers.Vercel`][pkg-vercel] | `UseVercelAiGateway` |
| [`SystemOne.Testing`][pkg-testing] | `UseMock` |
| [`SystemOne.ModelSources.HuggingFace`][pkg-huggingface] | `FromHuggingFace(...)`: cached, verified model downloads for in-process providers |

## Quick start

```csharp
// no using needed: every type is in the global namespace and SystemOne is the root
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
// 1.17: the expected level, 0-based
var risk = response.GetScore("risk").Level;
```

Questions come first and `.For(state)` comes last. The state can be any object, a string or a
`JsonNode`.

## Providers

| Method | Calls | Package |
|---|---|---|
| `UseClef(baseUri)` | your own clef server, model `clef-flash` | `SystemOne.Providers.Clef` |
| `UseSelfHosted(baseUri, model)` | any self-hosted `/v1/systemone` server: Kev, laya-serve, Ollama | `SystemOne` |
| `UseTypeSafeAi(apiKey)` | TypeSafe's hosted Jev | `SystemOne.Providers.TypeSafe` |
| `UseCloudflareWorkersAi(accountId, apiToken)` | clef on Cloudflare Workers AI | `SystemOne.Providers.Cloudflare` |
| `UseOpenRouter(apiKey, model)` | any OpenRouter model | `SystemOne.Providers.OpenRouter` |
| `UseVercelAiGateway(apiKey, model)` | Vercel AI Gateway | `SystemOne.Providers.Vercel` |
| `UseFallback(first, second, …)` | the first provider that succeeds | `SystemOne` |
| `UseMock()` | fixed answers, no network | `SystemOne.Testing` |

Every method above is called on `SystemOne.Providers` and returns an `ISystemOne`. A fallback
chain keeps personal data on your own hardware unless that server fails:

```csharp
var local = SystemOne.Providers.UseClef(new Uri("http://localhost:8000"));
var hosted = SystemOne.Providers.UseTypeSafeAi(apiKey);
var resilient = SystemOne.Providers.UseFallback(local, hosted);
```

Leave hosted providers out of the chain for data that must not leave your network.

## Request templates

A chain without `.For(...)` is a template. Keep it and send a new state each time:

```csharp
var triage = SystemOne.Requests
    .Choice("team", "Which team should handle this ticket?", "billing", "technical", "sales")
    .Score("urgency", "How urgent is it?", "low", "normal", "high");

var answer = await systemOne.DecideAsync(triage.For(ticket));
```

## Testing

The mock answers every question with a fixed value that matches its shape and records each
request. Set the answers you care about:

```csharp
var mock = SystemOne.Providers.UseMock()
    .Choice("team", "billing")
    .Score("urgency", 2);
```

## Errors

A failure on the provider's side (an HTTP error, a timeout, a response that cannot be read)
throws `SystemOneException`; `UseFallback` moves on to the next provider when it sees one. A
request that is not valid throws `ArgumentException` before anything is sent. Cancelling your own
token throws `OperationCanceledException`.

## Documentation

| Document | For |
|---|---|
| [Usage guide][docs-usage] | requests, responses, providers, errors, testing, model weights |
| [Writing providers][docs-providers] | authors of providers, request templates and model sources |
| [Agent guide: using SystemOne][docs-agent-using] | AI coding agents writing code that uses SystemOne |
| [Agent guide: extending SystemOne][docs-agent-extending] | AI coding agents writing providers and extensions |
| [ADR-0001: object creation][docs-adr] | why objects are created through `SystemOne.*` entry points |

Every package also contains the agent guides under `docs/agents/`, so an agent can read the
version you installed at `~/.nuget/packages/<package-id>/<version>/docs/agents/`.

## Samples

The [repository][repo] has two runnable samples. Both fall back to the mock when no clef server
is reachable; set `SYSTEMONE_CLEF_URL` to use yours.

- `samples/SystemOne.Example`: support-ticket triage.
- `samples/SystemOne.ZeroTrust`: zero-trust access decisions. Access-request signals go in; the
  endpoint action, risk level and SOC flags come out, for five scenarios from an ordinary office
  login to an admin delete over Tor.

## Contributing

Build and test with `dotnet build -c Release -warnaserror` and `dotnet test -c Release`. Integration
tests call real endpoints and run only when `SYSTEMONE_CLEF_URL` or `TYPESAFE_AI_API_KEY` is set;
the TypeSafe ones cost money. Read [AGENTS.md][agents] before changing code or docs.

## License

[MIT][license]

[system-one]: https://docs.typesafe.ai/concepts/system-one
[repo]: https://github.com/orlys/systemone
[readme-zh]: https://github.com/orlys/systemone/blob/main/README.zh-TW.md
[agents]: https://github.com/orlys/systemone/blob/main/AGENTS.md
[license]: https://github.com/orlys/systemone/blob/main/LICENSE
[license-badge]: https://img.shields.io/badge/license-MIT-blue.svg
[nuget]: https://www.nuget.org/packages/SystemOne
[nuget-badge]: https://img.shields.io/nuget/v/SystemOne.svg
[ci]: https://github.com/orlys/systemone/actions/workflows/ci.yml
[ci-badge]: https://img.shields.io/github/actions/workflow/status/orlys/systemone/ci.yml?branch=main&label=CI
[docs-usage]: https://github.com/orlys/systemone/blob/main/docs/usage.md
[docs-providers]: https://github.com/orlys/systemone/blob/main/docs/writing-providers.md
[docs-agent-using]: https://github.com/orlys/systemone/blob/main/docs/agents/using-systemone.md
[docs-agent-extending]: https://github.com/orlys/systemone/blob/main/docs/agents/extending-systemone.md
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