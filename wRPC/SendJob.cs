using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace wRPC
{
    internal readonly struct SendJob
    {
        public SocketQueue SocketQueue { get; }
        public MemoryPoolStream MemoryPoolStream { get; }

        public SendJob(SocketQueue socketQueue, MemoryPoolStream memoryPoolStream)
        {
            SocketQueue = socketQueue;
            MemoryPoolStream = memoryPoolStream;
        }
    }
}
