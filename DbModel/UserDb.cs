using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;

namespace DbModel
{
    [Table("User")]
    public class UserDb : IEntity<Guid>, ICreatedUtc, IUpdatedUtc
    {
        [Key] 
        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        [Required] 
        public string Login { get; set; }
        [Required] 
        public string Pasword { get; set; }
        [Required] 
        public string Salt { get; set; }
        
        public UserProfileDb Profile { get; set; }
    }
}