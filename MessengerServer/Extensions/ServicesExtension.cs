using DanilovSoft.MicroORM;
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
            string connectionString = configuration.GetConnectionString("Default");
            var sql = new SqlORM(connectionString, Npgsql.NpgsqlFactory.Instance);

            services.AddSingleton<SqlORM>(sql);
        }
    }
}
