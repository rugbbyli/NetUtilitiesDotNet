using System;
using System.Net;
using System.Net.NetworkInformation;

namespace NetUtilities
{
    public struct PingReply
    {
        public IPAddress Target;
        public PingStatus Status;
        public IPAddress Address;
        public int PacketSize;
        public IPStatus PingStatus;
        public Exception Exception;
        public int Time;
        public int Ttl;

        public override string ToString()
        {
            return $"Ping {Target} [{Address}] with {PacketSize} bytes data, status: {Status},{PingStatus}, time: {Time}ms, TTL: {Ttl}";
        }
    }
}