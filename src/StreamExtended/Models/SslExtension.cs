using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
using System.Net.Security;
using StreamExtended.Helpers;

namespace StreamExtended.Models;

/// <summary>
///     The SSL extension information.
/// </summary>
public class SslExtension
{
    internal static readonly byte[] Http11Utf8 = new byte[] { 0x68, 0x74, 0x74, 0x70, 0x2f, 0x31, 0x2e, 0x31 }; // "http/1.1"
    internal static readonly byte[] Http2Utf8 = new byte[] { 0x68, 0x32 }; // "h2"
    internal static readonly byte[] Http3Utf8 = new byte[] { 0x68, 0x33 }; // "h3"

    private static readonly Dictionary<int, string> NamedCurves = new()
    {
        [1] = "sect163k1 [0x1]",
        [2] = "sect163r1 [0x2]",
        [3] = "sect163r2 [0x3]",
        [4] = "sect193r1 [0x4]",
        [5] = "sect193r2 [0x5]",
        [6] = "sect233k1 [0x6]",
        [7] = "sect233r1 [0x7]",
        [8] = "sect239k1 [0x8]",
        [9] = "sect283k1 [0x9]",
        [10] = "sect283r1 [0xA]",
        [11] = "sect409k1 [0xB]",
        [12] = "sect409r1 [0xC]",
        [13] = "sect571k1 [0xD]",
        [14] = "sect571r1 [0xE]",
        [15] = "secp160k1 [0xF]",
        [16] = "secp160r1 [0x10]",
        [17] = "secp160r2 [0x11]",
        [18] = "secp192k1 [0x12]",
        [19] = "secp192r1 [0x13]",
        [20] = "secp224k1 [0x14]",
        [21] = "secp224r1 [0x15]",
        [22] = "secp256k1 [0x16]",
        [23] = "secp256r1 [0x17]",
        [24] = "secp384r1 [0x18]",
        [25] = "secp521r1 [0x19]",
        [26] = "brainpoolP256r1 [0x1A]",
        [27] = "brainpoolP384r1 [0x1B]",
        [28] = "brainpoolP512r1 [0x1C]",
        [29] = "x25519 [0x1D]",
        [30] = "x448 [0x1E]",
        [256] = "ffdhe2048\t[0x0100]",
        [257] = "ffdhe3072 [0x0101]",
        [258] = "ffdhe4096 [0x0102]",
        [259] = "ffdhe6144 [0x0103]",
        [260] = "ffdhe8192 [0x0104]",
        [65281] = "arbitrary_explicit_prime_curves [0xFF01]",
        [65282] = "arbitrary_explicit_char2_curves [0xFF02]",
    };

    private static readonly Dictionary<int, string> SignatureSchemes = new()
    {
        [0x401] = "rsa_pkcs1_sha256",
        [0x501] = "rsa_pkcs1_sha384",
        [0x601] = "rsa_pkcs1_sha512",
        [0x403] = "ecdsa_secp256r1_sha256",
        [0x503] = "ecdsa_secp384r1_sha384",
        [0x603] = "ecdsa_secp521r1_sha512",
        [0x804] = "rsa_pss_rsae_sha256",
        [0x805] = "rsa_pss_rsae_sha384",
        [0x806] = "rsa_pss_rsae_sha512",
        [0x807] = "ed25519",
        [0x808] = "ed448",
        [0x809] = "rsa_pss_pss_sha256",
        [0x80A] = "rsa_pss_pss_sha384",
        [0x80B] = "rsa_pss_pss_sha512",
        [0x201] = "rsa_pkcs1_sha1",
        [0x203] = "ecdsa_sha1",
    };

    private static readonly Dictionary<int, string> ExtensionNames = CreateExtensionNames();

    /// <summary>
    ///     Initializes a new instance of the <see cref="SslExtension" /> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="data">The data.</param>
    /// <param name="position">The position.</param>
    public SslExtension(int value, ReadOnlyMemory<byte> data, int position)
    {
        Value = value;
        this.data = data;
        Name = GetExtensionName(value);
        Position = position;
    }

    private ReadOnlyMemory<byte> data;

    /// <summary>
    ///     Gets the value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    ///     Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the data.
    /// </summary>
    public string Data => GetExtensionData(Value, data.Span);

    internal List<SslApplicationProtocol> Alpns => GetApplicationLayerProtocolNegotiation(data.Span);

    internal List<string> Protocols => GetSupportedVersions(data.Span);

    /// <summary>
    ///     Gets the position.
    /// </summary>
    public int Position { get; }

