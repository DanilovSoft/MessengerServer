using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace wRPC
{
    /// <summary>
    /// Нумератор использующий блокировку на объект синхронизации.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ThreadSafeEnumerator<T> : IEnumerator<T>
    {
        private readonly object _syncObj;
        private readonly List<T> _list;
        private readonly List<T>.Enumerator _enumerator;
        private int _disposed;
        private T _current;

        // ctor.
        public ThreadSafeEnumerator(List<T> list, object syncObj)
        {
            _syncObj = syncObj ?? throw new ArgumentNullException(nameof(syncObj));
            _list = list;
            _enumerator = list.GetEnumerator();
            Monitor.Enter(syncObj);
        }

        public T Current => _current;
        object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            return _enumerator.MoveNext();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if(Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                // Атомарно освобождаем блокировку.
                Monitor.Exit(_syncObj);
            }
        }
    }
}
