using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using StreamExtended.BufferPool;
using StreamExtended.Network;

namespace StreamExtended.Integration.Tests;

/// <summary>
/// Loopback TCP pair plus a short-lived self-signed server certificate.
/// </summary>
internal sealed class LoopbackTlsHarness : IAsyncDisposable
{
    public const string HostName = "localhost";
    public const int BufferSize = 16 * 1024;

    private readonly TcpListener listener;
    private readonly CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

    public LoopbackTlsHarness()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public CancellationToken CancellationToken => cts.Token;

    public IBufferPool BufferPool { get; } = new DefaultBufferPool();

    public Task<TcpClient> AcceptAsync() => listener.AcceptTcpClientAsync(cts.Token).AsTask();

    public async Task<TcpClient> ConnectAsync()
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, Port, cts.Token);
        return client;
    }

    public CustomBufferedStream Wrap(Stream networkStream) =>
        new(networkStream, BufferPool, BufferSize, leaveOpen: true);

    public static X509Certificate2 CreateServerCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={HostName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(HostName);
        request.CertificateExtensions.Add(san.Build());

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                critical: false));

        using var created = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(14));

        // Re-import as PFX so SslStream can use the private key. EphemeralKeySet is
        // rejected by Schannel ("platform does not support ephemeral keys").
        return X509CertificateLoader.LoadPkcs12(
            created.Export(X509ContentType.Pfx),
            password: null,
            X509KeyStorageFlags.Exportable);
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        listener.Stop();
        cts.Dispose();
        await Task.CompletedTask;
    }
}
