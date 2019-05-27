using ProtoBuf;
using System;

namespace Dto
{
    [ProtoContract]
    public class SendMessageResult
    {
        [ProtoMember(1)]
        public byte[] MessageId { get; set; }

        [ProtoMember(2)]
        public DateTime Date { get; set; }
    }
}
