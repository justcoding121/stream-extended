using System.Collections.Generic;
using StreamExtended.Models;

namespace StreamExtended.Helpers;

internal static class SslProtocolExtensions
{
    internal static List<string>? GetSslProtocols(this ClientHelloInfo clientHelloInfo)
    {
        if (clientHelloInfo.Extensions != null &&
            clientHelloInfo.Extensions.TryGetValue("supported_versions", out var versions))
        {
            var protocols = versions.Protocols;
            if (protocols.Count != 0)
            {
                return protocols;
            }
        }

        return null;
    }
}
