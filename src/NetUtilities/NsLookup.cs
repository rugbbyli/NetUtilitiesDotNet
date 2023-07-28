using System.Linq;
using System.Net;

public class NsLookup
{
    public static string GetHostName(IPAddress ip)
    {
        if (ip != null)
        {
            try
            {
                return Dns.GetHostEntry(ip).HostName;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static IPAddress GetIp(string hostName)
    {
        try
        {
            return Dns.GetHostAddresses(hostName).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}