using System;
using System.Collections.Generic;
using System.Text;

namespace Contract
{
    /// <summary>
    /// Исключение для передачи информации об ошибке удаленному подключению.
    /// </summary>
    [Serializable]
    public class RemoteException : Exception
    {
        public ErrorCode ErrorCode { get; }

        public RemoteException()
        {

        }

        public RemoteException(string message, ErrorCode errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
