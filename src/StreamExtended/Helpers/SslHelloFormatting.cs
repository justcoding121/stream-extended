using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StreamExtended.Models;

namespace StreamExtended.Helpers;

/// <summary>
///     Shared formatting helpers for ClientHello / ServerHello display strings.
/// </summary>
internal static class SslHelloFormatting
{
    internal static readonly string[] Compressions =
    {
        "null",
        "DEFLATE"
    };

    internal static DateTime GetGmtUnixTime(byte[] random)
    {
        if (random.Length <= 3)
        {
            return DateTime.MinValue;
        }

        // RFC 5246: gmt_unix_time is the first 4 bytes of Random, big-endian.
        return DateTime.UnixEpoch
            .AddSeconds(((uint)random[0] << 24) + ((uint)random[1] << 16) + ((uint)random[2] << 8) + random[3])
            .ToLocalTime();
    }

    internal static string SslVersionToString(int major, int minor)
    {
        var str = (major, minor) switch
        {
            (3, 3) => "TLS/1.2",
            (3, 2) => "TLS/1.1",
            (3, 1) => "TLS/1.0",
            (3, 0) => "SSL/3.0",
            (2, 0) => "SSL/2.0",
            _ => "Unknown"
        };

        return $"{major}.{minor} ({str})";
    }

    internal static string CompressionLabel(int compressionMethod)
    {
        return Compressions.Length > compressionMethod
            ? Compressions[compressionMethod]
            : $"unknown [0x{compressionMethod:X2}]";
    }

    internal static void AppendHelloPreamble(
        StringBuilder sb,
        string role,
        int handshakeVersion,
        int majorVersion,
        int minorVersion,
        byte[] random,
        byte[] sessionId)
    {
        sb.AppendLine(
            $"A SSLv{handshakeVersion}-compatible {role} handshake was found. The following parameters were extracted.");
        sb.AppendLine();
        sb.AppendLine($"Version: {SslVersionToString(majorVersion, minorVersion)}");
        sb.AppendLine($"Random: {((ReadOnlySpan<byte>)random).ByteArrayToHexString()}");
        sb.AppendLine($"\"Time\": {GetGmtUnixTime(random)}");
        sb.AppendLine($"SessionID: {((ReadOnlySpan<byte>)sessionId).ByteArrayToHexString()}");
    }

    internal static void AppendExtensions(StringBuilder sb, Dictionary<string, SslExtension>? extensions)
    {
        if (extensions == null)
        {
            return;
        }

        sb.AppendLine("Extensions:");
        foreach (var extension in extensions.Values.OrderBy(x => x.Position))
        {
            sb.AppendLine($"{extension.Name}: {extension.Data}");
        }
    }
}
