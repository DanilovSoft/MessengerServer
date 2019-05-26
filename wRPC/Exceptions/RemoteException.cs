using System;

namespace wRPC
{
    /// <summary>
    /// Исключение для передачи информации об ошибке удаленному подключению.
    /// Исключение этого типа прозрачно транслируется на удаленное подключение.
    /// </summary>
    [Serializable]
    public class RemoteException : Exception
    {
        public StatusCode ErrorCode { get; }

        public RemoteException()
        {

        }

        public RemoteException(string message) : base(message)
        {

        }

        public RemoteException(string message, StatusCode errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
