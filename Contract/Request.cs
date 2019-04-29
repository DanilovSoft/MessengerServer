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
    public sealed class Request
    {
        [MessagePackMember(1)]
        public int Uid { get; set; }

        [MessagePackMember(2)]
        public string ActionName { get; set; }

        [MessagePackMember(3)]
        public Arg[] Args { get; set; }

        [DebuggerDisplay("{DebugDisplay,nq}")]
        public class Arg
        {
            [MessagePackIgnore]
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string DebugDisplay => $"\"{ParameterName}\": {Value}";

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

    public readonly struct Response
    {
        [MessagePackMember(1)]
        public int Uid { get; }

        [MessagePackMember(2)]
        public MessagePackObject Result { get; }

        [MessagePackMember(3)]
        public string Error { get; }

        [MessagePackDeserializationConstructor]
        public Response(int uid, MessagePackObject result, string error)
        {
            Uid = uid;
            Result = result;
            Error = error;
        }

        //public Response(int uid, object result, string error = null)
        //{
        //    Uid = uid;
        //    Result = MessagePackObject.FromObject(result);
        //    Error = error;
        //}
    }
}
