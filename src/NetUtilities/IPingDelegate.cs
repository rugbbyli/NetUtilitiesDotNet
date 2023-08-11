using System.Net;
using System.Threading.Tasks;

namespace NetUtilities
{
    public interface IPingDelegate
    {
        Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, int packetSize);
        Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, byte[] buffer);
    }
}