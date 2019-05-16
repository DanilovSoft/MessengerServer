﻿using System;
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
        private string DebugDisplay => "{" + $"{_requestMessage.ActionName}" + "}";
        private readonly RequestMessage _requestMessage;
        public readonly Type ResultType;
        /// <summary>
        /// Флаг используется как fast-path
        /// </summary>
        private volatile bool _isCompleted;
        public bool IsCompleted => _isCompleted;
        private volatile object _response;
        private volatile Exception _exception;
        private Action _continuationAtomic;

        // ctor.
        public TaskCompletionSource(RequestMessage request, Type resultType)
        {
            _requestMessage = request;
            ResultType = resultType;
        }

        public TaskCompletionSource GetAwaiter() => this;

        public object GetResult()
        {
            var ex = _exception;
            if (ex != null)
                throw ex;

            return _response;
        }

        /// <summary>
        /// Передает ожидающему потоку исключение как результат запроса.
        /// </summary>
        public void OnError(Exception exception)
        {
            _exception = exception;
            OnResult();
        }

        /// <summary>
        /// Передает результат ожидающему потоку.
        /// </summary>
        public void OnResponse(object rawResult)
        {
            _response = rawResult;
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
                ThreadPool.UnsafeQueueUserWorkItem(CallContinuation, continuation);
            }
        }

        [DebuggerStepThrough]
        private void CallContinuation(object state)
        {
            var continuation = (Action)state;
            continuation();
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
