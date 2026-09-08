# StreamExtended

Peek TLS **ClientHello** / **ServerHello** (SNI, ALPN, and other extensions) from a stream before `SslStream.AuthenticateAsServer` / `AuthenticateAsClient`.

Works on **Windows, Linux, and macOS** with **.NET 10**.

## Install

```bash
dotnet add package StreamExtended
```

Current line is **2.0** (`net10.0`). For `net45` / `netstandard1.3`, stay on the **1.0.x** packages.

## Contents

- [Peeking TLS handshakes](Peeking-TLS-Handshakes) — ClientHello/ServerHello, `CustomBufferedStream`, then `SslStream`
- [Building and testing](Building-and-Testing) — local commands and CI jobs
- [API documentation](https://justcoding121.github.io/stream-extended/) — DocFX type reference
- [README](https://github.com/justcoding121/stream-extended#readme) — install and usage snippet
- [NuGet](https://www.nuget.org/packages/StreamExtended)
