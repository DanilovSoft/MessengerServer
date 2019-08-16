using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace DbModel
{
    public sealed class UserGroupDb
    {
        [DataMember(Name = "group_id")]
        public long GroupId;

        [DataMember(Name = "user_id")]
        public int UserId { get; set; }

        [DataMember(Name = "inviter_id")]
        public int? InviterId;


        //public DateTime CreatedUtc { get; set; }
        //public DateTime? DeletedUtc { get; set; }


        //[ForeignKey(nameof(GroupId))]
        //public GroupDb Group { get; set; }

        //[ForeignKey(nameof(UserId))]
        //public UserDb User { get; set; }

        //[ForeignKey(nameof(InviterId))]
        //public UserDb Inviter { get; set; }
    }
}
