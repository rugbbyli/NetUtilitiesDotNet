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
            public IPAddress Target;
            public List<HopInfo> Hops;
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
                    return $"{Hop,2}    *    请求超时";
                }

                return $"{Hop,2} {RTT,4} ms {HostName} [{Address}]";
            }
        }
        
        public class Options
        {
            public IPAddress Target;
            public int MaxHops = 64;
            public int PingTimeout = 4000;
            public int NsLookupTimeout = 3000;
            public int RetryTimes = 3;
            public bool ResolveHost = true;
            public int PacketSize = 32;
            public IPingDelegate PingDelegate;
        }

        public event Action<HopInfo> OnHop;

        public async Task<Result> RunAsync(Options opts)
        {
            var result = new Result()
            {
                Target = opts.Target, Hops = new List<HopInfo>()
            };

            var ping = opts.PingDelegate;
            
            byte[] bytes = new byte[opts.PacketSize].Fill(0x55);
            for(int i = 1; i <= opts.MaxHops; i++)
            {
                Debug.WriteLine($"ping     {i} at {DateTime.Now}");
                var reply = await PingAsync(ping, opts.Target, bytes, i, opts.PingTimeout, opts.RetryTimes);

                var hopInfo = new HopInfo()
                {
                    Hop = i, Status = reply.PingStatus, Address = reply.Address, RTT = reply.Time
                };
                Debug.WriteLine($"nslookup {i} at {DateTime.Now}");
                if (opts.ResolveHost && hopInfo.Status != IPStatus.TimedOut)
                {
                    hopInfo.HostName = await NsLookup.GetHostNameAsync(reply.Address).Timeout(opts.NsLookupTimeout, false);
                }
                result.Hops.Add(hopInfo);
                OnHop?.Invoke(hopInfo);
  
                Debug.WriteLine($"finish   {i} at {DateTime.Now}");

                if(reply.Status == PingStatus.Success)
                {
                    break;
                }
            }

            return result;
        }

        private static async Task<PingReply> PingAsync(IPingDelegate ping, IPAddress target, byte[] buffer, int ttl, int timeout,
            int retryTimes)
        {
            PingReply reply = new PingReply();
            do
            {
                try
                {
                    reply = await ping.RunAsync(target, ttl, timeout, buffer);
                }
                catch (Exception e)
                {
                    reply.Target = target;
                    reply.Status = PingStatus.Exception;
                    reply.Exception = e;
                }
            } while (retryTimes-- > 0 && reply.Status == PingStatus.Fail && reply.PingStatus == IPStatus.TimedOut);

            return reply;
        }
    }
}