    private static string GetExtensionData(int value, ReadOnlySpan<byte> data)
    {
        // https://www.iana.org/assignments/tls-extensiontype-values/tls-extensiontype-values.xhtml
        return value switch
        {
            0 => FormatServerNameList(data),
            5 => FormatStatusRequest(data),
            10 => GetSupportedGroup(data),
            11 => GetEcPointFormats(data),
            13 => GetSignatureAlgorithms(data),
            16 => FormatAlpn(data),
            21 => FormatPadding(data),
            43 => string.Join(", ", GetSupportedVersions(data)),
            50 => GetSignatureAlgorithms(data),
            35655 => $"{data.Length} bytes",
            _ => data.ByteArrayToHexString()
        };
    }

    private static string FormatServerNameList(ReadOnlySpan<byte> data)
    {
        var stringBuilder = new StringBuilder();
        var index = 2;
        while (index + 3 <= data.Length)
        {
            int nameType = data[index];
            var count = (data[index + 1] << 8) + data[index + 2];
            if (index + 3 + count > data.Length)
            {
                break;
            }

            if (nameType == 0)
            {
                var str = Encoding.ASCII.GetString(data.Slice(index + 3, count));
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append("; ");
                }

                stringBuilder.Append(str);
            }

            index += 3 + count;
        }

