using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DbModel.Store;
using EfProvider.Config;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EfProvider
{
    public class CustomEfDbContext : DbContext
    {
        private static readonly LoggerFactory _loggerFactory = new LoggerFactory(new[] { new DebugLoggerProvider() });
        // Выводит логи в Debug.
        private static readonly DebugLoggerProvider _debugLoggerProvider = new DebugLoggerProvider();
        private readonly IEnumerable<Type> _modeTypes;

        static CustomEfDbContext()
        {
            EnumFluentConfig.MapEnum();
        }

        // ctor.
        public CustomEfDbContext()
        {
/*
CREATE OR REPLACE FUNCTION public.gen_password (
)
RETURNS trigger AS
$body$
BEGIN
	NEW."Password" = crypt(NEW."Password", gen_salt('bf'));
	RETURN NEW;
END;
$body$
LANGUAGE 'plpgsql'
VOLATILE
CALLED ON NULL INPUT
SECURITY INVOKER
PARALLEL UNSAFE
COST 100;

ALTER FUNCTION public.gen_password ()
  OWNER TO postgres;

CREATE TRIGGER "Users_tr"
BEFORE INSERT OR UPDATE OF "Password" 
ON public."Users"
FOR EACH ROW
    EXECUTE PROCEDURE public.gen_password();
*/
        }

        public CustomEfDbContext(IModelStore modelStore, [NotNull] DbContextOptions options) : base(options)
        {
            _modeTypes = modelStore.GetModels();
        }

        public void Reset()
        {
            EntityEntry[] entries = ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged)
                .ToArray();

            foreach (EntityEntry entry in entries)
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
            optionsBuilder.UseLoggerFactory(_loggerFactory);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasPostgresExtension("uuid-ossp");
            builder.HasPostgresExtension("pgcrypto");

            foreach (Type modelType in _modeTypes)
            {
                builder.Entity(modelType);
            }

            EnumFluentConfig.Config(builder);
            IndexFluentConfig.Config(builder);
            ForeignKeysFluentConfig.Config(builder);
            AutoIncrementConfig.Config(builder);
            DataSeedingConfig.Config(builder);
            PostgresEfExtensions.Config(builder);
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
                    return null;

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