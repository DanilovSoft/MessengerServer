using MsgPack;
using MsgPack.Serialization;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
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
        public MessagePackObject[] Args { get; set; }
    }

    public readonly struct Response
    {
        [MessagePackMember(1)]
        public int Uid { get; }

        [MessagePackMember(2)]
        public MessagePackObject Result { get; }

        [MessagePackDeserializationConstructor]
        public Response(int uid, MessagePackObject result)
        {
            Uid = uid;
            Result = result;
        }

        public Response(int uid, object result)
        {
            Uid = uid;
            Result = MessagePackObject.FromObject(result);
        }
    }
}
