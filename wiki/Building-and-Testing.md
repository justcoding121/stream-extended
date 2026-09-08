# Building and testing

Requires **.NET 10**.

## Local

```bash
dotnet test src/StreamExtended.sln -c Release
```

Suites:

```bash
# Unit (crafted bytes + MemoryStream)
dotnet test tests/StreamExtended.Tests/StreamExtended.Tests.csproj -c Release

# Integration — real loopback ClientHello, peek only
dotnet test tests/StreamExtended.Integration.Tests/StreamExtended.Integration.Tests.csproj -c Release --filter TestCategory=Integration

# E2E — peek, then SslStream.AuthenticateAsServer, then application bytes
dotnet test tests/StreamExtended.Integration.Tests/StreamExtended.Integration.Tests.csproj -c Release --filter TestCategory=E2E
```

```bash
dotnet pack src/StreamExtended/StreamExtended.csproj -c Release
```

## CI

[`.github/workflows/ci.yml`](https://github.com/justcoding121/stream-extended/blob/develop/.github/workflows/ci.yml) on push/PR to `develop`, `beta`, and `stable`:

| Job | When | What |
|-----|------|------|
| `build` | Always | Windows: restore, build, unit + integration + e2e, pack smoke. SonarCloud + coverage only on **develop push**. |
| `test` | Always | Sibling matrix: **Ubuntu** and **macOS**, same three suites. |
| `docs` | develop push, after `build` and `test` | DocFX → GitHub Pages (`docs/`). |
| `publish` | beta/stable push, after `build` and `test` | NuGet + GitHub release. |

Windows is covered by `build`. The sibling matrix does not repeat Windows.

Wiki pages under `wiki/` sync to the GitHub wiki on develop via [wiki-sync](https://github.com/justcoding121/stream-extended/blob/develop/.github/workflows/wiki-sync.yml).