        return stringBuilder.ToString();
    }

    private static string FormatStatusRequest(ReadOnlySpan<byte> data)
    {
        if (data.Length == 5 && data[0] == 1 && data[1] == 0 && data[2] == 0 && data[3] == 0 && data[4] == 0)
        {
            return "OCSP - Implicit Responder";
        }

        return data.ByteArrayToHexString();
    }

    private static string FormatAlpn(ReadOnlySpan<byte> data)
    {
        var protocols = GetApplicationLayerProtocolNegotiation(data);
        return string.Join(", ", protocols.Select(x => Encoding.UTF8.GetString(x.Protocol.Span)));
    }

    private static string FormatPadding(ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != 0)
            {
                return data.ByteArrayToHexString();
            }
        }

        return $"{data.Length:N0} null bytes";
    }

    private static string GetSupportedGroup(ReadOnlySpan<byte> data)
    {
        // https://datatracker.ietf.org/doc/draft-ietf-tls-rfc4492bis/?include_text=1
        var list = new List<string>();
        if (data.Length < 2)
        {
            return string.Empty;
        }

        var i = 2;
        while (i < data.Length - 1)
        {
            var namedCurve = (data[i] << 8) + data[i + 1];
            list.Add(NamedCurves.TryGetValue(namedCurve, out var name)
                ? name
                : $"unknown [0x{namedCurve:X4}]");
            i += 2;
        }

        return string.Join(", ", list);
    }

    private static string GetEcPointFormats(ReadOnlySpan<byte> data)
    {
        var list = new List<string>();
        if (data.Length < 1)
        {
            return string.Empty;
        }

        var i = 1;
        while (i < data.Length)
        {
            list.Add(data[i] switch
            {
                0 => "uncompressed [0x0]",
                1 => "ansiX962_compressed_prime [0x1]",
                2 => "ansiX962_compressed_char2 [0x2]",
                _ => $"unknown [0x{data[i]:X2}]"
            });
            i += 2;
        }

        return string.Join(", ", list);
    }

    private static List<string> GetSupportedVersions(ReadOnlySpan<byte> data)
    {
        var list = new List<string>();
        if (data.Length < 2)
        {
            return list;
        }

        int i = 0;
        if (data.Length > 2)
        {
            // client hello contains a list (1-byte length prefix)
            i = 1;
        }

        for (; i + 1 < data.Length; i += 2)
        {
            int val = (data[i] << 8) | data[i + 1];
            list.Add(FormatSupportedVersion(val));
        }

        return list;
    }

    private static string FormatSupportedVersion(int val)
    {
        return val switch
        {
            0x300 => "Ssl3.0",
            0x301 => "Tls1.0",
            0x302 => "Tls1.1",
            0x303 => "Tls1.2",
            0x304 => "Tls1.3",
            _ when (val & 0x0A0A) == 0x0A0A && (val >> 8) == (val & 0xFF) => $"grease [0x{val:x}]",
            _ when (val & 0x7F00) == 32512 => $"Tls1.3_draft{val & 0xFF} [0x{val:x}]",
            _ => $"unknown [0x{val:x}]"
        };
    }

    private static string GetSignatureAlgorithms(ReadOnlySpan<byte> data)
    {
        // https://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml
        if (data.Length < 2)
        {
            return string.Empty;
        }

        var num = (data[0] << 8) + data[1];
        var sb = new StringBuilder();
        var index = 2;
        var end = Math.Min(num + 2, data.Length);
        while (index + 1 < end)
        {
            int val0 = data[index];
            int val1 = data[index + 1];
            int val = (val0 << 8) + val1;
            sb.Append(FormatSignatureAlgorithm(val, val0, val1));
            sb.Append(", ");
            index += 2;
        }

        if (sb.Length > 1)
        {
            sb.Length -= 2;
        }

        return sb.ToString();
    }

    private static string FormatSignatureAlgorithm(int val, int val0, int val1)
    {
        if (SignatureSchemes.TryGetValue(val, out var known))
        {
            return known;
        }

        return $"{FormatLegacyPubkey(val1)}_{FormatLegacyHash(val0)}";
    }

    private static string FormatLegacyPubkey(int val1) => val1 switch
    {
        0 => "anonymous",
        1 => "rsa",
        2 => "dsa",
        3 => "ecdsa",
        7 => "ed25519",
        8 => "ed448",
        64 => "gostr34102012_256",
        65 => "gostr34102012_512",
        _ => val1 >= 224
            ? $"Reserved for Private Use[0x{val1:X2}]"
            : $"Reserved[0x{val1:X2}]"
    };

    private static string FormatLegacyHash(int val0) => val0 switch
    {
        0 => "none",
        1 => "md5",
        2 => "sha1",
        3 => "sha224",
        4 => "sha256",
        5 => "sha384",
        6 => "sha512",
        8 => "Intrinsic",
        _ => val0 >= 224
            ? $"Reserved for Private Use[0x{val0:X2}]"
            : $"Reserved[0x{val0:X2}]"
    };

    private static List<SslApplicationProtocol> GetApplicationLayerProtocolNegotiation(ReadOnlySpan<byte> data)
    {
        var list = new List<SslApplicationProtocol>();
        var index = 2;
        while (index < data.Length)
        {
            int count = data[index];
            if (index + 1 + count > data.Length)
            {
                break;
            }

            var protocol = data.Slice(index + 1, count);
            if (Http11Utf8.AsSpan().SequenceEqual(protocol))
            {
                list.Add(SslApplicationProtocol.Http11);
            }
            else if (Http2Utf8.AsSpan().SequenceEqual(protocol))
            {
                list.Add(SslApplicationProtocol.Http2);
            }
            else if (Http3Utf8.AsSpan().SequenceEqual(protocol))
            {
                list.Add(SslApplicationProtocol.Http3);
            }
            else
            {
                list.Add(new SslApplicationProtocol(protocol.ToArray()));
            }

            index += 1 + count;
        }

        return list;
    }

    private static string GetExtensionName(int value)
    {
        // https://www.iana.org/assignments/tls-extensiontype-values/tls-extensiontype-values.xhtml
        if (ExtensionNames.TryGetValue(value, out var name))
        {
            return name;
        }

        if (IsGrease(value))
        {
            return "Reserved (GREASE)";
        }

        return $"unknown_{value:x2}";
    }

    private static bool IsGrease(int value) =>
        value is 2570 or 6682 or 10794 or 14906 or 19018 or 23130 or 27242 or 31354
            or 35466 or 39578 or 43690 or 47802 or 51914 or 56026 or 60138 or 64250;

    private static Dictionary<int, string> CreateExtensionNames() => new()
    {
        [0] = "server_name",
        [1] = "max_fragment_length",
        [2] = "client_certificate_url",
        [3] = "trusted_ca_keys",
        [4] = "truncated_hmac",
        [5] = "status_request",
        [6] = "user_mapping",
        [7] = "client_authz",
        [8] = "server_authz",
        [9] = "cert_type",
        [10] = "supported_groups",
        [11] = "ec_point_formats",
        [12] = "srp",
        [13] = "signature_algorithms",
        [14] = "use_srtp",
        [15] = "heartbeat",
        [16] = "ALPN",
        [17] = "status_request_v2",
        [18] = "signed_certificate_timestamp",
        [19] = "client_certificate_type",
        [20] = "server_certificate_type",
        [21] = "padding",
        [22] = "encrypt_then_mac",
        [23] = "extended_master_secret",
        [24] = "token_binding",
        [25] = "cached_info",
        [26] = "quic_transports_parameters",
        [35] = "SessionTicket TLS",
        [40] = "key_share_draft",
        [41] = "pre_shared_key",
        [42] = "early_data",
        [43] = "supported_versions",
        [44] = "cookie",
        [45] = "psk_key_exchange_modes",
        [46] = "ticket_early_data_info",
        [47] = "certificate_authorities",
        [48] = "oid_filters",
        [49] = "post_handshake_auth",
        [51] = "key_share",
        [57] = "quic_transport_parameters",
        [13172] = "next_protocol_negotiation",
        [30031] = "channel_id_old",
        [30032] = "channel_id",
        [35655] = "draft-agl-tls-padding",
        [65281] = "renegotiation_info",
        [65282] = "Draft version of TLS 1.3",
    };
}
