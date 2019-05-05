﻿using System;
using System.IO;
using System.Linq;
using Domain.Infrastructure.Provider;
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

            if (args.Any() == false)
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

    public class DbContextFactory : IDesignTimeDbContextFactory<DbContextFactory.MigratorEfDataConnection>
    {
        public MigratorEfDataConnection CreateDbContext(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(MigratorProgram.BaseDirectory ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{MigratorProgram.EnvironmentName}.json", true)
                .AddJsonFile("appsettings.local.json", true);

            var configuration = configurationBuilder.Build();

            var modelStore = new ModelStore();

            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            var dbContextOptions = new DbContextOptionsBuilder()
                .UseNpgsql(configuration["DbConnection"], o => o.MigrationsAssembly("DbMigrator")).Options;

            return new MigratorEfDataConnection(modelStore, dbContextOptions, loggerFactory);
        }

        public class MigratorEfDataConnection : CustomEfDataConnection
        {
            private readonly ILoggerFactory _loggerFactory;

            public MigratorEfDataConnection(IModelStore modelStore, [NotNull] DbContextOptions options,
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
