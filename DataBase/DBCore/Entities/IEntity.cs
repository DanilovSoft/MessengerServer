using System;
using System.ComponentModel.DataAnnotations;

namespace DBCore.Entities
{
    public interface IEntity
    {
    }

    public interface IEntity<out TKey> : IEntity where TKey : IComparable
    {
        [Key]
        TKey Id { get; }
    }
}
