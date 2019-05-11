using System;
using System.ComponentModel.DataAnnotations;

namespace DbModel.Base
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