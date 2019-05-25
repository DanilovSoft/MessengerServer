using System;

namespace wRPC
{
    public class SocketClosedException : Exception
    {
        public SocketClosedException()
        {

        }

        public SocketClosedException(string message) : base(message)
        {

        }
    }
}
