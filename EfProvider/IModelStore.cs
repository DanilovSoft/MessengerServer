using System;
using System.Collections.Generic;

namespace EfProvider
{
    public interface IModelStore
    {
        IEnumerable<Type> GetModels();
    }
}
