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
                    return $"{Hop,2} *\t请求超时";
                }

                return $"{Hop,2} {RTT} ms\t{HostName} [{Address}]";
            }
        }
        
        public class Options
        {
            public IPAddress Target;
            public int MaxHops = 64;
            public int PingTimeout = 4000;
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
            
            Stopwatch stopWatch = new Stopwatch();
            byte[] bytes = new byte[opts.PacketSize];
                
            for(int i = 1; i <= opts.MaxHops; i++)
            {
                var reply = await PingAsync(ping, opts.Target, bytes, i, opts.PingTimeout, opts.RetryTimes);

                var hopInfo = new HopInfo()
                {
                    Hop = i, Status = reply.PingStatus, Address = reply.Address, RTT = reply.Time
                };
                if (opts.ResolveHost && hopInfo.Status != IPStatus.TimedOut)
                {
                    hopInfo.HostName = NsLookup.GetHostName(reply.Address);
                }
                result.Hops.Add(hopInfo);
                OnHop?.Invoke(hopInfo);
  
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