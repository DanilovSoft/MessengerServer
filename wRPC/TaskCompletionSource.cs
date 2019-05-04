using Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace wRPC
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    internal sealed class TaskCompletionSource : INotifyCompletion
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{Uid}" + "}";
        public int Uid { get; }
        /// <summary>
        /// Флаг используется как fast-path
        /// </summary>
        private volatile bool _isCompleted;
        public bool IsCompleted => _isCompleted;
        private volatile Message _response;
        private Action _continuationAtomic;

        public TaskCompletionSource(int uid)
        {
            Uid = uid;
        }

        public Message GetResult() => _response;

        public TaskCompletionSource GetAwaiter() => this;

        public void OnResponse(Message response)
        {
            _response = response;
            OnResult();
        }

        private void OnResult()
        {
            // Результат уже установлен. Можно установить fast-path.
            _isCompleted = true;

            // Атомарно записать заглушку или вызвать оригинальный continuation
            Action continuation = Interlocked.CompareExchange(ref _continuationAtomic, GlobalVars.DummyAction, null);
            if (continuation != null)
            {
                // Нельзя делать продолжение текущим потоком т.к. это затормозит/остановит диспетчер 
                // или произойдет побег специального потока диспетчера.
                ThreadPool.UnsafeQueueUserWorkItem(delegate
                {
                    continuation();
                }, null);
            }
        }

        public void OnCompleted(Action continuation)
        {
            // Атомарно передаем continuation другому потоку.
            if (Interlocked.CompareExchange(ref _continuationAtomic, continuation, null) != null)
            // P.S. шанс попасть в этот блок очень маленький.
            {
                // В переменной _continuationAtomic была другая ссылка, 
                // это значит что другой поток уже установил результат и его можно забрать.
                continuation();
            }
        }
    }
}
