﻿// Vitalii Danilov
// Version 1.1.0

using System;
using System.Threading.Tasks;

namespace System.Threading
{
    internal sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private readonly Releaser _releaser;
        private volatile bool _disposed;

        public AsyncLock()
        {
            _sem = new SemaphoreSlim(1, 1);
            _releaser = new Releaser(this);
        }

        /// <exception cref="ObjectDisposedException"/>
        public async Task<Releaser> LockAsync()
        {
            await _sem.WaitAsync().ConfigureAwait(false);

            // Семафор мог быть освобожден после вызова Dispose.
            // Продолжать в этом случае ни в коем случае нельзя.
            if (_disposed) // Должно быть volatile.
                throw new ObjectDisposedException(typeof(AsyncLock).FullName);

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
                // Освобождать семафор нужно через блокировку так как может сработать Dispose.
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
