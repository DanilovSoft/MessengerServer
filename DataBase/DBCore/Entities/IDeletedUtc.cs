using System;

namespace DBCore.Entities
{
    public interface IDeletedUtc
    {
        DateTime? DeletedUtc { get; set; }
    }
}
