using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace wRPC
{
    internal sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private readonly Releaser _releaser;
        private bool _disposed;

        public AsyncLock()
        {
            _sem = new SemaphoreSlim(1, 1);
            _releaser = new Releaser(this);
        }

        /// <exception cref="ObjectDisposedException"/>
        public Task<Releaser> LockAsync()
        {
            return _sem.WaitAsync().ContinueWith(OnEnterSemaphore, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>
        /// Происходит при успешном захвате семафора.
        /// </summary>
        private Releaser OnEnterSemaphore(Task task)
        {
            // Семафор мог быть освобожден после вызова Dispose.
            lock (_sem)
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().FullName);
            }
            return _releaser;
        }

        internal readonly struct Releaser : IDisposable
        {
            private readonly AsyncLock _self;
            public Releaser(AsyncLock self)
            {
                _self = self;
            }

            /// <summary>
            /// Потокобезопасно. Не бросает исключения.
            /// </summary>
            public void Dispose()
            {
                lock (_self._sem)
                {
                    if (!_self._disposed)
                    {
                        _self._sem.Release();
                    }
                }
            }
        }

        /// <summary>
        /// Потокобезопасно. Не бросает исключения.
        /// </summary>
        public void Dispose()
        {
            // Fast-Path.
            if (!_disposed)
            {
                lock (_sem)
                {
                    if (!_disposed)
                    {
                        _disposed = true;

                        // Возможно есть поток который ожидает семафор. Необходимо отпустить его.
                        if (_sem.CurrentCount == 0)
                            _sem.Release();

                        _sem.Dispose();
                    }
                }
            }
        }
    }
}
