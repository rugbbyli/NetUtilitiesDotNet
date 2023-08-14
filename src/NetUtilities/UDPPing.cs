using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetUtilities
{
    public class UDPPing : IPingDelegate
    {
        public class Options
        {
            public IPAddress target;
            public int timeout = 5000;
            public int packetSize = 32;
            public int ttl = 64;
            public bool fragment = false;
        }
        
        
        private struct IPHeader
        {
            public IPAddress Source;
            public IPAddress Destination;
            public byte TimeToLive;

            public static IPHeader Parse(byte[] packet, AddressFamily addressFamily)
            {
                if(addressFamily == AddressFamily.InterNetwork)
                {
                    return new IPHeader()
                    {
                        TimeToLive = packet[8],
                        Source = ParseIP(packet, 12, 4),
                        Destination = ParseIP(packet, 16, 4),
                    };
                }
                else
                {
                    return new IPHeader()
                    {
                        TimeToLive = packet[7],
                        Source = ParseIP(packet, 8, 16),
                        Destination = ParseIP(packet, 24, 16),
                    };
                }
            }

            private static IPAddress ParseIP(byte[] data, int index, int length)
            {
                var b = new byte[length];
                Array.Copy(data, index, b, 0, length);
                return new IPAddress(b);
            }
        }

        private const int Port = 7;
        
        private struct IcmpPacket
        {
            public interface IHeaderTypes
            {
                byte EchoRequest { get; }
                byte EchoResponse { get; }
                byte DestUnavailable { get; }
                byte TimeExceeded { get; }
            }
            
            private class HeaderTypes4 : IHeaderTypes
            {
                public byte EchoRequest => 8;
                public byte EchoResponse => 0;
                public byte DestUnavailable => 3;
                public byte TimeExceeded => 11;
            }
            
            private class HeaderTypes6 : IHeaderTypes
            {
                public byte EchoRequest => 128;
                public byte EchoResponse => 129;
                public byte DestUnavailable => 1;
                public byte TimeExceeded => 3;
            }

            public static IHeaderTypes HeaderTypesV4 { get; } = new HeaderTypes4();
            public static IHeaderTypes HeaderTypesV6 { get; } = new HeaderTypes6();
            
            public byte Type;
            public byte Code;
            public Int16 Checksum;
            public Int16 Identifier;
            public Int16 SequenceNum;
            public byte[] Payload;

            public static IcmpPacket BuildEchoRequest(AddressFamily addressFamily, Int16 identifier, Int16 sequenceNum, byte[] payload)
            {
                return new IcmpPacket()
                {
                    Type = addressFamily == AddressFamily.InterNetwork ? HeaderTypesV4.EchoRequest : HeaderTypesV6.EchoRequest,
                    Code = 0,
                    Identifier = identifier,
                    SequenceNum = sequenceNum,
                    Payload = payload,
                };
            }

            public static IcmpPacket ParseHeader(byte[] data, int index)
            {
                // only parse header part
                return new IcmpPacket()
                {
                    Type = data[index],
                    Code = data[index + 1],
                    Checksum = (Int16)(data[index + 2] << 8 + data[index + 3]),
                    Identifier = (Int16)(data[index + 4] << 8 + data[index + 5]),
                    SequenceNum = (Int16)(data[index + 6] << 8 + data[index + 7]),
                };
            }

            public byte[] ToBytes()
            {
                var packet = new byte[8 + Payload.Length];
                packet[0] = Type;
                packet[1] = Code;
                
                Fill(packet, 4, Identifier);
                Fill(packet, 6, SequenceNum);
                
                Array.Copy(Payload, 0, packet, 8, Payload.Length);
                
                Fill(packet, 2, CalcChecksum(packet));

                return packet;
            }

            private static void Fill(byte[] data, int index, Int16 value)
            {
                data[index] = (byte)(value >> 8);
                data[index + 1] = (byte)value;
            }
            
            private Int16 CalcChecksum(byte[] data) {
                int sum = 0;
                // High bytes (even indices)
                for (int i = 0; i < data.Length; i += 2) {
                    sum += (data[i] & 0xFF) << 8;
                    sum = (sum & 0xFFFF) + (sum >> 16);
                }
                // Low bytes (odd indices)
                for (int i = 1; i < data.Length; i += 2) {
                    sum += (data[i] & 0xFF);
                    sum = (sum & 0xFFFF) + (sum >> 16);
                }
                // Fix any one's-complement errors- sometimes it is necessary to rotate twice.
                sum = (sum & 0xFFFF) + (sum >> 16);
                return (Int16) (sum ^ 0xFFFF);
            }
        }
        
        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, int packetSize)
        {
            return RunAsync(target, ttl, false, timeout, new byte[packetSize]);
        }

        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, byte[] buffer)
        {
            return RunAsync(target, ttl, false, timeout, buffer);
        }
        
        public Task<PingReply> RunAsync(Options options)
        {
            return RunAsync(options.target, options.ttl, options.fragment, options.timeout, new byte[options.packetSize]);
        }
        
        public async Task<PingReply> RunAsync(IPAddress target, int ttl, bool fragment, int timeout, byte[] buffer)
        {
            var result = new PingReply() { Target = target };
            
            var socket = new Socket(target.AddressFamily, SocketType.Dgram, ProtocolType.Icmp);
            socket.DontFragment = !fragment;
            var timeBegin = DateTime.Now.Ticks;
            var packet = IcmpPacket.BuildEchoRequest(socket.AddressFamily, 0, 0, buffer).ToBytes();
            try
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);

                await socket.SendToAsync(new ArraySegment<byte>(packet), SocketFlags.None,
                    new IPEndPoint(target, Port));
                var recvBuf = new ArraySegment<byte>();
                var recvTask = socket.ReceiveAsync(recvBuf, SocketFlags.None);
                var timeoutTask = Task.Delay(timeout);
                var finished = await Task.WhenAny(recvTask, timeoutTask);
                if (finished == timeoutTask)
                {
                    throw new TimeoutException();
                }

                result.PacketSize = recvBuf.Count;
                var ipHeader = IPHeader.Parse(recvBuf.Array, socket.AddressFamily);
                var icmpHeader = IcmpPacket.ParseHeader(recvBuf.Array, 20);

                result.Address = ipHeader.Source;
                result.Ttl = ipHeader.TimeToLive;
                result.Time = (int)((DateTime.Now.Ticks - timeBegin) / TimeSpan.TicksPerMillisecond);

                var headerTypes = target.AddressFamily == AddressFamily.InterNetwork
                    ? IcmpPacket.HeaderTypesV4
                    : IcmpPacket.HeaderTypesV6;

                if (icmpHeader.Type == headerTypes.EchoResponse)
                {
                    result.Status = PingStatus.Success;
                    result.PingStatus = IPStatus.Success;
                }
                else
                {
                    result.Status = PingStatus.Fail;
                    if (icmpHeader.Type == headerTypes.TimeExceeded)
                    {
                        result.PingStatus = IPStatus.TtlExpired;
                    }
                    else if (icmpHeader.Type == headerTypes.DestUnavailable)
                    {
                        result.PingStatus = IPStatus.DestinationUnreachable;
                    }
                    else
                    {
                        result.PingStatus = IPStatus.Unknown;
                    }
                }
            }
            catch (TimeoutException)
            {
                result.Status = PingStatus.Fail;
                result.PingStatus = IPStatus.TimedOut;
            }
            catch (Exception e)
            {
                result.Status = PingStatus.Exception;
                result.PingStatus = IPStatus.Unknown;
                result.Exception = e;
            }
            finally
            {
                socket.Close();
            }

            return result;
        }
    }
}