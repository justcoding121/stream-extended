Stream extended
========
* An extended SslStream with support for ALPN & SNI
* An extended BufferedStream with support for reading bytes and string

<a href="https://ci.appveyor.com/project/justcoding121/Streamextended">![Build Status](https://ci.appveyor.com/api/projects/status/3vp1pdya9ncmlqwq?svg=true)</a>

Forked as a separate repository from Titanium Web Proxy Project. 

Installation
========
Install by nuget:

    Install-Package StreamExtended

Usage
======

When SslStream is used on client side.

```csharp
var stream = new CustomBufferedStream(yourNetworkStreamToServer);
bool alpnEnabled = false;
var alpnStream = alpnEnabled ? (Stream)new ClientHelloAlpnAdderStream(stream) : stream;

//as usual
 await sslStream.AuthenticateAsClientAsync(yourRemoteHostName, null, yourSupportedSslProtocols, false);
```

When SslStream is used on server side.

```csharp
var stream = new CustomBufferedStream(yourNetworkStreamToClient);
bool alpnEnabled = false;
var alpnStream = alpnEnabled ? (Stream)new ServerHelloAlpnAdderStream(stream) : stream;

//as usual
await sslStream.AuthenticateAsServerAsync(yourClientCertificate, false, SupportedSslProtocols, false);
```
