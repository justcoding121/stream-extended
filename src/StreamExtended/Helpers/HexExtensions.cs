using System;
using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace StreamExtended.Helpers;

internal static class HexExtensions
{
    internal static string ByteArrayToHexString(this ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        int length = data.Length * 3;
        Span<byte> buf = stackalloc byte[length];
        var buf2 = buf;
        foreach (var b in data)
        {
            Utf8Formatter.TryFormat(b, buf2, out _, new StandardFormat('X', 2));
            buf2[2] = 32; // space
            buf2 = buf2.Slice(3);
        }

        return Encoding.UTF8.GetString(buf.Slice(0, length - 1));
    }
}
