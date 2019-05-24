using System;

namespace DBCore.Extensions
{
    public class ConcurrentModifyException : Exception
    {
        public ConcurrentModifyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
