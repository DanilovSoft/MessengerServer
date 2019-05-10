using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;
using DbModel.DbTypes;

namespace DbModel
{
    [Table("UserProfiles")]
    public class UserProfileDb : IEntity<int>
    {
        [Key]
        public int Id { get; set; }
        
        public Gender Gender { get; set; }
        
        [ForeignKey(nameof(Id))]
        public UserDb User { get; set; }
    }
}