# StreamExtended

Actively maintained .NET library for peeking TLS **ClientHello** / **ServerHello** (SNI, ALPN, and other extensions) from a stream before `SslStream.AuthenticateAsServer` / `AuthenticateAsClient`.

[![Build](https://github.com/justcoding121/stream-extended/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/justcoding121/stream-extended/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/StreamExtended.svg)](https://www.nuget.org/packages/StreamExtended)
[![NuGet downloads](https://img.shields.io/nuget/dt/StreamExtended.svg)](https://www.nuget.org/packages/StreamExtended)

## Code Quality

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=alert_status)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=coverage)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=ncloc)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=bugs)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=vulnerabilities)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=code_smells)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=security_rating)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=reliability_rating)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=sqale_rating)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Duplicated Lines](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=duplicated_lines_density)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=justcoding121_stream-extended&metric=sqale_index)](https://sonarcloud.io/summary/overall?id=justcoding121_stream-extended&branch=develop)

## Install

```bash
dotnet add package StreamExtended
```

Current line: **2.0** (requires **.NET 10**). For `net45` / `netstandard1.3`, stay on the **1.0.x** packages.

## Usage

```csharp
using StreamExtended;
using StreamExtended.BufferPool;
using StreamExtended.Network;

IBufferPool bufferPool = new DefaultBufferPool();
await using var stream = new CustomBufferedStream(networkStream, bufferPool, bufferSize: 4096, leaveOpen: true);

var clientHello = await SslTools.PeekClientHello(stream, bufferPool);
if (clientHello?.Extensions != null &&
    clientHello.Extensions.TryGetValue("server_name", out var sni))
{
    var hostName = sni.Data;
    // select certificate / continue with SslStream on the same stream
}

var serverHello = await SslTools.PeekServerHello(stream, bufferPool);
```

`CustomBufferedStream` implements `IPeekStream`, so peeked bytes remain available for the subsequent TLS handshake.

## Supported frameworks

- .NET 10 (`net10.0`)

## Build

```bash
dotnet test src/StreamExtended.sln -c Release
dotnet pack src/StreamExtended/StreamExtended.csproj -c Release
```

## License

MIT — see [LICENSE](LICENSE).
