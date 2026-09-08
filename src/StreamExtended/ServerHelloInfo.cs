using System;
using System.Collections.Generic;
using System.Text;
using StreamExtended.Helpers;
using StreamExtended.Models;

namespace StreamExtended;

/// <summary>
///     Wraps up the server SSL hello information.
/// </summary>
public class ServerHelloInfo
{
    public ServerHelloInfo(int handshakeVersion, int majorVersion, int minorVersion, byte[] random,
        byte[] sessionId, int cipherSuite, int serverHelloLength)
    {
        HandshakeVersion = handshakeVersion;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
        Random = random;
        SessionId = sessionId;
        CipherSuite = cipherSuite;
        ServerHelloLength = serverHelloLength;
    }

    public int HandshakeVersion { get; }

    public int MajorVersion { get; }

    public int MinorVersion { get; }

    public byte[] Random { get; }

    public DateTime Time => SslHelloFormatting.GetGmtUnixTime(Random);

    public byte[] SessionId { get; }

    public int CipherSuite { get; }

    public byte CompressionMethod { get; set; }

    internal int ServerHelloLength { get; }

    internal int ExtensionsStartPosition { get; set; }

    public Dictionary<string, SslExtension>? Extensions { get; set; }

    /// <summary>
    ///     Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        SslHelloFormatting.AppendHelloPreamble(
            sb, "ServerHello", HandshakeVersion, MajorVersion, MinorVersion, Random, SessionId);
        SslHelloFormatting.AppendExtensions(sb, Extensions);

        sb.AppendLine($"Compression: {SslHelloFormatting.CompressionLabel(CompressionMethod)}");

        if (!SslCiphers.Ciphers.TryGetValue(CipherSuite, out var cipherStr))
        {
            cipherStr = "unknown";
        }

        sb.Append("Cipher:");
        sb.AppendLine($"[0x{CipherSuite:X4}] {cipherStr}");

        return sb.ToString();
    }
}
