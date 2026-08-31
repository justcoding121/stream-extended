# StreamExtended

Actively maintained .NET library for peeking TLS **ClientHello** / **ServerHello** (SNI, ALPN, and other extensions) from a stream before `SslStream.AuthenticateAsServer` / `AuthenticateAsClient`.

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
