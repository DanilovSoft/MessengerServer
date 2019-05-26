using System.Diagnostics;

namespace wRPC
{
    /// <summary>
    /// Сериализуемое сообщение для удаленного соединения.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    internal sealed class Message
    {
        #region Debug

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"\"{(StatusCode == StatusCode.Request ? $"Request: {ActionName}" : $"Result: {Result}")}\"" + "}";

        #endregion

        public short Uid { get; set; }
        public StatusCode StatusCode { get; private set; }
        public string ActionName { get; private set; }
        /// <summary>
        /// Параметры для удаленного метода <see cref="ActionName"/>.
        /// </summary>
        public Arg[] Args { get; private set; }
        public object Result { get; private set; }

        public string Error { get; private set; }

        /// <summary>
        /// Связанный запрос.
        /// </summary>
        public RequestMessage ReceivedRequest { get; set; }

        /// <summary>
        /// Конструктор запроса.
        /// </summary>
        private Message(string actionName, Arg[] args)
        {
            ActionName = actionName;
            StatusCode = StatusCode.Request;
            Args = args;
        }

        /// <summary>
        /// Конструктор ответа.
        /// </summary>
        private Message(short uid, object result, string error, StatusCode errorCode)
        {
            Uid = uid;
            Result = result;
            Error = error;
            StatusCode = errorCode;
        }

        public static Message CreateRequest(string actionName, Arg[] args)
        {
            return new Message(actionName, args);
        }

        public static Message FromResult(short uid, object rawResult)
        {
            return new Message(uid, rawResult, error: null, StatusCode.Ok);
        }

        public static Message FromError(short uid, RemoteException remoteException)
        {
            return new Message(uid, result: null, remoteException.Message, remoteException.ErrorCode);
        }

        public static Message FromError(short uid, string errorMessage, StatusCode errorCode)
        {
            return new Message(uid, result: null, errorMessage, errorCode);
        }
    }
}
