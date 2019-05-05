using System;

namespace DbModel.Base
{
    public interface IDeletedUtc
    {
        DateTime? DeletedUtc { get; set; }
    }
}