using System.Linq;
using System.Net;
using System.Threading.Tasks;

public class NsLookup
{
    public static async Task<string> GetHostNameAsync(IPAddress ip)
    {
        if (ip != null)
        {
            try
            {
                return (await Dns.GetHostEntryAsync(ip)).HostName;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static async Task<IPAddress> GetIpAsync(string hostName)
    {
        try
        {
            return (await Dns.GetHostAddressesAsync(hostName)).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}