using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetUtilities
{
    public class UDPPing : IPingDelegate
    {
        private SocketPing _ping = new SocketPing(SocketType.Dgram, 7);
        
        public Task<PingReply> RunAsync(IPAddress target, int ttl = 64, int timeout = 5000, int packetSize = 32)
        {
            return _ping.RunAsync(target, ttl, false, timeout, new byte[packetSize].Fill(0xbb));
        }

        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, byte[] buffer)
        {
            return _ping.RunAsync(target, ttl, false, timeout, buffer);
        }
    }
}