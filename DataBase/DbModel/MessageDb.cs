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
    }
}
