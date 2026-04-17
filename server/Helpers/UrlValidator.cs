using System.Net;
using System.Net.Sockets;

namespace server.Helpers;

public static class UrlValidator
{
    public static bool IsPrivateOrReserved(Uri uri)
    {
        if (!IPAddress.TryParse(uri.Host, out var ip))
        {
            try
            {
                var addresses = Dns.GetHostAddresses(uri.Host);
                return addresses.Any(IsPrivateOrReservedAddress);
            }
            catch
            {
                return false;
            }
        }

        return IsPrivateOrReservedAddress(ip);
    }

    private static bool IsPrivateOrReservedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (link-local / cloud metadata)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 127.0.0.0/8
            if (bytes[0] == 127) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
            {
                return true;
            }
        }

        return false;
    }
}
