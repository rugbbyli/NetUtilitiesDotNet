using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetUtilities
{
    internal static class Extensions
    {
        public static byte[] Fill(this byte[] array, byte value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }

            return array;
        }

        public static async Task<T> Timeout<T>(this Task<T> task, int timeoutMs, bool throwException = true)
        {
            var cts = new CancellationTokenSource();
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            var t = await Task.WhenAny(task, timeoutTask);
            if (t == timeoutTask)
            {
                if (throwException)
                {
                    throw new TimeoutException();
                }

                return default;
            }
            cts.Cancel(false);
            if (task.IsFaulted && task.Exception != null)
            {
                if (throwException)
                {
                    throw task.Exception;
                }

                return default;
            }
            return task.Result;
        }

        public static Task<SocketReceiveFromResult> ReceiveFromAnyAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags flags, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<SocketReceiveFromResult>(cancellationToken);
            }
            var tcs = new TaskCompletionSource<SocketReceiveFromResult>(socket);
            EndPoint remote = new IPEndPoint(0, 0);
            socket.BeginReceiveFrom(buffer.Array, buffer.Offset, buffer.Count, flags, ref remote, iar =>
            {
                var (innerTcs, endPoint, cancelToken) = ((TaskCompletionSource<SocketReceiveFromResult>, EndPoint, CancellationToken))iar.AsyncState;
                if (cancelToken.IsCancellationRequested)
                {
                    innerTcs.TrySetCanceled();
                }
                else
                {
                    try
                    {
                        int receivedBytes = ((Socket)innerTcs.Task.AsyncState).EndReceiveFrom(iar, ref endPoint);
                        if (cancelToken.IsCancellationRequested)
                        {
                            innerTcs.TrySetCanceled();
                        }
                        else
                        {
                            innerTcs.TrySetResult(new SocketReceiveFromResult
                            {
                                ReceivedBytes = receivedBytes,
                                RemoteEndPoint = endPoint,
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        innerTcs.TrySetException(e);
                    }
                }
            }, (tcs, remote, cancellationToken));

            cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            return tcs.Task;
        }
    }
}