## Stream extended

* An extended SslStream with support for ALPN & SNI
* An extended BufferedStream with support for reading bytes and string

<a href="https://ci.appveyor.com/project/justcoding121/Streamextended">![Build Status](https://ci.appveyor.com/api/projects/status/3vp1pdya9ncmlqwq?svg=true)</a>

## Installation

Install by nuget 

    Install-Package StreamExtended

Supports

 * .Net Standard 1.3 or above
 * .Net Framework 4.5 or above
 
## Usage

### ALPN

When SslStream is used on client side.

```csharp
var stream = new CustomBufferedStream(yourNetworkStreamToServer);
bool alpnEnabled = false;
var alpnStream = alpnEnabled ? (Stream)new ClientHelloAlpnAdderStream(stream) : stream;
var sslStream = new SslStream(alpnStream, false, null, null);
//as usual (but we have a bug currently! )
 await sslStream.AuthenticateAsClientAsync(yourRemoteHostName, null, yourSupportedSslProtocols, false);
 
 //TODO add few lines inside ClientHelloAlpnAdderStream so that
 //we will be able to read server hello to find the chosen Http protocol
```

When SslStream is used on server side.

```csharp
var stream = new CustomBufferedStream(yourNetworkStreamToClient);
bool alpnEnabled = false;
var alpnStream = alpnEnabled ? (Stream)new ServerHelloAlpnAdderStream(stream) : stream;
var sslStream = new SslStream(alpnStream);
//as usual
await sslStream.AuthenticateAsServerAsync(yourClientCertificate, false, SupportedSslProtocols, false);
```

### Server Name Indication

```csharp
var clientSslHelloInfo = await SslTools.GetClientHelloInfo(yourClientStream);

//will be null if no client hello was received (not a SSL connection)
if (clientSslHelloInfo != null)
{
    string sniHostName = clientSslHelloInfo.Extensions?.FirstOrDefault(x => x.Name == "server_name")?.Data;
   
    //create yourClientCertificate based on sniHostName
    
    //and now as usual
    var sslStream = new SslStream(yourClientStream);
    await sslStream.AuthenticateAsServerAsync(yourClientCertificate, false, SupportedSslProtocols, false);
}
```


## Peek SSL Information

### Peek Client SSL Hello
```csharp
var clientSslHelloInfo = await SslTools.GetClientHelloInfo(yourClientStream);

//will be null if no client hello was received (not a SSL connection)
if(clientSslHelloInfo!=null)
{
    //and now as usual
    var sslStream = new SslStream(yourClientStream);
    await sslStream.AuthenticateAsServerAsync(yourClientCertificate, false, SupportedSslProtocols, false);
}
```

### Peek Server SSL Hello
```csharp
var serverSslHelloInfo = await SslTools.GetServerHelloInfo(yourServerStream);

//will be null if no server hello was received (not a SSL connection)
if(serverSslHelloInfo!=null)
{
     //and now as usual
     var sslStream = new SslStream(alpnStream, false, null, null);
     await sslStream.AuthenticateAsClientAsync(yourRemoteHostName, null, yourSupportedSslProtocols, false);

}
```

## Contributors

Special thanks to [@honfika](https://github.com/honfika) who contributed this code [originally in Titanium Web Proxy](https://github.com/justcoding121/Titanium-Web-Proxy/issues/293) project. 
