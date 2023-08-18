using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetUtilities
{
    public class TraceRoute
    {
        public struct Result
        {
            public IPAddress Target;
            public bool Succeed;
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

        public async Task<Result> RunAsync(Options opts, CancellationToken cancellationToken)
        {
            var result = new Result()
            {
                Target = opts.Target, Hops = new List<HopInfo>()
            };
            if (cancellationToken.IsCancellationRequested)
            {
                return result;
            }

            var ping = opts.PingDelegate;
            
            byte[] bytes = new byte[opts.PacketSize].Fill(0x55);
            for(int i = 1; i <= opts.MaxHops; i++)
            {
                var reply = await PingAsync(ping, opts.Target, bytes, i, opts.PingTimeout, opts.RetryTimes, cancellationToken);
                
                if (cancellationToken.IsCancellationRequested)
                {
                    return result;
                }
                
                var hopInfo = new HopInfo()
                {
                    Hop = i, Status = reply.PingStatus, Address = reply.Address, RTT = reply.Time
                };
                if (opts.ResolveHost && hopInfo.Status != IPStatus.TimedOut)
                {
                    hopInfo.HostName = await NsLookup.GetHostNameAsync(reply.Address).Timeout(opts.NsLookupTimeout, false);
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    return result;
                }
                
                result.Hops.Add(hopInfo);
                OnHop?.Invoke(hopInfo);
  
                if(reply.Status == PingStatus.Success)
                {
                    result.Succeed = true;
                    break;
                }
            }

            return result;
        }

        private static async Task<PingReply> PingAsync(IPingDelegate ping, IPAddress target, byte[] buffer, int ttl, int timeout,
            int retryTimes, CancellationToken cancellationToken)
        {
            PingReply reply = new PingReply();
            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return reply;
                }
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