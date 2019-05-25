using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace wRPC
{
    internal static class TaskConverter
    {
        private static readonly MethodInfo _InnerConvertTaskMethod;
        private static readonly SyncDictionary<Type, Func<Task<object>, object>> _dict = new SyncDictionary<Type, Func<Task<object>, object>>();

        static TaskConverter()
        {
            _InnerConvertTaskMethod = typeof(TaskConverter).GetMethod(nameof(InnerConvertTask), BindingFlags.NonPublic | BindingFlags.Static);
        }

        /// <summary>
        /// Преобразует <see cref="Task"/><see langword="&lt;object&gt;"/> в <see cref="Task{T}"/>.
        /// </summary>
        public static object ConvertTask(Task<object> task, Type desireType)
        {
            // Получить делегат шаблонного конвертера.
            Func<Task<object>, object> genericConverter = _dict.GetOrAdd(desireType, Factory);

            return genericConverter(task);
        }

        private static object InnerConvertTask<T>(Task<object> task)
        {
            return task.ContinueWith(Convert<T>);
        }

        [DebuggerStepThrough]
        private static T Convert<T>(Task<object> task)
        {
            using (task)
            {
                var result = (T)task.GetAwaiter().GetResult();
                return result;
            }
        }

        private static Func<Task<object>, object> Factory(Type key)
        {
            // Создать шаблонный метод InnerConvertTask<T>.
            MethodInfo method = _InnerConvertTaskMethod.MakeGenericMethod(key);

            // Создать типизированный делегат.
            var deleg = (Func<Task<object>, object>)method.CreateDelegate(typeof(Func<Task<object>, object>));

            return deleg;
        }
    }
}
