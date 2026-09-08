using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using StreamExtended;
using StreamExtended.BufferPool;
using StreamExtended.Network;

const string hostName = "localhost";
const int bufferSize = 16 * 1024;
IBufferPool bufferPool = new DefaultBufferPool();

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
using var certificate = CreateServerCertificate(hostName);

Console.WriteLine("StreamExtended peek example on 127.0.0.1:{0}", port);

var serverTask = RunServerAsync(listener, certificate, bufferPool, bufferSize);
var clientTask = RunClientAsync(port);

await Task.WhenAll(serverTask, clientTask);
listener.Stop();
Console.WriteLine("Peek + TLS handshake + echo succeeded.");

static async Task RunServerAsync(
    TcpListener listener,
    X509Certificate2 certificate,
    IBufferPool bufferPool,
    int bufferSize)
{
    using var accepted = await listener.AcceptTcpClientAsync();
    await using var peekable = new CustomBufferedStream(
        accepted.GetStream(), bufferPool, bufferSize, leaveOpen: true);

    var hello = await SslTools.PeekClientHello(peekable, bufferPool);
    if (hello?.Extensions == null || !hello.Extensions.TryGetValue("server_name", out var sni))
    {
        throw new InvalidOperationException("ClientHello did not contain SNI.");
    }

    Console.WriteLine("Peeked ClientHello SNI: {0}", sni.Data);
    if (hello.Extensions.TryGetValue("ALPN", out var alpn))
    {
        Console.WriteLine("Peeked ClientHello ALPN: {0}", alpn.Data);
    }

    await using var ssl = new SslStream(peekable, leaveInnerStreamOpen: true);
    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
    {
        ServerCertificate = certificate,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        ClientCertificateRequired = false
    });

    var payload = Encoding.ASCII.GetBytes("stream-extended");
    await ssl.WriteAsync(payload);
    await ssl.FlushAsync();

    var echo = new byte[payload.Length];
    await ReadExactAsync(ssl, echo);
    if (!payload.AsSpan().SequenceEqual(echo))
    {
        throw new InvalidOperationException("Echo mismatch after TLS.");
    }
}

static async Task RunClientAsync(int port)
{
    using var client = new TcpClient();
    await client.ConnectAsync(IPAddress.Loopback, port);
    await using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: true);
    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
    {
        TargetHost = hostName,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11],
        RemoteCertificateValidationCallback = static (_, _, _, _) => true
    });

    var incoming = new byte["stream-extended".Length];
    await ReadExactAsync(ssl, incoming);
    await ssl.WriteAsync(incoming);
    await ssl.FlushAsync();
}

static async Task ReadExactAsync(Stream stream, byte[] buffer)
{
    var read = 0;
    while (read < buffer.Length)
    {
        var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
        if (n == 0)
        {
            throw new EndOfStreamException($"Expected {buffer.Length} bytes, got {read}.");
        }

        read += n;
    }
}

static X509Certificate2 CreateServerCertificate(string hostName)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        $"CN={hostName}",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    var san = new SubjectAlternativeNameBuilder();
    san.AddDnsName(hostName);
    request.CertificateExtensions.Add(san.Build());
    request.CertificateExtensions.Add(
        new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
            critical: false));

    using var created = request.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(14));

    return X509CertificateLoader.LoadPkcs12(
        created.Export(X509ContentType.Pfx),
        password: null,
        X509KeyStorageFlags.Exportable);
}
