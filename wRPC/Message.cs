using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace wRPC
{
    /// <summary>
    /// Сериализуемое сообщение для удаленного соединения.
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class Message
    {
        #region Debug
        [JsonIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"\"{(IsRequest ? ActionName : $"Result: {Result}")}\"" + "}";
        #endregion

        /// <summary>
        /// <see langword="true"/> если сообщение явлется запросом.
        /// </summary>
        [JsonProperty(Order = 1, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool IsRequest;

        [JsonProperty(Order = 2)]
        public int Uid;

        [JsonProperty(Order = 3)]
        public string ActionName;

        /// <summary>
        /// Параметры для удаленного метода <see cref="ActionName"/>.
        /// </summary>
        [JsonProperty(Order = 4, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Arg[] Args;

        [JsonProperty(Order = 5, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JToken Result;

        /// <summary>
        /// Не <see langword="null"/> если запрос завершен с ошибкой.
        /// </summary>
        [JsonProperty(Order = 6, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ErrorCode? ErrorCode;

        [JsonProperty(Order = 7, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Error;

        //[JsonIgnore]
        //public bool IsSuccessStatusCode => (ErrorCode == null && Error == null);

        [JsonConstructor]
        private Message() { }

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
        public Message(int uid, object result, string error, ErrorCode? errorCode)
        {
            IsRequest = false; 
            Uid = uid;
            Result = result == null ? null : JToken.FromObject(result);
            Error = error;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Бросает исключение если запрос был завершен с ошибкой.
        /// </summary>
        /// <exception cref="RemoteException"/>
        public void EnsureSuccessStatusCode()
        {
            if (Error != null || ErrorCode != null)
            {
                if (Error != null)
                    throw new RemoteException(Error, ErrorCode);

                throw new RemoteException($"ErrorCode: {ErrorCode.Value}", ErrorCode.Value);
            }
        }

        [DataContract]
        [DebuggerDisplay("{DebugDisplay,nq}")]
        public sealed class Arg
        {
            #region Debug
            [JsonIgnore]
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string DebugDisplay => $"\"{ParameterName}\": {Value}";
            #endregion

            [JsonProperty(Order = 1)]
            public string ParameterName;

            [JsonProperty(Order = 2)]
            public JToken Value;

            [JsonConstructor]
            private Arg() { }

            public Arg(string parameterName, object value)
            {
                ParameterName = parameterName;
                Value = JToken.FromObject(value);
            }
        }
    }
}
