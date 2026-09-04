using System.Net;
using System.Net.Sockets;

namespace Obrasci.Services;

public static class SsrfUrlValidator
{
    public static bool TryValidatePublicHttpsUrl(
        string? input,
        out string error)
    {
        error = string.Empty;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            error = "A valid absolute URL is required.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Only HTTPS URLs are allowed.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "URLs containing user information are not allowed.";
            return false;
        }

        if (uri.IsLoopback ||
            string.Equals(uri.Host, "localhost",
                StringComparison.OrdinalIgnoreCase))
        {
            error = "Loopback and localhost URLs are not allowed.";
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var ip) &&
            IsPrivateOrReserved(ip))
        {
            error = "Private, loopback, and link-local IP addresses are not allowed.";
            return false;
        }

        return true;
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal ||
                   ip.IsIPv6SiteLocal ||
                   ip.IsIPv6Multicast;
        }

        var bytes = ip.GetAddressBytes();

        return bytes[0] == 10 ||
               bytes[0] == 127 ||
               (bytes[0] == 169 && bytes[1] == 254) ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}