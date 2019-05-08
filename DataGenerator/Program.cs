using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DbModel.Store;
using EfProvider;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mono.Options;

namespace DataGenerator
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
    public static class GeneratorProgram
    {
        public static string EnvironmentName = Microsoft.AspNetCore.Hosting.EnvironmentName.Development;
        public static string BaseDirectory;

        [UsedImplicitly]
        private static Task Main(string[] args)
        {
            if (args.Any())
            {
                var dataOptions = new OptionSet
                {
                    {"environment=", s => EnvironmentName = s}
                };
                dataOptions.Parse(args);
            }

            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(BaseDirectory ?? Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{EnvironmentName}.json", true)
                .AddJsonFile("appsettings.local.json", true);

            var configuration = configurationBuilder.Build();
            var builder = new DbContextOptionsBuilder()
                .UseNpgsql(configuration["DbConnection"], n => n.MigrationsAssembly("DbMigrator"));

            var modelStore = new ModelStore();
            var context = new CustomEfDbContext(modelStore, builder.Options);
            var provider = new EfDataProvider(context);

            var generator = new DataGenerator(provider, EnvironmentName);
            return generator.Gen();
        }
    }
}