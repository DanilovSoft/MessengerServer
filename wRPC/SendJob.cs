using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace wRPC
{
    internal readonly struct SendJob
    {
        public SocketQueue SocketQueue { get; }
        public MemoryPoolStream MemoryPoolStream { get; }

        [DebuggerStepThrough]
        public SendJob(SocketQueue socketQueue, MemoryPoolStream memoryPoolStream)
        {
            SocketQueue = socketQueue;
            MemoryPoolStream = memoryPoolStream;
        }
    }
}
