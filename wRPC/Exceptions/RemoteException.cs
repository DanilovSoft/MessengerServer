using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    /// <summary>
    /// Исключение для передачи информации об ошибке удаленному подключению.
    /// Исключение этого типа прозрачно транслируется на удаленное подключение.
    /// </summary>
    [Serializable]
    public class RemoteException : Exception
    {
        public ResultCode ErrorCode { get; }

        public RemoteException()
        {

        }

        public RemoteException(string message) : base(message)
        {

        }

        public RemoteException(string message, ResultCode errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
