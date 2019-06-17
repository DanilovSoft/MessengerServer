using ProtoBuf;
using System;

namespace Dto
{
    [ProtoContract]
    public sealed class ChatMessage
    {
        [ProtoMember(1)]
        public Guid MessageId;

        [ProtoMember(2)]
        public string Text;

        [ProtoMember(3)]
        public DateTime CreatedUtcDate;

        [ProtoMember(4)]
        public bool IsMy;
    }
}
