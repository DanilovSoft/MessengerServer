﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EfProvider.Config;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Npgsql;

namespace EfProvider
{
    public class CustomEfDataConnection : DbContext
    {
        private readonly IEnumerable<Type> _modeTypes;

        static CustomEfDataConnection()
        {
            EnumFluentConfig.MapEnum();
        }

        public CustomEfDataConnection(IModelStore modelStore, [NotNull] DbContextOptions options) : base(options)
        {
            _modeTypes = modelStore.GetModels();
        }

        public void Reset()
        {
            var entries = ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToArray();
            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Modified:
                        entry.State = EntityState.Unchanged;
                        break;
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                    case EntityState.Deleted:
                        entry.Reload();
                        break;
                }
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.ReplaceService<IEntityMaterializerSource, MyEntityMaterializerSource>();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var item in _modeTypes)
            {
                builder.Entity(item);
            }

            builder.HasPostgresExtension("uuid-ossp");

            EnumFluentConfig.Config(builder);
            IndexFluentConfig.Config(builder);
            ForeignKeysFluentConfig.Config(builder);
            DataSeedingConfig.Config(builder);
        }

        private static class DateTimeMapper
        {
            public static DateTime Normalize(DateTime value)
            {
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }

            public static DateTime? NormalizeNullable(DateTime? value)
            {
                if (value == null)
                {
                    return null;
                }

                return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
            }
        }

        [UsedImplicitly]
        private class MyEntityMaterializerSource : EntityMaterializerSource
        {
            private static readonly MethodInfo NormalizeMethod =
                typeof(DateTimeMapper).GetTypeInfo().GetMethod(nameof(DateTimeMapper.Normalize));

            private static readonly MethodInfo NormalizeNullableMethod = typeof(DateTimeMapper).GetTypeInfo()
                .GetMethod(nameof(DateTimeMapper.NormalizeNullable));

            public override Expression CreateReadValueExpression(Expression valueBuffer, Type type, int index,
                IPropertyBase property)
            {
                if (type == typeof(DateTime))
                {
                    return Expression.Call(
                        NormalizeMethod,
                        base.CreateReadValueExpression(valueBuffer, type, index, property)
                    );
                }

                if (type == typeof(DateTime?))
                {
                    return Expression.Call(
                        NormalizeNullableMethod,
                        base.CreateReadValueExpression(valueBuffer, type, index, property)
                    );
                }

                return base.CreateReadValueExpression(valueBuffer, type, index, property);
            }
        }
    }
}
