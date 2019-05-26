using ProtoBuf;
using System;
using wRPC;

namespace Contract.Dto
{
    [ProtoContract]
    public sealed class AuthorizationResult
    {
        [ProtoMember(1)]
        public BearerToken BearerToken { get; set; }

        [ProtoMember(2)]
        public int UserId { get; set; }

        [ProtoMember(3)]
        public string UserName { get; set; }

        [ProtoMember(4)]
        public Uri ImageUrl { get; set; }
    }
}
