using MessengerServer.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessengerServer
{
    public class ApplicationContext : DbContext
    {
        public static readonly LoggerFactory _myLoggerFactory = new LoggerFactory(new[] { new DebugLoggerProvider() });

        public DbSet<User> Users { get; set; }

        [DbFunction("crypt")]
        public static string Crypt(string password, string salt)
        {
            throw new NotImplementedException();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLoggerFactory(_myLoggerFactory); // Warning: Do not create a new ILoggerFactory instance each time

            optionsBuilder.UseNpgsql("Host=10.0.0.101;Port=5432;Database=postgres;Username=postgres;Password=pizdec");
        }
    }
}
