using Contract;
using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    internal sealed class RequestQueue
    {
        private readonly Random _rnd;
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
                        var tcs = new TaskCompletionSource(uid, this);
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
        internal void OnResponse(Response response)
        {
            bool removed;
            TaskCompletionSource tcs;

            lock (_dict)
            {
                // Обязательно удалить из словаря,
                // что-бы дубль результата не мог сломать рабочий процесс.
                removed = _dict.Remove(response.Uid, out tcs);
            }

            if(removed)
            {
                tcs.OnResponse(response);
            }
        }
    }
}
