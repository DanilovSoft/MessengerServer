using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;

namespace DbModel
{
    [Table("Users")]
    public class UserDb : IEntity<int>, ICreatedUtc, IUpdatedUtc
    {
        [Key] public int Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        [Required]
        [StringLength(32)]
        public string Login { get; set; }

        [Required]
        [StringLength(32)]
        public string NormalLogin { get; set; }

        [Required]
        [StringLength(60, MinimumLength = 60)]
        public string Password { get; set; }

        public UserProfileDb Profile { get; set; }
    }
}