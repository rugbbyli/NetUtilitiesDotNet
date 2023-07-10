using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetUtilities
{
    public class Ping
    {
        public struct Result
        {
            public int PacketSent;
            public int PacketReceived;

            public string Target;
            public IPAddress Address;
            public int PacketSize;
            public List<(int time, int ttl)> Results;

            public override string ToString()
            {
                /*
正在 Ping auth.80166.com [52.80.22.120] 具有 32 字节的数据:
请求超时。
请求超时。
请求超时。
请求超时。
请求超时。

52.80.22.120 的 Ping 统计信息:
    数据包: 已发送 = 5，已接收 = 0，丢失 = 5 (100% 丢失)，
                 */
                var timeStats = Results.Count > 0
                    ? $", time-max={Results.Max(i => i.time)}ms, time-min={Results.Min(i => i.time)}ms, time-avg={Results.Average(i => i.time):N0}ms"
                    : string.Empty;
                return
                    $"Ping {Target} [{Address}] with {PacketSize} bytes data, sent={PacketSent}, recv={PacketReceived}, lost={PacketSent - PacketReceived}{timeStats}";
            }
        }
        
        public class Options
        {
            public string target;
            public int times = 4;
            public int timeout = 5000;
            public int packetSize = 32;
            public int ttl = 64;
            public bool fragment = false;
        }

        public static async Task<Result> RunAsync(Options options, CancellationToken cancellationToken)
        {
            var runTimes = options.times == -1 ? int.MaxValue : options.times;
            var buffer = new byte[options.packetSize];
            var result = new Result() {PacketSize = options.packetSize, Target = options.target, Results = new List<(int time, int ttl)>()};
            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                var opts = new PingOptions(options.ttl, !options.fragment);
                while (runTimes-- > 0 && !cancellationToken.IsCancellationRequested)
                {
                    result.PacketSent++;
                    var reply = await ping.SendPingAsync(options.target, options.timeout, buffer, opts);
                    if (reply.Status == IPStatus.Success)
                    {
                        result.Results.Add(((int)reply.RoundtripTime, reply.Options.Ttl));
                        result.PacketReceived++;
                        result.Address = reply.Address;
                    }
                }
            }

            return result;
        }
    }
}