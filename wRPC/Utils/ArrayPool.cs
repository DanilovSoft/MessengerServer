using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    internal readonly struct ArrayPool : IDisposable
    {
        public byte[] Array { get; }

        public ArrayPool(int minimumLength)
        {
            Array = ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(Array);
        }
    }
}
