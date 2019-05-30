using Danilovsoft.MicroORM;
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

        public static void AddDbMicro(this IServiceCollection services, IConfiguration configuration)
        {
            string connectionString = configuration["DbConnectionMicro"];
            SqlORM sql = new SqlORM(connectionString, Npgsql.NpgsqlFactory.Instance);

            services.AddSingleton(typeof(SqlORM), sql);

            //services.AddSingleton<IModelStore, ModelStore>();

            //services
            //    .AddEntityFrameworkNpgsql()
            //    .AddDbContext<CustomEfDbContext>(optionsBuilder =>
            //    {
            //        optionsBuilder.UseNpgsql(configuration["DbConnection"],
            //            o => o.MigrationsAssembly("DbMigrator"));
            //    });

            //services.AddScoped<IDataProvider, EfDataProvider>();
        }
    }
}
