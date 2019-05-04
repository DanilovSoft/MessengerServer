using Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace wRPC
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    internal sealed class RequestQueue
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"Count = {_dict.Count}" + "}";
        private readonly Random _rnd;
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly Dictionary<int, TaskCompletionSource> _dict = new Dictionary<int, TaskCompletionSource>();

        public RequestQueue()
        {
            _rnd = new Random();
        }

        /// <summary>
        /// Потокобезопасно добавляет запрос в очередь запросов.
        /// </summary>
        public TaskCompletionSource CreateRequest()
        {
            do
            {
                lock (_dict)
                {
                    int uid = _rnd.Next();
                    if (!_dict.ContainsKey(uid))
                    {
                        var tcs = new TaskCompletionSource(uid);
                        _dict.Add(uid, tcs);
                        return tcs;
                    }
                }
            } while (true);
        }

        //internal void RemoveRequest(int uid)
        //{
        //    lock (_dict)
        //    {
        //        _dict.Remove(uid);
        //    }
        //}

        /// <summary>
        /// Потокобезопасно связывает результат запроса с самим запросом.
        /// </summary>
        internal void OnResponse(Message message)
        {
            bool removed;
            TaskCompletionSource tcs;

            lock (_dict)
            {
                if (removed = _dict.TryGetValue(message.Uid, out tcs))
                {
                    // Обязательно удалить из словаря,
                    // что-бы дубль результата не мог сломать рабочий процесс.
                    _dict.Remove(message.Uid);
                }
            }

            if(removed)
            {
                tcs.OnResponse(message);
            }
        }
    }
}
