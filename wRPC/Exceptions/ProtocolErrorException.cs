using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    [Serializable]
    public class ProtocolErrorException : Exception
    {
        public ProtocolErrorException()
        {

        }

        public ProtocolErrorException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
