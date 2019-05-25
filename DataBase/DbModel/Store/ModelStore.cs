﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DBCore.Entities;
using DBCore.Store;
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
                .Where(x => !x.IsInterface && typeof(IEntity).IsAssignableFrom(x))
                .ToArray();
        }

        public IEnumerable<Type> GetModels()
        {
            return _modelTypes;
        }
    }
}
