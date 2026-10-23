# AGENTS.md

Instructions for coding agents working in this repository. 中文說明見各文件的 `.zh-TW.md` 版本。

## What to read

| Task | Read |
|---|---|
| Write code that uses SystemOne (samples, tests, a consumer app) | [docs/agents/using-systemone.md](docs/agents/using-systemone.md) |
| Add or change a provider, request template, factory helper or weight source | [docs/agents/extending-systemone.md](docs/agents/extending-systemone.md) |
| Understand why objects are created through mount points | [docs/adr/0001-object-creation.md](docs/adr/0001-object-creation.md) |

## Repository layout

| Path | Contents |
|---|---|
| `src/SystemOne.Abstractions` | contracts, request and answer types, mount points, `Factory`, validation |
| `src/SystemOne` | `HttpSystemOneProvider`, `SystemOneEndpoint`, fallback chain |
| `src/SystemOne.Providers.<Name>` | one package per service or model: Clef, TypeSafe, Cloudflare, OpenRouter, Vercel |
| `src/SystemOne.Testing` | `MockSystemOneProvider` |
| `src/SystemOne.ModelSources.HuggingFace` | `HuggingFaceModelSource` |
| `tests/SystemOne.Tests` | one xUnit project for every package |
| `samples/` | runnable examples |
| `docs/` | usage guides (English and `.zh-TW.md`), agent guides, ADRs |

## Commands

```bash
dotnet build -c Release -warnaserror
dotnet test -c Release
dotnet format --verify-no-changes
bash tools/check-docs.sh
```

Integration tests run only when `SYSTEMONE_CLEF_URL` or `TYPESAFE_AI_API_KEY` is set; the TypeSafe
ones cost money.

## Rules

- Code follows the house rules at <https://github.com/orlys/rules>. Highlights: XML doc comments
  in Traditional Chinese (Taiwan terms); braces on every block; no expression-bodied members;
  comments on their own line; named arguments when a call wraps; lines ≤ 120 characters, 96
  preferred.
- Keep the public API shaped by [ADR-0001](docs/adr/0001-object-creation.md): create through
  mount points, `internal` provider constructors, immutable requests and mocks.
- Never call real endpoints from unit tests.
- Documentation is bilingual. When you change behavior or public API, update the English file and
  its `.zh-TW.md` twin together: `README.md`, `docs/usage.md`, `docs/writing-providers.md`,
  `docs/agents/using-systemone.md`, `docs/agents/extending-systemone.md`.
- `docs/agents/*.md` is packed into every NuGet package; keep it accurate for the version being
  released.
- After changing docs or public API, run `bash tools/check-docs.sh` (CI runs it too). It compiles
  every ```` ```csharp ```` block against `src/`, runs the test examples, and checks that English and
  Chinese code blocks match, links and anchors resolve, and lines stay within 120 characters.
  Statement blocks are wrapped in a method; shared variables they assume (such as `request`,
  `response`, `apiKey`) are declared in the script, so add one there if a new example needs it.