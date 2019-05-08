using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;

namespace DbModel
{
    [Table("UserProfile")]
    public class UserProfileDb : IEntity<Guid>
    {
        [Key]
        public Guid Id { get; set; }
        
        public Gender Gender { get; set; }
        
        [ForeignKey(nameof(Id))]
        public UserDb User { get; set; }
    }
}