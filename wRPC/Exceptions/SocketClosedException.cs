using System;
using System.Collections.Generic;
using System.Text;

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
