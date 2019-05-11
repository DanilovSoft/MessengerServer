using System.ComponentModel.DataAnnotations.Schema;
using DbModel.Base;
using DbModel.DbTypes;
using JetBrains.Annotations;

namespace DbModel
{
    [UsedImplicitly]
    [Table("UserProfiles")]
    public class UserProfileDb : IEntity<int>
    {
        public int Id { get; set; }

        public Gender Gender { get; set; }
        public string AvatarUrl { get; set; }
        public string DisplayName { get; set; }

        [ForeignKey(nameof(Id))]
        public UserDb User { get; set; }
    }
}