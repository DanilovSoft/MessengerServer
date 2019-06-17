using System;
using System.Runtime.Serialization;

namespace MessengerServer.Controllers
{
    public sealed class UserConversation
    {
        [DataMember(Name = "group_id")]
        public long GroupId;

        [DataMember(Name = "name")]
        public string GroupName;

        [DataMember(Name = "avatar_url")]
        public string AvatarUrl;

        [DataMember(Name = "message_id")]
        public Guid MessageId;

        [DataMember(Name = "text")]
        public string MessageText;

        [DataMember(Name = "created")]
        public DateTime Created;

        [DataMember(Name = "is_my")]
        public bool IsMy;
    }
}