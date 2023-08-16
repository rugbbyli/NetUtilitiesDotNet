using System;
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
    }
}