
using ProtoBuf;
using System;

namespace Dto
{
    [ProtoContract]
    public sealed class ChatUser
    {
        [ProtoMember(1)]
        public Uri AvatarUrl;

        [ProtoMember(2)]
        public string Name;

        [ProtoMember(3)]
        public long GroupId;

        [ProtoMember(4)]
        public ChatMessage LastMessage;

        [ProtoMember(5)]
        public bool IsOnline;
    }
}
