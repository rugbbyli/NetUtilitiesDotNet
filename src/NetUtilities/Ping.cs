using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetUtilities
{
    public interface IPingDelegate
    {
        Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, int packetSize);
        Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, byte[] buffer);
    }
    
    public class Ping : IPingDelegate, IDisposable
    {
        public class Options
        {
            public IPAddress target;
            public int timeout = 5000;
            public int packetSize = 32;
            public int ttl = 64;
            public bool fragment = false;
        }
        
        private System.Net.NetworkInformation.Ping _sender;
        private System.Net.NetworkInformation.PingOptions _options;

        public Ping()
        {
            _sender = new System.Net.NetworkInformation.Ping();
            _options = new PingOptions(1, true);
        }
        
        private async Task<PingReply> RunAsync(IPAddress target, int ttl, bool fragment, int timeout, byte[] buffer)
        {
            var result = new PingReply() { Target = target};
            
            _options.Ttl = ttl;
            _options.DontFragment = !fragment;
            try
            {
                var reply = await _sender.SendPingAsync(target, timeout, buffer, _options);
                result.PingStatus = reply.Status;
                result.Address = reply.Address;
                result.PacketSize = reply.Buffer.Length;
                if (reply.Status == IPStatus.Success)
                {
                    result.Time = (int)reply.RoundtripTime;
                    result.Ttl = reply.Options.Ttl;
                    result.Status = PingStatus.Success;
                }
                else
                {
                    result.Status = PingStatus.Fail;
                }
            }
            catch (Exception e)
            {
                result.Status = PingStatus.Exception;
                result.Exception = e;
            }

            return result;
        }
            
        public Task<PingReply> RunAsync(Options options)
        {
            return RunAsync(options.target, options.ttl, options.fragment, options.timeout, new byte[options.packetSize]);
        }

        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, int packetSize)
        {
            return RunAsync(target, ttl, false, timeout, new byte[packetSize]);
        }

        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, byte[] buffer)
        {
            return RunAsync(target, ttl, false, timeout, buffer);
        }

        public void Dispose()
        {
            _sender.Dispose();
        }
    }
}