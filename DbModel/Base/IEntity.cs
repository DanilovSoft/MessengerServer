using System;

namespace DbModel.Base
{
    public interface IDbEntity
    {
    }
    
    public interface IEntity : IDbEntity
    {
    }

    public interface IEntity<out TKey> : IDbEntity where TKey : IComparable
    {
        TKey Id { get; }
    }
}