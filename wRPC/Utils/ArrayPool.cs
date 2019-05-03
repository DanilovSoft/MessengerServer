using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    internal readonly struct ArrayPool : IDisposable
    {
        public byte[] Buffer { get; }

        public ArrayPool(int minimumLength)
        {
            Buffer = ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
}
