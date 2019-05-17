using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.Serialization;

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
        private string DebugDisplay => "{" + $"\"{(IsRequest ? ActionName : $"Result: {Result}")}\"" + "}";

        #endregion

        /// <summary>
        /// <see langword="true"/> если сообщение явлется запросом.
        /// </summary>
        public bool IsRequest { get; set; }
        public short Uid { get; set; }
        public string ActionName { get; set; }
        /// <summary>
        /// Параметры для удаленного метода <see cref="ActionName"/>.
        /// </summary>
        public Arg[] Args { get; set; }
        public object Result { get; set; }

        /// <summary>
        /// Не <see langword="null"/> если запрос завершен с ошибкой.
        /// </summary>
        public ResultCode ErrorCode { get; set; }

        public string Error { get; set; }

        public RequestMessage Request { get; set; }

        protected Message()
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
        public Message(short uid, object result, string error, ResultCode errorCode)
        {
            IsRequest = false; 
            Uid = uid;
            Result = result;
            Error = error;
            ErrorCode = errorCode;
        }
    }
}
