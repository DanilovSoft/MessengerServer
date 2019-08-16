using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;

namespace DbModel
{
    [Table("Groups")]
    public class GroupDb
    {
        public long Id { get; set; }
        public int CreatorId { get; set; }

        [StringLength(120)]
        public string Name { get; set; }

        public string AvatarUrl { get; set; }


        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public DateTime? DeletedUtc { get; set; }


        [ForeignKey(nameof(CreatorId))]
        public UserDb Creator { get; set; }

        public ICollection<MessageDb> Messages { get; set; }
        public ICollection<UserGroupDb> Users { get; set; }
    }
}
