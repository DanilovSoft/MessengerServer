using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;

namespace DbModel
{
    [Table("User")]
    public class UserDb : IEntity<int>, ICreatedUtc, IUpdatedUtc
    {
        [Key] 
        public int Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        
        [Required] 
        public string Login { get; set; }
        
        [Required] 
        public string NormalLogin { get; set; }
        
        [Required] 
        [StringLength(60, MinimumLength = 60)]
        public string Pasword { get; set; }
        
        public UserProfileDb Profile { get; set; }
    }
}