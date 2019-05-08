using System;

namespace DbModel.Base
{
    
    public interface IEntity 
    {
    }

    public interface IEntity<out TKey> : IEntity where TKey : IComparable
    {
        TKey Id { get; }
    }
}