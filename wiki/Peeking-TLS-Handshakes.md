# Peeking TLS handshakes

`SslStream` consumes the handshake as soon as you call `AuthenticateAsServer` or `AuthenticateAsClient`. StreamExtended lets you **read the hello first** (SNI, ALPN, cipher list, extensions) without dropping those bytes, then continue the handshake on the same stream.

## CustomBufferedStream

Wrap the network stream with `CustomBufferedStream`. It implements `IPeekStream`, so `SslTools` can look ahead while later `Read` / `SslStream` calls still see the peeked bytes.

Use `leaveOpen: true` when another owner (usually `SslStream` or the accepted `TcpClient`) should dispose the inner `NetworkStream`.

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
    // select certificate, then AuthenticateAsServer on the same stream
}

var serverHello = await SslTools.PeekServerHello(stream, bufferPool);
```

Extension names follow the IANA TLS table (`server_name`, `ALPN`, `supported_versions`, and others). See `SslExtension.Data` for the decoded payload.

## After the peek

Keep using the same `CustomBufferedStream` instance:

```csharp
await using var ssl = new SslStream(stream, leaveInnerStreamOpen: true);
await ssl.AuthenticateAsServerAsync(options);
```

Do not wrap a second buffer around a stream that already peeked; a new unread wrapper would miss the buffered hello.

## Buffer size

ClientHello records can be larger than 1 KiB (many extensions, GREASE). Size `CustomBufferedStream` so a full hello fits — 4–16 KiB is typical. Peeking past the buffer throws.
