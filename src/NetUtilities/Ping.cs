using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetUtilities
{
    public class Ping
    {
        public enum Status
        {
            Success,
            Fail,
            Exception,
        }
        public struct Result
        {
            public IPAddress Target;
            public Status Status;
            public IPAddress Address;
            public int PacketSize;
            public IPStatus PingStatus;
            public int Time;
            public int Ttl;

            public override string ToString()
            {
                return $"Ping {Target} [{Address}] with {PacketSize} bytes data, status: {Status},{PingStatus}, time: {Time}ms, TTL: {Ttl}";
            }
        }
        
        public class Options
        {
            public IPAddress target;
            public int timeout = 5000;
            public int packetSize = 32;
            public int ttl = 64;
            public bool fragment = false;
        }

        public static async Task<Result> RunAsync(Options options)
        {
            var result = new Result() {PacketSize = options.packetSize, Target = options.target};
            
            var buffer = new byte[options.packetSize];
            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                var opts = new PingOptions(options.ttl, !options.fragment);
                try
                {
                    var reply = await ping.SendPingAsync(options.target, options.timeout, buffer, opts);
                    result.PingStatus = reply.Status;
                    result.Address = reply.Address;
                    if (reply.Status == IPStatus.Success)
                    {
                        result.Time = (int)reply.RoundtripTime;
                        result.Ttl = reply.Options.Ttl;
                        result.Status = Status.Success;
                    }
                    else
                    {
                        result.Status = Status.Fail;
                    }
                }
                catch
                {
                    result.Status = Status.Exception;
                }
            }

            return result;
        }
    }
}