using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC
{
    internal static class DynamicAwaiter
    {
        /// <summary>
        /// Асинхронно ожидает завершение задачи если <paramref name="controllerResult"/> является <see cref="Task"/>'ом.
        /// </summary>
        /// <param name="controllerResult"><see cref="Task"/> или любой объект.</param>
        /// <returns></returns>
        public static Task<object> WaitAsync(object controllerResult)
        {
            return Async((dynamic)controllerResult);
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
