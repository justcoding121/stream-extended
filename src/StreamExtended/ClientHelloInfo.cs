using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using StreamExtended.Helpers;
using StreamExtended.Models;

namespace StreamExtended;

/// <summary>
///     Wraps up the client SSL hello information.
/// </summary>
public class ClientHelloInfo
{
    internal ClientHelloInfo(int handshakeVersion, int majorVersion, int minorVersion, byte[] random, byte[] sessionId,
        int[] ciphers, int clientHelloLength)
    {
        HandshakeVersion = handshakeVersion;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        Random = random;
        SessionId = sessionId;
        Ciphers = ciphers;
        ClientHelloLength = clientHelloLength;
    }

    public int HandshakeVersion { get; }

    public int MajorVersion { get; }

    public int MinorVersion { get; }

    public byte[] Random { get; }

    public DateTime Time => SslHelloFormatting.GetGmtUnixTime(Random);

    public byte[] SessionId { get; }

    public int[] Ciphers { get; }

    public byte[]? CompressionData { get; internal set; }

    internal int ClientHelloLength { get; }

    internal int ExtensionsStartPosition { get; set; }

    public Dictionary<string, SslExtension>? Extensions { get; set; }

    public SslProtocols SslProtocol
    {
        get
        {
            var major = MajorVersion;
            var minor = MinorVersion;
            if (major == 3 && minor == 3)
            {
                var protocols = this.GetSslProtocols();
                if (protocols != null && protocols.Contains("Tls1.3"))
                {
                    return SslProtocols.Tls12 | SslProtocols.Tls13;
                }

                return SslProtocols.Tls12;
            }

            // Map ClientHello-advertised versions for reporting only (does not enable these protocols).
#pragma warning disable S4423
#pragma warning disable SYSLIB0039
#pragma warning disable 618
            if (major == 3 && minor == 2)
                return SslProtocols.Tls11;

            if (major == 3 && minor == 1)
                return SslProtocols.Tls;

            if (major == 3 && minor == 0)
                return SslProtocols.Ssl3;

            if (major == 2 && minor == 0)
                return SslProtocols.Ssl2;
#pragma warning restore 618
#pragma warning restore SYSLIB0039
#pragma warning restore S4423

            return SslProtocols.None;
        }
    }

    /// <summary>
    ///     Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        SslHelloFormatting.AppendHelloPreamble(
            sb, "ClientHello", HandshakeVersion, MajorVersion, MinorVersion, Random, SessionId);
        SslHelloFormatting.AppendExtensions(sb, Extensions);

        if (CompressionData != null && CompressionData.Length > 0)
        {
            sb.AppendLine($"Compression: {SslHelloFormatting.CompressionLabel(CompressionData[0])}");
        }

        if (Ciphers.Length > 0)
        {
            sb.AppendLine("Ciphers:");
            foreach (var cipherSuite in Ciphers)
            {
                if (!SslCiphers.Ciphers.TryGetValue(cipherSuite, out var cipherStr))
                {
                    cipherStr = "unknown";
                }

                sb.AppendLine($"[0x{cipherSuite:X4}] {cipherStr}");
            }
        }

        return sb.ToString();
    }
}
