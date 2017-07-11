## Stream extended

* An extended SslStream with support for SNI
* An extended BufferedStream with support for reading bytes and string

<a href="https://ci.appveyor.com/project/justcoding121/Streamextended">![Build Status](https://ci.appveyor.com/api/projects/status/3vp1pdya9ncmlqwq?svg=true)</a>

## Installation

Install by [nuget](https://www.nuget.org/packages/StreamExtended)

    Install-Package StreamExtended

Supports

 * .Net Standard 1.3 or above
 * .Net Framework 4.5 or above
 
## Usage

### Server Name Indication

```csharp
var yourClientStream = new CustomBufferedStream(clientStream, 4096)
var clientSslHelloInfo = await SslTools.PeekClientHello(yourClientStream);

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

var yourClientStream = new CustomBufferedStream(clientStream, 4096)
var clientSslHelloInfo = await SslTools.PeekClientHello(yourClientStream);

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
var yourServerStream = new CustomBufferedStream(serverStream, 4096)
var serverSslHelloInfo = await SslTools.PeekServerHello(yourServerStream);

//will be null if no server hello was received (not a SSL connection)
if(serverSslHelloInfo!=null)
{
     //and now as usual
     var sslStream = new SslStream(yourServerStream, false, null, null);
     await sslStream.AuthenticateAsClientAsync(yourRemoteHostName, null, yourSupportedSslProtocols, false);

}
```

## Contributors

Special thanks to [@honfika](https://github.com/honfika) who contributed this code [originally in Titanium Web Proxy](https://github.com/justcoding121/Titanium-Web-Proxy/issues/293) project. 
