using DbModel.Store;
using EfProvider;
using MessengerServer.Controllers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using wRPC;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerServer
{
    class Program
    {
        private const int Port = 65125;

        static void Main()
        {
            Console.Title = "Сервер";
            using (var mutex = new Mutex(initiallyOwned: true, $"MessengerServer_Port:{Port}", out bool createdNew))
            {
                if (createdNew)
                {
                    using (var listener = new Listener(Port))
                    {
                        var modelStore = new ModelStore();
                        var builder = new DbContextOptionsBuilder<CustomEfDbContext>();
                        builder.UseNpgsql("Server=10.0.0.101;Port=5432;User Id=postgres;Password=pizdec;Database=MessengerServer;Pooling=true;MinPoolSize=15;MaxPoolSize=20;CommandTimeout=20;Timeout=20");

                        listener.IoC.AddScoped<ISql, Sql>();

                        Console.WriteLine("Ожидание подключений...");
                        listener.StartAccept();

                        Thread.Sleep(-1);
                    }
                }
            }
        }
    }

    class Sql : ISql, IDisposable
    {
        public void Dispose()
        {
            
        }
    }

    interface ISql
    {

    }
}
