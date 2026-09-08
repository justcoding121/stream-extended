using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StreamExtended.Integration.Tests;

[TestClass]
public class TlsPeekIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task PeekClientHello_FromSslStreamClient_ReadsSni()
    {
        await using var harness = new LoopbackTlsHarness();

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await harness.AcceptAsync();
            await using var peekable = harness.Wrap(accepted.GetStream());
            var hello = await SslTools.PeekClientHello(peekable, harness.BufferPool, harness.CancellationToken);
            Assert.IsNotNull(hello);
            Assert.IsNotNull(hello!.Extensions);
            Assert.IsTrue(hello.Extensions!.ContainsKey("server_name"),
                "ClientHello extensions: " + string.Join(", ", hello.Extensions.Keys));
            StringAssert.Contains(hello.Extensions["server_name"].Data, LoopbackTlsHarness.HostName);
        }, harness.CancellationToken);

        using var connected = await harness.ConnectAsync();
        await using var ssl = new SslStream(connected.GetStream(), leaveInnerStreamOpen: true);
        var auth = AuthenticateClientAsync(ssl, alpn: false, harness.CancellationToken);
        await serverTask;
        connected.Close();
        try
        {
            await auth;
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or ObjectDisposedException
                                       or SocketException or OperationCanceledException)
        {
            // Server closed after peek; the client handshake is expected to fail.
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PeekClientHello_FromSslStreamClient_ReadsAlpn()
    {
        await using var harness = new LoopbackTlsHarness();

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await harness.AcceptAsync();
            await using var peekable = harness.Wrap(accepted.GetStream());
            var hello = await SslTools.PeekClientHello(peekable, harness.BufferPool, harness.CancellationToken);
            Assert.IsNotNull(hello);
            Assert.IsNotNull(hello!.Extensions);
            Assert.IsTrue(hello.Extensions!.ContainsKey("ALPN"),
                "ClientHello extensions: " + string.Join(", ", hello.Extensions.Keys));
            StringAssert.Contains(hello.Extensions["ALPN"].Data, "h2");
        }, harness.CancellationToken);

        using var connected = await harness.ConnectAsync();
        await using var ssl = new SslStream(connected.GetStream(), leaveInnerStreamOpen: true);
        var auth = AuthenticateClientAsync(ssl, alpn: true, harness.CancellationToken);
        await serverTask;
        connected.Close();
        try
        {
            await auth;
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or ObjectDisposedException
                                       or SocketException or OperationCanceledException)
        {
            // Server closed after peek; the client handshake is expected to fail.
        }
    }

    private static Task AuthenticateClientAsync(SslStream ssl, bool alpn, CancellationToken cancellationToken)
    {
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = LoopbackTlsHarness.HostName,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            RemoteCertificateValidationCallback = static (_, _, _, _) => true
        };

        if (alpn)
        {
            options.ApplicationProtocols =
            [
                SslApplicationProtocol.Http2,
                SslApplicationProtocol.Http11
            ];
        }

        return ssl.AuthenticateAsClientAsync(options, cancellationToken);
    }
}
