using MsgPack;
using MsgPack.Serialization;
using System.Diagnostics;
using wRPC;

namespace Contract
{
    /// <summary>
    /// Сериализуемое сообщение для удаленного соединения.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class Message
    {
        #region Debug
        [MessagePackIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"\"{ActionName}\"" + "}";
        #endregion

        [MessagePackMember(1)]
        public bool IsRequest;

        [MessagePackMember(2)]
        public int Uid { get; set; }

        [MessagePackMember(3)]
        public string ActionName { get; set; }

        /// <summary>
        /// Параметры для удаленного метода <see cref="ActionName"/>.
        /// </summary>
        [MessagePackMember(4)]
        public Arg[] Args { get; set; }

        [MessagePackMember(5)]
        public MessagePackObject Result { get; set; }

        /// <summary>
        /// Не <see langword="null"/> если запрос завершен с ошибкой.
        /// </summary>
        [MessagePackMember(6)]
        public ErrorCode? ErrorCode { get; set; }

        [MessagePackMember(7)]
        public string Error { get; set; }

        [MessagePackIgnore]
        public bool IsSuccessStatusCode => (ErrorCode == null);

        [MessagePackDeserializationConstructor]
        public Message()
        {

        }

        /// <summary>
        /// Конструктор запроса.
        /// </summary>
        public Message(string actionName)
        {
            ActionName = actionName;
            IsRequest = true;
        }

        /// <summary>
        /// Конструктор ответа.
        /// </summary>
        public Message(int uid, MessagePackObject result, string error, ErrorCode? errorCode)
        {
            IsRequest = false; 
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
            if (!IsSuccessStatusCode)
            {
                if (Error != null)
                    throw new RemoteException(Error, ErrorCode.Value);

                throw new RemoteException($"ErrorCode: {ErrorCode.Value}", ErrorCode.Value);
            }
        }

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

    ///// <summary>
    ///// Сериализуемый ответ на запрос <see cref="Request"/>.
    ///// </summary>
    //[DebuggerDisplay("{DebugDisplay,nq}")]
    //public sealed class Response
    //{
    //    #region Debug
    //    [MessagePackIgnore]
    //    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    //    private string DebugDisplay => "{" + $"{(Error == null ? $"Result: {Result}" : $"Error: \"{Error}\"")}" + "}";
    //    #endregion

    //    [MessagePackMember(1)]
    //    public int Uid { get; }

    //    [MessagePackMember(2)]
    //    public MessagePackObject Result { get; }

    //    [MessagePackMember(3)]
    //    public string Error { get; }

    //    /// <summary>
    //    /// Не <see langword="null"/> если запрос завершен с ошибкой.
    //    /// </summary>
    //    [MessagePackMember(4)]
    //    public ErrorCode? ErrorCode { get; }

    //    [MessagePackIgnore]
    //    public bool IsSuccessStatusCode => (ErrorCode == null);

    //    // ctor
    //    public Response(int uid, MessagePackObject result, string error, ErrorCode? errorCode)
    //    {
    //        Uid = uid;
    //        Result = result;
    //        Error = error;
    //        ErrorCode = errorCode;
    //    }

    //    /// <summary>
    //    /// Бросает исключение если запрос был завершен с ошибкой.
    //    /// </summary>
    //    /// <exception cref="RemoteException"/>
    //    public void EnsureSuccessStatusCode()
    //    {
    //        if(!IsSuccessStatusCode)
    //        {
    //            if(Error != null)
    //                throw new RemoteException(Error, ErrorCode.Value);

    //            throw new RemoteException($"ErrorCode: {ErrorCode.Value}", ErrorCode.Value);
    //        }
    //    }
    //}
}
