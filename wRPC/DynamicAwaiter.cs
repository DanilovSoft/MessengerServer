using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC
{
    internal static class DynamicAwaiter
    {
        public static ValueTask<object> FromAsync(object actionResult)
        {
            return Async((dynamic)actionResult);
        }

        private static ValueTask<object> Async(object rawResult)
        {
            return new ValueTask<object>(rawResult);
        }

        private static async ValueTask<object> Async(Task task)
        {
            await task;
            return null;
        }

        private static async ValueTask<object> Async<T>(Task<T> task)
        {
            return await task;
        }

        private static async ValueTask<object> Async<T>(ValueTask task)
        {
            await task;
            return null;
        }

        private static async ValueTask<object> Async<T>(ValueTask<T> task)
        {
            return await task;
        }
    }
}
