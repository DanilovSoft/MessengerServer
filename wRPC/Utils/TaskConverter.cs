﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace wRPC
{
    internal static class TaskConverter
    {
        private static readonly MethodInfo _method;
        private static readonly SyncDictionary<Type, Func<Task<object>, object>> _dict = new SyncDictionary<Type, Func<Task<object>, object>>();

        static TaskConverter()
        {
            _method = typeof(TaskConverter).GetMethod(nameof(InnerConvertTask), BindingFlags.NonPublic | BindingFlags.Static);
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
            return task.ContinueWith(t =>
            {
                var result = (T)t.GetAwaiter().GetResult();
                t.Dispose();
                return result;
            });
        }

        private static Func<Task<object>, object> Factory(Type key)
        {
            // Создать шаблонный метод InnerConvertTask<T>.
            MethodInfo method = _method.MakeGenericMethod(key);

            // Создать типизированный делегат.
            var deleg = (Func<Task<object>, object>)method.CreateDelegate(typeof(Func<Task<object>, object>));

            return deleg;
        }
    }
}
