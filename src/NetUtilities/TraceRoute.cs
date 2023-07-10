using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace NetUtilities
{
    public class TraceRoute
    {
        public struct HopInfo
        {
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
        
        public struct Options
        {
            public int MaxHops;
            public int pingTimeout;
            public int retryTimes;
            public bool resolveHost;
        }

        public static Options DefaultOpts = new Options() { MaxHops = 30, pingTimeout = 4000, resolveHost = true, retryTimes = 3 };
        
        public static IEnumerable<HopInfo> Run(string ipAddressOrHostName, Options opts, Action<string> logger = null)
        {
            IPAddress ipAddress = Dns.GetHostEntry(ipAddressOrHostName).AddressList[0];
            List<HopInfo> results = new List<HopInfo>();

            using(var pingSender = new System.Net.NetworkInformation.Ping())
            {
                PingOptions pingOptions = new PingOptions();
                Stopwatch stopWatch = new Stopwatch();
                byte[] bytes = new byte[32];
  
                pingOptions.DontFragment = true;
                pingOptions.Ttl = 1;
                
                logger?.Invoke($"Tracing route to {ipAddress} over a maximum of {opts.MaxHops} hops:");

                for(int i = 1; i < opts.MaxHops + 1; i++)
                {
                    stopWatch.Restart();
                    PingReply pingReply = Ping(pingSender, ipAddress, bytes, pingOptions, opts.pingTimeout, opts.retryTimes);
                    stopWatch.Stop();

                    var hopInfo = new HopInfo()
                    {
                        Status = pingReply.Status, Address = pingReply.Address, RTT = (int)stopWatch.ElapsedMilliseconds
                    };
                    if (opts.resolveHost && hopInfo.Status != IPStatus.TimedOut)
                    {
                        hopInfo.HostName = NsLookup.GetHostName(pingReply.Address);
                    }
                    results.Add(hopInfo);
                    logger?.Invoke($"{i}\t{hopInfo}");
  
                    if(pingReply.Status == IPStatus.Success)
                    {
                        break;
                    }
  
                    pingOptions.Ttl++;
                }
                
            }

            return results;
        }

        private static PingReply Ping(System.Net.NetworkInformation.Ping ping, IPAddress address, byte[] buffer, PingOptions options, int timeout,
            int retryTimes)
        {
            int times = 0;
            PingReply reply;
            do
            {
                reply = ping.Send(address, timeout, buffer, options);
            } while (times++ < retryTimes && reply.Status == IPStatus.TimedOut);

            return reply;
        }
    }
}