using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace NetUtilities
{
    public class NetworkInfo
    {
        public struct DnsServer
        {
            public NetworkInterface Adapter;
            public IPAddress[] DnsServers;

            public override string ToString()
            {
                return $"{Adapter.Description}\t{Adapter.NetworkInterfaceType}\n{string.Join("\n", DnsServers.Select(s => $"  {s}"))}";
            }
        }
        
        public static IEnumerable<DnsServer> GetDnsServers()
        {
            bool Good(NetworkInterface @interface)
            {
                if (@interface.OperationalStatus != OperationalStatus.Up) return false;
                if (@interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) return false;
                var stats = @interface.GetIPStatistics();
                if (stats.BytesReceived == 0) return false;

                return true;
            }
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(Good)
                .Select(i => new DnsServer()
                {
                    Adapter = i,
                    DnsServers = i.GetIPProperties().DnsAddresses.ToArray(),
                });
        }

        public bool IsNetworkAvailable => NetworkInterface.GetIsNetworkAvailable();
    }
}