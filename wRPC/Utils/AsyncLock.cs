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
            _releaser = new Releaser(_sem);
        }

        public async Task<Releaser> LockAsync()
        {
            await _sem.WaitAsync().ConfigureAwait(false);
            return _releaser;
        }

        internal readonly struct Releaser : IDisposable
        {
            private readonly SemaphoreSlim _sem;
            public Releaser(SemaphoreSlim sem)
            {
                _sem = sem;
            }

            public void Dispose()
            {
                _sem.Release();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _sem.Dispose();
            }
        }
    }
}
