using System;
using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;
using JetBrains.Annotations;

namespace DbModel
{
    [UsedImplicitly]
    [Table("UserGroups")]
    public class UserGroupDb : IEntity, ICreatedUtc, IDeletedUtc
    {
        public long GroupId { get; set; }
        public int UserId { get; set; }
        public int? InviterId { get; set; }


        public DateTime CreatedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }


        [ForeignKey(nameof(GroupId))]
        public GroupDb Group { get; set; }

        [ForeignKey(nameof(UserId))]
        public UserDb User { get; set; }

        [ForeignKey(nameof(InviterId))]
        public UserDb Inviter { get; set; }
    }
}