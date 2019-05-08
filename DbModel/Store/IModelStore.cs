using System;
using System.Collections.Generic;

namespace DbModel.Store
{
    public interface IModelStore
    {
        IEnumerable<Type> GetModels();
    }
}
