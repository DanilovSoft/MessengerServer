using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using DBCore.Entities;
using JetBrains.Annotations;

namespace DbModel
{
    public sealed class MessageDb
    {
        [DataMember(Name = "message_id")]
        public Guid MessageId;

        [DataMember(Name = "group_id")]
        public long GroupId;

        [DataMember(Name = "user_id")]
        public int UserId;

        [DataMember(Name = "text")]
        public string Text;

        [DataMember(Name = "created")]
        public DateTime CreatedUtc;

        [DataMember(Name = "file_url")]
        public string FileUrl;

        //public Guid Id { get; set; }
        //public long GroupId { get; set; }
        //public int UserId { get; set; }
        //public string Text { get; set; }
        //public string FileUrl { get; set; }

        //public DateTime CreatedUtc { get; set; }
        //public DateTime UpdatedUtc { get; set; }


        //[ForeignKey(nameof(GroupId))]
        //public GroupDb Group { get; set; }

        //[ForeignKey(nameof(UserId))]
        //public UserDb User { get; set; }
    }
}
