using System;

namespace DBCore.Extensions
{
    public class ObjectAlreadyExistsException : Exception
    {
        public ObjectAlreadyExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
