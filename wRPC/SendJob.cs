using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace wRPC
{
    internal sealed class SendJob
    {
        public int ContentLength;
        public SocketQueue SocketQueue;
        public MemoryPoolStream MemoryPoolStream;
    }
}
