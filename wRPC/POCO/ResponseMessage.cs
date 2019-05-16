using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace wRPC
{
    internal sealed class ResponseMessage
    {
        public ResultCode ResultCode;
        /// <summary>
        /// Сообщение ошибки.
        /// </summary>
        public string Error;
        public object Result;

        /// <summary>
        /// Бросает исключение если запрос был завершен с ошибкой.
        /// </summary>
        /// <exception cref="RemoteException"/>
        public void EnsureSuccessStatusCode()
        {
            if (Error != null || ResultCode != ResultCode.Ok)
            {
                if (Error != null)
                    throw new RemoteException(Error, ResultCode);

                throw new RemoteException($"ErrorCode: {ResultCode}", ResultCode);
            }
        }
    }
}
