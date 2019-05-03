using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC
{
    internal static class DynamicAwaiter
    {
        public static Task<object> FromAsync(object actionResult)
        {
            return Async((dynamic)actionResult);
        }

        private static Task<object> Async(object rawResult)
        {
            return Task.FromResult(rawResult);
        }

        private static async Task<object> Async(Task task)
        {
            await task;
            return null;
        }

        private static async Task<object> Async<T>(Task<T> task)
        {
            return await task;
        }

        //private static async ValueTask<object> Async<T>(ValueTask task)
        //{
        //    await task;
        //    return null;
        //}

        //private static async ValueTask<object> Async<T>(ValueTask<T> task)
        //{
        //    return await task;
        //}
    }
}
