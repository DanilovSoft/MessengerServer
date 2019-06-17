﻿using System.IO;
using System.Threading.Tasks;
using DbModel.Store;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mono.Options;

namespace DataGenerator
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    public static class GeneratorProgram
    {
        private static string _environmentName = Microsoft.AspNetCore.Hosting.EnvironmentName.Development;
        private static string _baseDirectory;

        [UsedImplicitly]
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var dataOptions = new OptionSet
                {
                    {"environment=", s => _environmentName = s}
                };
                dataOptions.Parse(args);
            }

            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(_baseDirectory ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{_environmentName}.json", true)
                .AddJsonFile("appsettings.local.json", true);

            IConfigurationRoot configuration = configurationBuilder.Build();

            string connectionString = configuration.GetConnectionString("Default");
            var sql = new DanilovSoft.MicroORM.SqlORM(connectionString, Npgsql.NpgsqlFactory.Instance);

            var generator = new DataGenerator(sql, _environmentName);

            generator.Gen();
        }
    }
}