using MsgPack;
using MsgPack.Serialization;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace Contract
{
    /// <summary>
    /// Сериализуемый запрос для удаленного соединения.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class Request
    {
        #region Debug
        [MessagePackIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"\"{ActionName}\"" + "}";
        #endregion

        [MessagePackMember(1)]
        public int Uid { get; set; }

        [MessagePackMember(2)]
        public string ActionName { get; set; }

        /// <summary>
        /// Параметры для удаленного метода <see cref="ActionName"/>.
        /// </summary>
        [MessagePackMember(3)]
        public Arg[] Args { get; set; }

        [DebuggerDisplay("{DebugDisplay,nq}")]
        public sealed class Arg
        {
            #region Debug
            [MessagePackIgnore]
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string DebugDisplay => $"\"{ParameterName}\": {Value}";
            #endregion

            [MessagePackMember(1)]
            public string ParameterName { get; }

            [MessagePackMember(2)]
            public MessagePackObject Value { get; }

            public Arg(string parameterName, MessagePackObject value)
            {
                ParameterName = parameterName;
                Value = value;
            }
        }
    }

    /// <summary>
    /// Сериализуемый ответ на запрос <see cref="Request"/>.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class Response
    {
        #region Debug
        [MessagePackIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"\"{(Error == null ? $"Result: {Result}" : $"Error: {ErrorCode}")}\"" + "}";
        #endregion

        [MessagePackMember(1)]
        public int Uid { get; }

        [MessagePackMember(2)]
        public MessagePackObject Result { get; }

        [MessagePackMember(3)]
        public string Error { get; }

        /// <summary>
        /// Не <see langword="null"/> если запрос завершен с ошибкой.
        /// </summary>
        [MessagePackMember(4)]
        public ErrorCode? ErrorCode { get; }

        [MessagePackIgnore]
        public bool IsSuccessStatusCode => (ErrorCode == null);

        // ctor
        public Response(int uid, MessagePackObject result, string error, ErrorCode? errorCode)
        {
            Uid = uid;
            Result = result;
            Error = error;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Бросает исключение если запрос был завершен с ошибкой.
        /// </summary>
        /// <exception cref="RemoteException"/>
        public void EnsureSuccessStatusCode()
        {
            if(!IsSuccessStatusCode)
            {
                if(Error != null)
                    throw new RemoteException(Error, ErrorCode.Value);

                throw new RemoteException($"ErrorCode: {ErrorCode.Value}", ErrorCode.Value);
            }
        }
    }
}
