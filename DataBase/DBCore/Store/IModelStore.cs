using System;
using System.Collections.Generic;

namespace DBCore.Store
{
    public interface IModelStore
    {
        IEnumerable<Type> GetModels();
    }
}
