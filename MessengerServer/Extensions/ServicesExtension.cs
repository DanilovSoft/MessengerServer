using DBCore;
using DBCore.Store;
using DbModel.Store;
using EfProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerServer.Extensions
{
    internal static class ServicesExtensions
    {
        public static void AddDb(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IModelStore, ModelStore>();
            services
                .AddEntityFrameworkNpgsql()
                .AddDbContext<CustomEfDbContext>(optionsBuilder =>
                {
                    optionsBuilder.UseNpgsql(configuration["DbConnection"],
                        o => o.MigrationsAssembly("DbMigrator"));
                });

            services.AddScoped<IDataProvider, EfDataProvider>();
        }
    }
}
