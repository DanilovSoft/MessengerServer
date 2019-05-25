using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DBCore.Entities;
using JetBrains.Annotations;

namespace DbModel
{
    [UsedImplicitly]
    [Table("Users")]
    public class UserDb : IEntity<int>, ICreatedUtc, IUpdatedUtc
    {
        public int Id { get; set; }

        [Required]
        [StringLength(32)]
        public string Login { get; set; }

        [Required]
        [StringLength(32)]
        public string NormalLogin { get; set; }

        [Required]
        [StringLength(60, MinimumLength = 60)]
        public string Password { get; set; }


        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }


        public UserProfileDb Profile { get; set; }
        public ICollection<UserGroupDb> Groups { get; set; }
        public ICollection<GroupDb> Creations { get; set; }
        public ICollection<UserGroupDb> Invitations { get; set; }
        public ICollection<MessageDb> Messages { get; set; }
        public ICollection<ContactDb> Contacts { get; set; }
        public ICollection<ContactDb> InContacts { get; set; }
    }
}
