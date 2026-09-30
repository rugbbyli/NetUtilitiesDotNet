using System;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NetUtilities
{
    /// <summary>
    /// 基于 iphlpapi 的 IcmpSendEcho，与系统 ping/tracert 同一套 API，不需要管理员权限。仅支持 IPv4。
    /// </summary>
    public class WindowsNativePing : IPingDelegate
    {
        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, int packetSize)
        {
            return RunAsync(target, ttl, timeout, new byte[packetSize]);
        }

        public Task<PingReply> RunAsync(IPAddress target, int ttl, int timeout, byte[] buffer)
        {
            return Task.Run(() => Send(target, ttl, timeout, buffer));
        }

        private static PingReply Send(IPAddress target, int ttl, int timeout, byte[] buffer)
        {
            var reply = new PingReply() { Target = target };

            if (target.AddressFamily != AddressFamily.InterNetwork)
            {
                reply.Status = PingStatus.Exception;
                reply.PingStatus = IPStatus.Unknown;
                reply.Exception = new NotSupportedException($"only IPv4 is supported: {target}");
                return reply;
            }

            var handle = InvalidHandle;
            var replyBuffer = IntPtr.Zero;
            try
            {
                handle = IcmpCreateFile();
                if (handle == InvalidHandle)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // 文档要求：一个 ICMP_ECHO_REPLY + 请求数据 + 8 字节 ICMP 错误报文
                var replySize = Marshal.SizeOf<IcmpEchoReply>() + buffer.Length + 8;
                replyBuffer = Marshal.AllocHGlobal(replySize);
                var options = new IpOptionInformation() { Ttl = (byte)ttl, Flags = DontFragmentFlag };
                var destination = BitConverter.ToUInt32(target.GetAddressBytes(), 0);

                var count = IcmpSendEcho(handle, destination, buffer, (ushort)buffer.Length, ref options,
                    replyBuffer, (uint)replySize, (uint)timeout);
                if (count == 0)
                {
                    // 返回 0 时 GetLastError 可能是 IP_STATUS（>= 11000，如超时）也可能是一般 Win32 错误
                    var error = Marshal.GetLastWin32Error();
                    if (error < IpStatusBase)
                    {
                        throw new Win32Exception(error);
                    }
                    reply.Status = PingStatus.Fail;
                    reply.PingStatus = (IPStatus)error;
                    return reply;
                }

                var echo = Marshal.PtrToStructure<IcmpEchoReply>(replyBuffer);
                reply.PingStatus = (IPStatus)echo.Status;
                reply.Status = reply.PingStatus == IPStatus.Success ? PingStatus.Success : PingStatus.Fail;
                reply.Address = new IPAddress(echo.Address);
                reply.Time = (int)echo.RoundTripTime;
                reply.Ttl = echo.Options.Ttl;
                reply.PacketSize = echo.DataSize;
            }
            catch (Exception e)
            {
                reply.Status = PingStatus.Exception;
                reply.PingStatus = IPStatus.Unknown;
                reply.Exception = e;
            }
            finally
            {
                if (replyBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(replyBuffer);
                }
                if (handle != InvalidHandle)
                {
                    IcmpCloseHandle(handle);
                }
            }

            return reply;
        }

        private const int IpStatusBase = 11000;
        private const byte DontFragmentFlag = 0x02;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct IpOptionInformation
        {
            public byte Ttl;
            public byte Tos;
            public byte Flags;
            public byte OptionsSize;
            public IntPtr OptionsData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IcmpEchoReply
        {
            public uint Address;
            public uint Status;
            public uint RoundTripTime;
            public ushort DataSize;
            public ushort Reserved;
            public IntPtr Data;
            public IpOptionInformation Options;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern IntPtr IcmpCreateFile();

        [DllImport("iphlpapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IcmpCloseHandle(IntPtr icmpHandle);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint IcmpSendEcho(IntPtr icmpHandle, uint destinationAddress, byte[] requestData,
            ushort requestSize, ref IpOptionInformation requestOptions, IntPtr replyBuffer, uint replySize,
            uint timeout);
    }
}
