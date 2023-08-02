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
        }

        public event Action<HopInfo> OnHop;

        public async Task<Result> RunAsync(Options opts)
        {
            var result = new Result()
            {
                Target = opts.Target, Status = Status.Success, Hops = new List<HopInfo>()
            };
            
            using(var pingSender = new System.Net.NetworkInformation.Ping())
            {
                PingOptions pingOptions = new PingOptions();
                Stopwatch stopWatch = new Stopwatch();
                byte[] bytes = new byte[opts.PacketSize];
  
                pingOptions.DontFragment = false;
                pingOptions.Ttl = 1;
                
                for(int i = 1; i <= opts.MaxHops; i++)
                {
                    stopWatch.Restart();
                    var (status, address) = await PingAsync(pingSender, opts.Target, bytes, pingOptions, opts.PingTimeout, opts.RetryTimes);
                    stopWatch.Stop();

                    var hopInfo = new HopInfo()
                    {
                        Hop = i, Status = status, Address = address, RTT = (int)stopWatch.ElapsedMilliseconds
                    };
                    if (opts.ResolveHost && hopInfo.Status != IPStatus.TimedOut)
                    {
                        hopInfo.HostName = NsLookup.GetHostName(address);
                    }
                    result.Hops.Add(hopInfo);
                    OnHop?.Invoke(hopInfo);
  
                    if(status == IPStatus.Success)
                    {
                        break;
                    }
  
                    pingOptions.Ttl++;
                }
            }

            return result;
        }

        private static async Task<(IPStatus, IPAddress)> PingAsync(System.Net.NetworkInformation.Ping ping, IPAddress target, byte[] buffer, PingOptions options, int timeout,
            int retryTimes)
        {
            int times = 0;
            IPStatus status;
            IPAddress address = null;
            do
            {
                try
                {
                    var reply = await ping.SendPingAsync(target, timeout, buffer, options);
                    status = reply.Status;
                    address = reply.Address;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    status = IPStatus.Unknown;
                }
            } while (times++ < retryTimes && (status == IPStatus.TimedOut || status == IPStatus.Unknown));

            return (status, address);
        }
    }
}