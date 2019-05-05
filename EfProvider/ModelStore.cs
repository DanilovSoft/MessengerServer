using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using DbModel.Base;

namespace EfProvider
{
    [UsedImplicitly]
    public class ModelStore : IModelStore
    {
        private readonly Type[] _modelTypes;

        public ModelStore()
        {
            var domainAssembly = Assembly.Load(new AssemblyName("DbModel"));

            _modelTypes = domainAssembly.GetExportedTypes()
                .Where(x => x.GetInterfaces().Any(type => type == typeof(IDbEntity)))
                .ToArray();
        }

        public IEnumerable<Type> GetModels()
        {
            return _modelTypes;
        }
    }
}
