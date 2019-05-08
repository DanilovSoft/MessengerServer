using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DbModel.Base;
using JetBrains.Annotations;

namespace DbModel.Store
{
    [UsedImplicitly]
    public class ModelStore : IModelStore
    {
        private readonly Type[] _modelTypes;

        public ModelStore()
        {
            var domainAssembly = Assembly.GetExecutingAssembly();

            _modelTypes = domainAssembly.GetExportedTypes()
                .Where(x => x.IsClass || x.IsValueType)
                .Where(x => x.GetInterfaces().Any(type => type == typeof(IEntity)))
                .ToArray();
        }

        public IEnumerable<Type> GetModels()
        {
            return _modelTypes;
        }
    }
}