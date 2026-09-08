using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StreamExtended.Integration.Tests;

[TestClass]
public class TlsPeekE2ETests
{
    [TestMethod]
    [TestCategory("E2E")]
    public async Task PeekClientHello_ThenAuthenticateAsServer_ExchangesApplicationBytes()
    {
        await using var harness = new LoopbackTlsHarness();
        using var certificate = LoopbackTlsHarness.CreateServerCertificate();

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await harness.AcceptAsync();
            await using var peekable = harness.Wrap(accepted.GetStream());

            var hello = await SslTools.PeekClientHello(peekable, harness.BufferPool, harness.CancellationToken);
            Assert.IsNotNull(hello);
            Assert.IsNotNull(hello!.Extensions);
            Assert.IsTrue(hello.Extensions!.ContainsKey("server_name"));
            StringAssert.Contains(hello.Extensions["server_name"].Data, LoopbackTlsHarness.HostName);

            await using var ssl = new SslStream(peekable, leaveInnerStreamOpen: true);
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ClientCertificateRequired = false
            }, harness.CancellationToken);

            var payload = Encoding.ASCII.GetBytes("stream-extended");
            await ssl.WriteAsync(payload, harness.CancellationToken);
            await ssl.FlushAsync(harness.CancellationToken);

            var echo = new byte[payload.Length];
            await ReadExactAsync(ssl, echo, harness.CancellationToken);
            CollectionAssert.AreEqual(payload, echo);
        }, harness.CancellationToken);

        var clientTask = Task.Run(async () =>
        {
            using var connected = await harness.ConnectAsync();
            await using var clientSsl = new SslStream(connected.GetStream(), leaveInnerStreamOpen: true);
            await clientSsl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = LoopbackTlsHarness.HostName,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            }, harness.CancellationToken);

            var incoming = new byte["stream-extended".Length];
            await ReadExactAsync(clientSsl, incoming, harness.CancellationToken);
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("stream-extended"), incoming);
            await clientSsl.WriteAsync(incoming, harness.CancellationToken);
            await clientSsl.FlushAsync(harness.CancellationToken);
        }, harness.CancellationToken);

        try
        {
            await Task.WhenAll(serverTask, clientTask);
        }
        catch
        {
            if (serverTask.IsFaulted)
            {
                throw serverTask.Exception!.GetBaseException();
            }

            throw;
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken);
            if (n == 0)
            {
                throw new EndOfStreamException($"Expected {buffer.Length} bytes, got {read}.");
            }

            read += n;
        }
    }
}
