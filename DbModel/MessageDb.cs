using System;
using System.ComponentModel.DataAnnotations.Schema;
using DBCore.Entities;
using JetBrains.Annotations;

namespace DbModel
{
    [UsedImplicitly]
    [Table("Messages")]
    public class MessageDb : IEntity<Guid>, ICreatedUtc, IUpdatedUtc
    {
        public Guid Id { get; set; }
        public long GroupId { get; set; }
        /// <summary>
        /// ������������ ��������� ���������.
        /// </summary>
        public int UserId { get; set; }
        public string Text { get; set; }
        public string FileUrl { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }


        [ForeignKey(nameof(GroupId))]
        public GroupDb Group { get; set; }

        [ForeignKey(nameof(UserId))]
        public UserDb User { get; set; }
    }
}
