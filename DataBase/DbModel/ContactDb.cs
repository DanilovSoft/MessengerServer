using System.ComponentModel.DataAnnotations.Schema;
using DBCore.Entities;
using JetBrains.Annotations;

namespace DbModel
{
    [UsedImplicitly]
    [Table("Contacts")]
    public class ContactDb : IEntity
    {
        public int WhoId { get; set; }
        public int WhomId { get; set; }

        [ForeignKey(nameof(WhoId))]
        public UserDb Who { get; set; }

        [ForeignKey(nameof(WhomId))]
        public UserDb Whom { get; set; }
    }
}
