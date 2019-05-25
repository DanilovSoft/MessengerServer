using System;
using System.IO;
using DBCore.Store;
using DbModel.Store;
using EfProvider;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace DbMigrator
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    public static class MigratorProgram
    {
        public static string EnvironmentName = Microsoft.AspNetCore.Hosting.EnvironmentName.Development;
        public static string BaseDirectory;

        [UsedImplicitly]
        private static void Main(string[] args)
        {
            var dataOptions = new OptionSet
            {
                {"environment=", s => EnvironmentName = s}
            };
            var actionOptions = new OptionSet
            {
                {"migrate", s => Migrate()}
            };

            if (args.Length == 0)
            {
                Console.WriteLine("Support parameters");
                dataOptions.WriteOptionDescriptions(Console.Out);
                actionOptions.WriteOptionDescriptions(Console.Out);
                Console.ReadKey();
            }

            dataOptions.Parse(args);
            actionOptions.Parse(args);
        }

        private static void Migrate()
        {
            var context = new DbContextFactory().CreateDbContext(new string[0]);
            context.Database.Migrate();
        }
    }

    public class DbContextFactory : IDesignTimeDbContextFactory<DbContextFactory.MigratorEfDbContext>
    {
        public MigratorEfDbContext CreateDbContext(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(MigratorProgram.BaseDirectory ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{MigratorProgram.EnvironmentName}.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true);

            IConfigurationRoot configuration = configurationBuilder.Build();

            var modelStore = new ModelStore();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            var dbContextOptions = new DbContextOptionsBuilder()
                .UseNpgsql(configuration["DbConnection"], o => o.MigrationsAssembly("DbMigrator")).Options;

            return new MigratorEfDbContext(modelStore, dbContextOptions, loggerFactory);
        }

        public class MigratorEfDbContext : CustomEfDbContext
        {
            private readonly ILoggerFactory _loggerFactory;

            public MigratorEfDbContext(IModelStore modelStore, [NotNull] DbContextOptions options,
                ILoggerFactory loggerFactory) : base(modelStore, options)
            {
                _loggerFactory = loggerFactory;
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                base.OnConfiguring(optionsBuilder);

                if (_loggerFactory != null)
                {
                    optionsBuilder.UseLoggerFactory(_loggerFactory);
                }
            }
        }
    }
}
