using System.Linq;
using System.Net;

public class NsLookup
{
    public static string GetHostName(IPAddress ip)
    {
        if (TryGetHostEntry(ip.ToString(), out var host))
        {
            return host.HostName;
        }

        return string.Empty;
    }

    public static IPAddress GetIp(string hostName)
    {
        if (TryGetHostEntry(hostName, out var host))
        {
            return host.AddressList.FirstOrDefault();
        }

        return null;
    }

    private static bool TryGetHostEntry(string ipOrHost, out IPHostEntry hostEntry)
    {
        try
        {
            hostEntry = Dns.GetHostEntry(ipOrHost);
            return true;
        }
        catch
        {
            hostEntry = null;
            return false;
        }
    }
}