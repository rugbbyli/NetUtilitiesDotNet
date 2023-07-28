using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace NetUtilities
{
    public class TraceRoute
    {
        public struct Result
        {
            public string Target;
            public Status Status;
            public List<HopInfo> Hops;
        }

        public enum Status
        {
            Success,
            UnknownHost,
        }

        public struct HopInfo
        {
            public int Hop;
            public IPStatus Status;
            public int RTT;
            public IPAddress Address;
            public string HostName;

            public override string ToString()
            {
                bool timeout = Status == IPStatus.TimedOut;
                if (timeout)
                {
                    return "*\t请求超时";
                }

                return $"{RTT} ms\t{HostName} [{Address}]";
            }
        }
        
        public class Options
        {
            public int MaxHops = 30;
            public int PingTimeout = 4000;
            public int RetryTimes = 3;
            public bool ResolveHost = true;
            public int PacketSize = 32;
        }

        public event Action<HopInfo> OnHop;

        public async Task<Result> RunAsync(string ipAddressOrHostName, Options opts)
        {
            var result = new Result()
            {
                Target = ipAddressOrHostName
            };
            IPAddress ipAddress = NsLookup.GetIp(ipAddressOrHostName);
            if(ipAddress == null)
            {
                result.Status = Status.UnknownHost;
                return result;
            }

            result.Status = Status.Success;
            result.Hops = new List<HopInfo>();

            using(var pingSender = new System.Net.NetworkInformation.Ping())
            {
                PingOptions pingOptions = new PingOptions();
                Stopwatch stopWatch = new Stopwatch();
                byte[] bytes = new byte[opts.PacketSize];
  
                pingOptions.DontFragment = true;
                pingOptions.Ttl = 1;
                
                for(int i = 1; i <= opts.MaxHops; i++)
                {
                    stopWatch.Restart();
                    PingReply pingReply = await PingAsync(pingSender, ipAddress, bytes, pingOptions, opts.PingTimeout, opts.RetryTimes);
                    stopWatch.Stop();

                    var hopInfo = new HopInfo()
                    {
                        Hop = i, Status = pingReply.Status, Address = pingReply.Address, RTT = (int)stopWatch.ElapsedMilliseconds
                    };
                    if (opts.ResolveHost && hopInfo.Status != IPStatus.TimedOut)
                    {
                        hopInfo.HostName = NsLookup.GetHostName(pingReply.Address);
                    }
                    result.Hops.Add(hopInfo);
                    OnHop?.Invoke(hopInfo);
  
                    if(pingReply.Status == IPStatus.Success)
                    {
                        break;
                    }
  
                    pingOptions.Ttl++;
                }
            }

            return result;
        }

        private static async Task<PingReply> PingAsync(System.Net.NetworkInformation.Ping ping, IPAddress address, byte[] buffer, PingOptions options, int timeout,
            int retryTimes)
        {
            int times = 0;
            PingReply reply;
            do
            {
                reply = await ping.SendPingAsync(address, timeout, buffer, options);
            } while (times++ < retryTimes && reply.Status == IPStatus.TimedOut);

            return reply;
        }
    }
}